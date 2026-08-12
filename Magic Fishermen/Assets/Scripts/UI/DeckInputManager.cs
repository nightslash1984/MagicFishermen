using UnityEngine;
using System.Collections.Generic;
using TMPro; // if using TextMeshPro

public class DeckInputHandler : MonoBehaviour
{
    public TMP_InputField inputField; // or InputField if not TMP
    public DeckDisplay deckDisplay;

    public void OnLoadDeckClicked()
    {
        string text = inputField.text;

        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("Deck input is empty!");
            return;
        }

        // Parse deck
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
        inputField.text = "";
        foreach (string card in deckDisplay.currentDeck)
        {
            inputField.text += card + "\n";
        }
    }
}
