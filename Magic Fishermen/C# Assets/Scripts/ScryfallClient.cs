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
            request = UnityWebRequest.Get(url);
            request.SetRequestHeader("User-Agent", "Nightslash - MTG - Deckbuilder / 1.0(Unity; UWP; +https://github.com/nightslash1984/MagicFishermen#)");
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
                request.Dispose();
                yield return new WaitForSeconds(waitSeconds);
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
            imgRequest = UnityWebRequestTexture.GetTexture(imageUrl);
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
                imgRequest.Dispose();
                yield return new WaitForSeconds(waitSeconds);
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