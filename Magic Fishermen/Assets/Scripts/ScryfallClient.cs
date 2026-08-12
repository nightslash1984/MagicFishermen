using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class ScryfallCard
{
    public string name;
    public ImageUris image_uris;
    public CardFace[] card_faces;

    public string mana_cost;

    public string type_line;
    public string oracle_text;

    public string[] color_identity;
    public ScryfallLegalities legalities;

    public string set;

    public int cmc;
}

[Serializable]
public class CardFace
{
    public string name;
    public ImageUris image_uris;
}

[Serializable]
public class ImageUris
{
    public string normal;
    public string small;
}

public class ScryfallClient : MonoBehaviour
{
    public TMP_Text errorText;
    private const string UserAgent = "MagicFishermen/1.0 (https://github.com/nightslash1984/MagicFishermen)";

    // Scryfall asks API clients to avoid bursts. Keep this shared across all
    // client instances so a second deck import cannot bypass the throttle.
    // Keep a deliberately conservative shared budget for both api.scryfall.com
    // and cards.scryfall.io. A deck import makes one request of each kind per
    // unique card, and some networks apply the same rate limit to both hosts.
    private const float MinimumScryfallRequestInterval = 0.25f;
    private static float nextScryfallRequestTime;

    private static IEnumerator WaitForScryfallRequestSlot()
    {
        float delay = nextScryfallRequestTime - Time.realtimeSinceStartup;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        nextScryfallRequestTime = Time.realtimeSinceStartup + MinimumScryfallRequestInterval;
    }

    private static void ApplyRateLimitCooldown(float waitSeconds)
    {
        nextScryfallRequestTime = Mathf.Max(
            nextScryfallRequestTime,
            Time.realtimeSinceStartup + waitSeconds
        );
    }

    private static void SetScryfallHeaders(UnityWebRequest request, string accept)
    {
        // Scryfall rejects requests that do not identify the client and state
        // the expected response type. This is required for image URLs too.
        request.SetRequestHeader("User-Agent", UserAgent);
        request.SetRequestHeader("Accept", accept);
    }

