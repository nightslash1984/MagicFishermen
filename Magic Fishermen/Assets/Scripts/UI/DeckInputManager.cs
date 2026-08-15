using UnityEngine;
using System.Collections.Generic;
using TMPro; // if using TextMeshPro

public class DeckInputHandler : MonoBehaviour
{
    public TMP_InputField inputField; // or InputField if not TMP
    public DeckDisplay deckDisplay;

    // Keep the original import text so saved decks retain section headers,
    // quantities, sideboards, and commander entries.
    private string activeDeckText;

    public string ActiveDeckText => string.IsNullOrWhiteSpace(activeDeckText)
        ? inputField.text
        : activeDeckText;

    public void OnLoadDeckClicked()
    {
        string text = inputField.text;

        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("Deck input is empty!");
            return;
        }

        LoadDeckText(text, deckDisplay.currentDeck.Count == 0);
    }

    /// <summary>Loads a deck list, optionally replacing the deck already on screen.</summary>
    public void LoadDeckText(string text, bool replaceExisting)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("Deck input is empty!");
            return;
        }

        activeDeckText = text;
        inputField.text = text;

        if (replaceExisting)
            deckDisplay.ClearDeck();

        ParsedDeck parsed = DeckParser.Parse(text);

        // Expand into individual cards
        List<string> cards = ExpandDeck(parsed);

        List<string> sideboard = ExpandEntries(parsed.sideboard);
        if (deckDisplay.currentDeck.Count == 0)
        {
            deckDisplay.SetSideboardCards(sideboard);
            deckDisplay.DisplayDeck(cards);
        }
        else
        {
            deckDisplay.AddSideboardCards(sideboard);
            deckDisplay.AddCards(GetNewCopies(cards, deckDisplay.currentDeck));
        }
    }

    /// <summary>Public entry point used by the saved-deck menu.</summary>
    public void LoadSavedDeck(string text)
    {
        LoadDeckText(text, true);
    }

    private List<string> ExpandDeck(ParsedDeck deck)
    {
        List<string> cards = new();

        void AddRange(List<(string name, int count)> list)
        {
            foreach (var (name, count) in list)
            {
                for (int i = 0; i < count; i++)
                    cards.Add(name);
            }
        }

        AddRange(deck.mainboard);
        AddRange(deck.sideboard);
        AddRange(deck.commanders);

        return cards;
    }

    private List<string> ExpandEntries(List<(string name, int count)> entries)
    {
        List<string> cards = new();
        foreach (var (name, count) in entries)
        {
            for (int i = 0; i < count; i++)
                cards.Add(name);
        }
        return cards;
    }

    private List<string> GetNewCopies(List<string> desiredCards, List<string> existingCards)
    {
        var existingCounts = new Dictionary<string, int>();
        foreach (string cardName in existingCards)
        {
            string key = CardImageCache.NormalizeName(cardName);
            existingCounts[key] = existingCounts.TryGetValue(key, out int count) ? count + 1 : 1;
        }

        var additions = new List<string>();
        foreach (string cardName in desiredCards)
        {
            string key = CardImageCache.NormalizeName(cardName);
            if (existingCounts.TryGetValue(key, out int count) && count > 0)
            {
                existingCounts[key] = count - 1;
            }
            else
            {
                additions.Add(cardName);
            }
        }
        return additions;
    }
    public void SetDeckList()
    {
        Debug.Log("Setting deck list in input field...");
        if (!string.IsNullOrWhiteSpace(activeDeckText))
        {
            inputField.text = activeDeckText;
            return;
        }

        inputField.text = "";
        foreach (string card in deckDisplay.currentDeck)
        {
            inputField.text += card + "\n";
        }
    }

    /// <summary>Resets both the on-screen deck and its saved import text for a new deck.</summary>
    public void BeginNewDeck()
    {
        activeDeckText = string.Empty;
        inputField.text = string.Empty;
        deckDisplay.ClearDeck();
    }
}
