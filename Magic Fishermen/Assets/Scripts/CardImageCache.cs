using System.Collections.Generic;
using UnityEngine;

public class CardImageCache : MonoBehaviour
{
    private Dictionary<string, Texture2D> imageCache = new();
    private Dictionary<string, ScryfallCard> dataCache = new();

    public bool TryGet(string name, out Texture2D tex, out ScryfallCard data)
    {
        bool hasImage = imageCache.TryGetValue(name, out tex);
        bool hasData = dataCache.TryGetValue(name, out data);

        return hasImage && hasData;
    }

    public void Store(string name, Texture2D tex, ScryfallCard data)
    {
        if (!imageCache.ContainsKey(name))
            imageCache[name] = tex;

        if (!dataCache.ContainsKey(name))
            dataCache[name] = data;
    }
}