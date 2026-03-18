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

    private List<string> currentDeck = new();
    private CardUI selectedCard;

    public void DisplayDeck(List<string> cardNames)
    {
        currentDeck = new List<string>(cardNames);
        StartCoroutine(BuildList());
    }

    IEnumerator BuildList()
    {
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

        foreach (var cardName in currentDeck)
        {
            bool success = false;
            Texture2D loadedTexture = null;

            yield return StartCoroutine(
                scryfall.GetCardImage(cardName, (texture) =>
                {
                    success = true;
                    loadedTexture = texture;
                })
            );

            // 🚫 Skip invalid cards
            if (!success || loadedTexture == null)
            {
                Debug.LogWarning("Skipping invalid card: " + cardName);
                continue;
            }

            // ✅ Only instantiate if valid
            GameObject obj = Instantiate(cardPrefab, content);
            CardUI cardUI = obj.GetComponent<CardUI>();

            Sprite sprite = Sprite.Create(
                loadedTexture,
                new Rect(0, 0, loadedTexture.width, loadedTexture.height),
                new Vector2(0.5f, 0.5f)
            );

            cardUI.Init(cardName, sprite, this);
        }
    }

    // 🔽 NEW: selection
    public void SelectCard(CardUI card)
    {
        if (selectedCard != null)
            selectedCard.SetSelected(false);

        selectedCard = card;
        selectedCard.SetSelected(true);
    }

    // 🔽 NEW: removal
    public void RemoveSelectedCard()
    {
        if (selectedCard == null)
            return;

        currentDeck.Remove(selectedCard.cardName);

        Destroy(selectedCard.gameObject);
        selectedCard = null;
    }
}