    public IEnumerator GetCardsDataBatch(List<string> cardNames, Action<Dictionary<string, ScryfallCard>> onComplete)
    {
        var identifiers = new ScryfallIdentifier[cardNames.Count];
        for (int i = 0; i < cardNames.Count; i++)
            identifiers[i] = new ScryfallIdentifier { name = cardNames[i] };

        byte[] body = System.Text.Encoding.UTF8.GetBytes(
            JsonUtility.ToJson(new ScryfallCollectionRequest { identifiers = identifiers })
        );

        yield return WaitForScryfallRequestSlot();
        using (var request = new UnityWebRequest("https://api.scryfall.com/cards/collection", UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            SetScryfallHeaders(request, "application/json");
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 20;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode == 429)
                {
                    float cooldown = 60f;
                    string retryAfter = request.GetResponseHeader("Retry-After");
                    if (!string.IsNullOrEmpty(retryAfter) && float.TryParse(retryAfter, out var parsed)) cooldown = parsed;
                    ApplyRateLimitCooldown(cooldown);
                }
                Debug.LogWarning($"Scryfall collection request failed (code={request.responseCode} error={request.error})");
                onComplete?.Invoke(null);
                yield break;
            }

            var response = JsonUtility.FromJson<ScryfallCollectionResponse>(request.downloadHandler.text);
            var results = new Dictionary<string, ScryfallCard>();
            if (response?.data != null)
                foreach (var card in response.data)
                    if (card != null && !string.IsNullOrEmpty(card.name))
                        results[CardImageCache.NormalizeName(card.name)] = card;
            onComplete?.Invoke(results);
        }
    }

    public IEnumerator GetCardImage(ScryfallCard card, Action<bool, Texture2D> onComplete)
    {
        string imageUrl = card?.image_uris?.normal;
        if (string.IsNullOrEmpty(imageUrl) && card?.card_faces != null && card.card_faces.Length > 0)
            imageUrl = card.card_faces[0]?.image_uris?.normal;
        if (string.IsNullOrEmpty(imageUrl))
        {
            onComplete?.Invoke(false, null);
            yield break;
        }

        yield return WaitForScryfallRequestSlot();
        using (var request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            SetScryfallHeaders(request, "image/*,*/*;q=0.8");
            request.timeout = 20;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode == 429)
                {
                    float cooldown = 60f;
                    string retryAfter = request.GetResponseHeader("Retry-After");
                    if (!string.IsNullOrEmpty(retryAfter) && float.TryParse(retryAfter, out var parsed)) cooldown = parsed;
                    ApplyRateLimitCooldown(cooldown);
                }
                Debug.LogWarning($"Image request failed: {card.name} (code={request.responseCode} error={request.error})");
                onComplete?.Invoke(false, null);
                yield break;
            }
            onComplete?.Invoke(true, DownloadHandlerTexture.GetContent(request));
        }
    }

    public IEnumerator ResolveCardAlias(string cardName, Action<ScryfallCard> onComplete)
    {
        string url = "https://api.scryfall.com/cards/named?fuzzy=" + UnityWebRequest.EscapeURL(cardName);
        yield return WaitForScryfallRequestSlot();

        using (var request = UnityWebRequest.Get(url))
        {
            SetScryfallHeaders(request, "application/json");
            request.timeout = 15;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(null);
                yield break;
            }

            ScryfallCard card = JsonUtility.FromJson<ScryfallCard>(request.downloadHandler.text);
            string requestedName = CardImageCache.NormalizeName(cardName);
            bool isSafeMatch = CardImageCache.NormalizeName(card?.name) == requestedName;

            if (!isSafeMatch && card?.card_faces != null)
            {
                foreach (CardFace face in card.card_faces)
                {
                    if (CardImageCache.NormalizeName(face?.name) == requestedName)
                    {
                        isSafeMatch = true;
                        break;
                    }
                }
            }

            // Fuzzy search can return a similarly spelled but different card.
            // Only accept it when the requested name is the card or a face name.
            onComplete?.Invoke(isSafeMatch ? card : null);
        }
    }

    public IEnumerator GetCardData(string cardName, Action<bool, ScryfallCard, Texture2D> onComplete)
    {
        bool called = false;

        void Complete(bool success, ScryfallCard card, Texture2D tex)
        {
            if (called) return;
            called = true;
            onComplete?.Invoke(success, card, tex);
        }

        string url = "https://api.scryfall.com/cards/named?exact=" + UnityWebRequest.EscapeURL(cardName);

        // --- Metadata request with retry-on-429 ---
        const int maxMetaAttempts = 4;
        int metaAttempt = 0;
        UnityWebRequest request = null;
        string responseText = null;

        while (metaAttempt < maxMetaAttempts)
        {
            metaAttempt++;

            yield return WaitForScryfallRequestSlot();
            request = UnityWebRequest.Get(url);
            SetScryfallHeaders(request, "application/json");
            request.timeout = 15;

            yield return request.SendWebRequest();

            long code = request.responseCode;
            if (request.result == UnityWebRequest.Result.Success)
            {
                responseText = request.downloadHandler.text;
                request.Dispose();
                break;
            }

            // If rate limited, respect Retry-After (if present) or back off a bit and retry
            if (code == 429)
            {
                string retryHdr = request.GetResponseHeader("Retry-After");
                float waitSeconds = 1f;
                if (!string.IsNullOrEmpty(retryHdr) && float.TryParse(retryHdr, out var parsed))
                    waitSeconds = parsed;

                Debug.LogWarning($"Scryfall metadata 429 for '{cardName}', retrying after {waitSeconds}s (attempt {metaAttempt}/{maxMetaAttempts})");
                ApplyRateLimitCooldown(waitSeconds);
                request.Dispose();
                continue;
            }

            // other non-retryable failure
            Debug.LogWarning($"Card metadata request failed: {cardName} (code={code} error={request.error})");
            request.Dispose();
            Complete(false, null, null);
            yield break;
        }

        if (responseText == null)
        {
            Debug.LogWarning($"Failed to fetch metadata for {cardName} after {maxMetaAttempts} attempts.");
            Complete(false, null, null);
            yield break;
        }

        ScryfallCard card = null;
        try
        {
            card = JsonUtility.FromJson<ScryfallCard>(responseText);
        }
        catch (Exception ex)
        {
            Debug.LogError($"JSON parse failed for '{cardName}': {ex}");
            Complete(false, null, null);
            yield break;
        }

        string imageUrl = null;
        if (card.image_uris != null && !string.IsNullOrEmpty(card.image_uris.normal))
            imageUrl = card.image_uris.normal;
        else if (card.card_faces != null && card.card_faces.Length > 0)
        {
            var face = card.card_faces[0];
            if (face?.image_uris != null)
                imageUrl = face.image_uris.normal;
        }

        if (string.IsNullOrEmpty(imageUrl))
        {
            Debug.LogWarning("No image for: " + cardName);
            Complete(false, null, null);
            yield break;
        }

        // --- Image request with retry-on-429 ---
        const int maxImgAttempts = 4;
        int imgAttempt = 0;
        UnityWebRequest imgRequest = null;
        Texture2D tex = null;

        while (imgAttempt < maxImgAttempts)
        {
            imgAttempt++;
            yield return WaitForScryfallRequestSlot();
            imgRequest = UnityWebRequestTexture.GetTexture(imageUrl);
            SetScryfallHeaders(imgRequest, "image/*,*/*;q=0.8");
            imgRequest.timeout = 20;

            yield return imgRequest.SendWebRequest();

            long code = imgRequest.responseCode;
            if (imgRequest.result == UnityWebRequest.Result.Success)
            {
                tex = DownloadHandlerTexture.GetContent(imgRequest);
                imgRequest.Dispose();
                break;
            }

            if (code == 429)
            {
                string retryHdr = imgRequest.GetResponseHeader("Retry-After");
                float waitSeconds = 1f;
                if (!string.IsNullOrEmpty(retryHdr) && float.TryParse(retryHdr, out var parsed))
                    waitSeconds = parsed;

                Debug.LogWarning($"Image 429 for '{cardName}', retrying after {waitSeconds}s (attempt {imgAttempt}/{maxImgAttempts})");
                ApplyRateLimitCooldown(waitSeconds);
                imgRequest.Dispose();
                continue;
            }

            Debug.LogWarning($"Image request failed: {cardName} (code={code} error={imgRequest.error})");
            imgRequest.Dispose();
            Complete(false, null, null);
            yield break;
        }

        if (tex == null)
        {
            Debug.LogWarning($"Failed to download image for {cardName} after {maxImgAttempts} attempts.");
            Complete(false, null, null);
            yield break;
        }

        Complete(true, card, tex);
    }
}

[Serializable]
public class ScryfallLegalities
{
    public string commander;
}

[Serializable]
public class ScryfallIdentifier
{
    public string name;
}

[Serializable]
public class ScryfallCollectionRequest
{
    public ScryfallIdentifier[] identifiers;
}

[Serializable]
public class ScryfallCollectionResponse
{
    public ScryfallCard[] data;
}
