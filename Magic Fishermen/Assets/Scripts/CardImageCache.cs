using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public class CardImageCache : MonoBehaviour
{
    private Dictionary<string, Texture2D> imageCache = new();
    private Dictionary<string, ScryfallCard> dataCache = new();
    private string cacheDirectory;

    private void Awake()
    {
        cacheDirectory = Path.Combine(Application.persistentDataPath, "ScryfallCache-Normal");
        Directory.CreateDirectory(cacheDirectory);
    }

    public bool TryGet(string name, out Texture2D tex, out ScryfallCard data)
    {
        string key = NormalizeName(name);
        bool hasImage = imageCache.TryGetValue(key, out tex);
        bool hasData = dataCache.TryGetValue(key, out data);

        if (hasImage && hasData)
            return true;

        return TryLoadFromDisk(key, out tex, out data);
    }

    public void Store(string name, Texture2D tex, ScryfallCard data)
    {
        string key = NormalizeName(name);

        imageCache[key] = tex;
        dataCache[key] = data;

        try
        {
            File.WriteAllBytes(GetCachePath(key, ".png"), tex.EncodeToPNG());
            File.WriteAllText(GetCachePath(key, ".json"), JsonUtility.ToJson(data));
        }
        catch (IOException exception)
        {
            Debug.LogWarning("Could not save Scryfall cache entry: " + exception.Message);
        }
    }

    private bool TryLoadFromDisk(string key, out Texture2D tex, out ScryfallCard data)
    {
        tex = null;
        data = null;
        try
        {
            string imagePath = GetCachePath(key, ".png");
            string dataPath = GetCachePath(key, ".json");
            if (!File.Exists(imagePath) || !File.Exists(dataPath))
                return false;

            data = JsonUtility.FromJson<ScryfallCard>(File.ReadAllText(dataPath));
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (data == null || !tex.LoadImage(File.ReadAllBytes(imagePath)))
            {
                Destroy(tex);
                tex = null;
                data = null;
                return false;
            }

            imageCache[key] = tex;
            dataCache[key] = data;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private string GetCachePath(string key, string extension)
    {
        if (string.IsNullOrEmpty(cacheDirectory))
            cacheDirectory = Path.Combine(Application.persistentDataPath, "ScryfallCache-Normal");

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
            var name = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
                name.Append(value.ToString("x2"));
            return Path.Combine(cacheDirectory, name + extension);
        }
    }

    public static string NormalizeName(string name)
    {
        return (name ?? string.Empty).Trim().ToLowerInvariant();
    }
}
