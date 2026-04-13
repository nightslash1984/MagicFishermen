using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class DeckDisplay : MonoBehaviour
{
    public Transform content;
    public GameObject cardPrefab;

    public ScryfallClient scryfall;
    public CardImageCache cache;

    public List<string> currentDeck = new();
    private List<CardUI> selectedCards = new List<CardUI>();
    private List<CardUI> commanders = new List<CardUI>();
    private CardUI background;
    private CardUI companion;

    public HashSet<string> CommanderColors = new();

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void RemoveCommander(CardUI card)
    {
        commanders.Remove(card);
    }

    public void SetCommander(CardUI card)
    {
        var data = card.cardData;

        // 🔹 Companion logic
        if (IsCompanion(data))
        {
            if (companion != null)
                companion.SetCommander(false);

            companion = card;
            companion.SetCommander(true);

            Debug.Log("Companion set: " + card.cardName);
            return;
        }

        // 🔹 Background logic (already implemented)
        if (IsBackground(data))
        {
            if (commanders.Count == 0 || !CanChooseBackground(commanders[0].cardData))
            {
                Debug.LogWarning("Commander cannot use background");
                return;
            }

            if (background != null)
                background.SetCommander(false);

            background = card;
            background.SetCommander(true);

            UpdateCommanderColors();
            return;
        }

        // 🔹 Normal commander logic

        if (!IsLegalCommander(data))
        {
            Debug.LogWarning("Invalid commander: " + card.cardName);
            return;
        }

        // If already 2 commanders → reset
        if (commanders.Count >= 2)
        {
            foreach (var cmd in commanders)
                cmd.SetCommander(false);

            commanders.Clear();
        }

        // Check partner compatibility
        if (commanders.Count == 1)
        {
            var existing = commanders[0];

            bool validPair =
                HasPartner(existing.cardData) && HasPartner(data)
                || GetPartnerWithName(existing.cardData) == card.cardName.ToLower()
                || GetPartnerWithName(data) == existing.cardName.ToLower();

            if (!validPair)
            {
                Debug.LogWarning("These commanders cannot partner");
                return;
            }
        }

        commanders.Add(card);
        card.SetCommander(true);

        UpdateCommanderColors();
        ValidateDeck();
        if (ValidateCompanion())
        {
            Debug.LogWarning("Invalid companion");
        }

        card.transform.SetAsFirstSibling();
    }

    public bool ValidateCompanion()
    {
        if (companion == null) return true;

        string text = companion.cardData.oracle_text.ToLower();

        // Example: Lurrus rule
        if (text.Contains("each permanent card in your starting deck has mana value 2 or less"))
        {
            foreach (Transform child in content)
            {
                CardUI card = child.GetComponent<CardUI>();

                if (card.cardData == null) continue;

                int manaValue = card.cardData.cmc;

                if (manaValue > 2)
                    return false;
            }
        }

        return true;
    }

    public static bool HasPartner(ScryfallCard card)
    {
        if (card?.oracle_text == null) return false;

        string text = card.oracle_text.ToLower();

        return text.Contains("partner")
            || text.Contains("friends forever");
    }

    public static bool IsCompanion(ScryfallCard card)
    {
        if (card?.oracle_text == null) return false;

        return card.oracle_text.ToLower().Contains("companion");
    }

    public static string GetPartnerWithName(ScryfallCard card)
    {
        if (card?.oracle_text == null) return null;

        string text = card.oracle_text.ToLower();

        if (!text.Contains("partner with"))
            return null;

        int start = text.IndexOf("partner with") + "partner with".Length;
        string name = text.Substring(start).Trim();

        // crude parse until punctuation
        int end = name.IndexOfAny(new char[] { ',', '.', '\n' });
        if (end > 0)
            name = name.Substring(0, end);

        return name.Trim();
    }

    void UpdateCommanderColors()
    {
        CommanderColors.Clear();

        foreach (var cmd in commanders)
            CommanderColors.UnionWith(GetColorIdentity(cmd.cardData));

        if (background != null)
            CommanderColors.UnionWith(GetColorIdentity(background.cardData));
    }

    public void ValidateDeck()
    {
        foreach (Transform child in content)
        {
            CardUI card = child.GetComponent<CardUI>();

            if (card == null || card.cardData == null)
                continue;

            bool valid = IsWithinColorIdentity(card.cardData, CommanderColors);

            // Highlight invalid cards
            if (!valid)
            {
                card.image.color = Color.red;
            }
            else
            {
                card.image.color = Color.white;
            }
        }
    }
    public static bool IsBackground(ScryfallCard card)
    {
        if (card == null) return false;

        return card.type_line != null &&
               card.type_line.ToLower().Contains("background");
    }

    public static bool CanChooseBackground(ScryfallCard card)
    {
        if (card == null) return false;

        return card.oracle_text != null &&
               card.oracle_text.ToLower().Contains("choose a background");
    }

    public static HashSet<string> GetColorIdentity(ScryfallCard card)
    {
        if (card?.color_identity == null)
            return new HashSet<string>();

        return new HashSet<string>(card.color_identity);
    }

    public static bool IsWithinColorIdentity(ScryfallCard card, HashSet<string> commanderColors)
    {
        var cardColors = GetColorIdentity(card);

        return cardColors.All(color => commanderColors.Contains(color));
    }

    public void DisplayDeck(List<string> cardNames)
    {
        currentDeck = new List<string>(cardNames);
        StartCoroutine(BuildList());
    }

    IEnumerator BuildList()
    {
        // Clear existing cards
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

        // Load cards one at a time
        foreach (string cardName in currentDeck)
        {
            yield return StartCoroutine(LoadCard(cardName));
            yield return new WaitForSeconds(0.1f); // small delay to avoid hitting rate limits
        }
    }

    IEnumerator LoadCard(string cardName)
    {
        // try cache first
        if (cache != null && cache.TryGet(cardName, out Texture2D cachedTex, out ScryfallCard cachedData))
        {
            Sprite cachedSprite = Sprite.Create(
                cachedTex,
                new Rect(0, 0, cachedTex.width, cachedTex.height),
                new Vector2(0.5f, 0.5f)
            );

            GameObject cachedObj = Instantiate(cardPrefab, content);
            cachedObj.GetComponent<CardUI>().Init(cardName, cachedSprite, this, cachedData);
            yield break;
        }

        const int maxRetries = 3;
        int attempt = 0;
        bool success = false;
        ScryfallCard cardData = null;
        Texture2D texture = null;

        while (!success && attempt < maxRetries)
        {
            attempt++;
            yield return StartCoroutine(
                scryfall.GetCardData(cardName, (s, data, tex) =>
                {
                    success = s;
                    cardData = data;
                    texture = tex;
                })
            );

            if (!success)
            {
                Debug.LogWarning($"Retry {attempt} for {cardName}");
                yield return new WaitForSeconds(0.2f);
            }
        }

        if (!success || cardData == null || texture == null)
        {
            Debug.LogWarning("Skipping: " + cardName);
            yield break;
        }

        // store in cache
        cache?.Store(cardName, texture, cardData);

        GameObject createdObj = Instantiate(cardPrefab, content);
        CardUI cardUI = createdObj.GetComponent<CardUI>();

        Sprite loadedSprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );

        cardUI.Init(cardName, loadedSprite, this, cardData);
    }


    // 🔽 NEW: selection
    public void SelectCard(CardUI card)
    {
        if (selectedCards.Contains(card))
        {
            UnselectCard(card);
            return;
        }
        selectedCards.Add(card);
        card.SetSelected(true);
    }
    public static bool IsLegalCommander(ScryfallCard card)
    {
        if (card == null) return false;

        string type = card.type_line?.ToLower() ?? "";
        string text = card.oracle_text?.ToLower() ?? "";

        // ✅ Legendary Creature
        if (type.Contains("legendary") && type.Contains("creature"))
            return true;

        // ✅ Special rules text
        if (text.Contains("can be your commander"))
            return true;

        return false;
    }

    public void UnselectCard(CardUI card)
    {
        selectedCards.Remove(card);
        card.SetSelected(false);
    }

    // 🔽 NEW: removal
    public void RemoveSelectedCards()
    {
        foreach (var card in selectedCards)
        {
            currentDeck.Remove(card.cardName);

            Destroy(card.gameObject);
        }
        selectedCards.Clear();
    }

    public void ClearDeck()
    {
        currentDeck.Clear();
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }
    }
}