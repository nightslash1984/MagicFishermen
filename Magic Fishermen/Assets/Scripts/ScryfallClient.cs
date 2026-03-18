using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using static System.Net.WebRequestMethods;

[Serializable]
public class ScryfallCard
{
    public ImageUris image_uris;
}

[Serializable]
public class ImageUris
{
    public string normal;
}

public class ScryfallClient : MonoBehaviour
{
    public TMP_Text errorText;

    public IEnumerator GetCardImage(string cardName, Action<Texture2D> onLoaded)
    {
        string url = "https://api.scryfall.com/cards/named?exact=" + UnityWebRequest.EscapeURL(cardName);

        UnityWebRequest request = UnityWebRequest.Get(url);

        // REQUIRED: Proper User-Agent
        request.SetRequestHeader(
            "User-Agent",
            "Nightslash - MTG - Deckbuilder / 1.0(Unity; UWP; +https://github.com/nightslash1984/MagicFishermen#)"
        );

        // Optional but recommended
        request.SetRequestHeader("Accept", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("FAILED: " + cardName);
            Debug.LogError("Error: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);
            Debug.LogError("URL: " + url);
            errorText.text = $"Failed to load card: {cardName}";
            yield break;
        }

        ScryfallCard card = JsonUtility.FromJson<ScryfallCard>(request.downloadHandler.text);

        UnityWebRequest imgRequest = UnityWebRequestTexture.GetTexture(card.image_uris.normal);
        yield return imgRequest.SendWebRequest();

        if (imgRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Image load failed: " + cardName);
            yield break;
        }

        Texture2D tex = DownloadHandlerTexture.GetContent(imgRequest);
        onLoaded?.Invoke(tex);
    }
}
