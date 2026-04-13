using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor.Experimental.GraphView;

public class CardUI : MonoBehaviour, IPointerClickHandler
{
    public ScryfallCard cardData;
    public string cardName;
    public Image image;

    private DeckDisplay deckDisplay;
    public bool isCommander = false;

    private void Awake()
    {
        if (image == null)
            image = GetComponent<Image>();
    }

    void Update()
    {
        if (isCommander)
        {
            image.color = new Color(0.5f, 1f, 0.5f); // light green
        }
    }

    public void Init(string name, Sprite sprite, DeckDisplay display, ScryfallCard data)
    {
        cardName = name;
        deckDisplay = display;
        image.sprite = sprite;
        cardData = data;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // LEFT CLICK → select (existing behavior)
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            deckDisplay.SelectCard(this);
        }
        // RIGHT CLICK → set commander
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (!isCommander)
                deckDisplay.SetCommander(this);
            else
            {
                isCommander = false;
                deckDisplay.RemoveCommander(this);
                image.color = Color.white;
            }
        }
    }

    public void SetSelected(bool selected)
    {
        if (!isCommander)
            image.color = selected ? Color.yellow : Color.white;
    }

    public void SetCommander(bool value)
    {
        isCommander = value;

        if (isCommander)
        {
            // Commander highlight
            image.color = new Color(0.5f, 1f, 0.5f); // light green
        }
        else
        {
            image.color = Color.white;
        }
    }
}