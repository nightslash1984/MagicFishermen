using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeckDisplay : MonoBehaviour
{
    public Transform content;
    public GameObject cardPrefab;

    public ScryfallClient scryfall;
    public CardImageCache cache;

    public void DisplayDeck(List<string> cardNames)
    {
        StartCoroutine(BuildList(cardNames));
    }

    IEnumerator BuildList(List<string> cards)
    {
        foreach (var cardName in cards)
        {
            GameObject obj = Instantiate(cardPrefab, content);
            Image img = obj.GetComponent<Image>();

            if (cache.TryGet(cardName, out Sprite cachedSprite))
            {
                img.sprite = cachedSprite;
                continue;
            }

            yield return StartCoroutine(
                scryfall.GetCardImage(cardName, (texture) =>
                {
                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );

                    img.sprite = sprite;
                    cache.Store(cardName, sprite);
                })
            );
        }
    }
}
