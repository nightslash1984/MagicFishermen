using System.Collections.Generic;
using UnityEngine;

public class CardImageCache : MonoBehaviour
{
    private Dictionary<string, Sprite> cache = new();

    public bool TryGet(string name, out Sprite sprite)
    {
        return cache.TryGetValue(name, out sprite);
    }

    public void Store(string name, Sprite sprite)
    {
        if (!cache.ContainsKey(name))
            cache.Add(name, sprite);
    }
}
