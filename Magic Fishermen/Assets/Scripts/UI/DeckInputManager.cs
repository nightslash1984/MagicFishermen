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

        // Display
        deckDisplay.DisplayDeck(cards);
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
}