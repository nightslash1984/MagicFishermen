using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CardUI : MonoBehaviour, IPointerClickHandler
{
    public string cardName;
    public Image image;

    private DeckDisplay deckDisplay;

    public void Init(string name, Sprite sprite, DeckDisplay display)
    {
        cardName = name;
        image.sprite = sprite;
        deckDisplay = display;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        deckDisplay.SelectCard(this);
    }

    public void SetSelected(bool selected)
    {
        // simple visual highlight
        image.color = selected ? Color.yellow : Color.white;
    }
}