using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System;

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
    public IEnumerator GetCardImage(string cardName, Action<Texture2D> onLoaded)
    {
        string url = "https://api.scryfall.com/cards/named?exact=" + UnityWebRequest.EscapeURL(cardName);

        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Card fetch failed: " + cardName);
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
