using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stores named deck lists on this device and builds their buttons in a ScrollRect content object.
/// Attach to a persistent UI object, then assign the fields in the Inspector.
/// </summary>
public class DeckLibraryManager : MonoBehaviour
{
    private const string SavedDecksKey = "magic-fishermen.saved-decks.v1";

    [Header("Required references")]
    [SerializeField] private DeckInputHandler deckInput;
    [SerializeField] private Transform deckListContent;
    [SerializeField] private TMP_InputField deckNameInput;

    [Header("Optional panels")]
    [SerializeField] private GameObject deckListMenu;
    [SerializeField] private GameObject createDeckMenu;
    [SerializeField] private GameObject nameDeckMenu;
    [SerializeField] private TMP_Text messageText;

    [Serializable]
    private class SavedDeck
    {
        public string name;
        [TextArea] public string deckText;
        public string commanderName;
        public string companionName;
    }

    [Serializable]
    private class SavedDeckCollection
    {
        public List<SavedDeck> decks = new();
    }

    private SavedDeckCollection savedDecks = new();
    private string loadedDeckName;
    private Image selectedDeckTile;

    private void Awake()
    {
        LoadLibrary();
    }

    private void OnEnable()
    {
        RefreshDeckList();
    }

    /// <summary>Assign to the Decks navigation button.</summary>
    public void ShowDeckList()
    {
        deckInput?.deckDisplay.SetCardInteractionsEnabled(false);
        LoadLibrary();
        RefreshDeckList();
        if (deckListMenu != null) deckListMenu.SetActive(true);
        if (nameDeckMenu != null) nameDeckMenu.SetActive(false);
    }

    /// <summary>Assign to Create Deck. It opens the existing deck-entry screen elsewhere in the scene.</summary>
    public void BeginSaveCurrentDeck()
    {
        if (deckInput == null || string.IsNullOrWhiteSpace(deckInput.ActiveDeckText))
        {
            ShowMessage("Load a deck before saving it.");
            return;
        }

        if (deckNameInput != null)
        {
            deckNameInput.text = loadedDeckName ?? string.Empty;
            deckNameInput.Select();
            deckNameInput.ActivateInputField();
        }
        if (nameDeckMenu != null) nameDeckMenu.SetActive(true);
    }

    /// <summary>Assign to the Save button in NameDeckMenu.</summary>
    public void SaveCurrentDeck()
    {
        if (deckNameInput == null)
        {
            ShowMessage("Assign Deck Name Input on DeckLibraryManager.");
            return;
        }

        string name = deckNameInput.text.Trim();
        string deckText = deckInput != null ? deckInput.ActiveDeckText : string.Empty;
        string commanderName = GetCurrentCommanderName(deckText);
        string companionName = GetCurrentCompanionName();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(deckText))
        {
            ShowMessage("Enter a deck name and load a deck first.");
            return;
        }

        SavedDeck existing = savedDecks.decks.Find(deck =>
            string.Equals(deck.name, name, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
            savedDecks.decks.Add(new SavedDeck
            {
                name = name,
                deckText = deckText,
                commanderName = commanderName,
                companionName = companionName
            });
        else
        {
            existing.name = name;
            existing.deckText = deckText;
            existing.commanderName = commanderName;
            existing.companionName = companionName;
        }

        PersistLibrary();
        if (nameDeckMenu != null) nameDeckMenu.SetActive(false);
        if (createDeckMenu != null) createDeckMenu.SetActive(false);
        ShowDeckList();
        ShowMessage($"Saved {name}.");
    }

    /// <summary>Assign to a Cancel button in NameDeckMenu, if you add one.</summary>
    public void CancelSave()
    {
        if (nameDeckMenu != null) nameDeckMenu.SetActive(false);
    }

    /// <summary>Assign to Create Deck. Starts with no cards and no selected saved-deck name.</summary>
    public void CreateNewDeck()
    {
        loadedDeckName = null;
        deckInput?.deckDisplay.SetCardInteractionsEnabled(true);
        deckInput?.BeginNewDeck();
        if (nameDeckMenu != null) nameDeckMenu.SetActive(false);
        if (deckListMenu != null) deckListMenu.SetActive(false);
        if (createDeckMenu != null) createDeckMenu.SetActive(true);
        StartCoroutine(EnsureNewDeckIsEmpty());
    }

    private System.Collections.IEnumerator EnsureNewDeckIsEmpty()
    {
        // Run after the panels change state, so no activation callback can
        // repopulate the old input field or leave the prior card list visible.
        yield return null;
        deckInput?.BeginNewDeck();
    }

    /// <summary>Assign to Edit Deck. Keeps the selected deck name so Save updates it.</summary>
    public void EditLoadedDeck()
    {
        deckInput?.deckDisplay.SetCardInteractionsEnabled(true);
        deckInput?.SetDeckList();
    }

    public void DeleteDeck(string deckName)
    {
        savedDecks.decks.RemoveAll(deck => deck.name == deckName);
        PersistLibrary();
        RefreshDeckList();
    }

    /// <summary>Deletes the deck most recently selected from the library.</summary>
    public void DeleteLoadedDeck()
    {
        if (string.IsNullOrEmpty(loadedDeckName))
        {
            ShowMessage("Select a saved deck before deleting it.");
            return;
        }

        string deckName = loadedDeckName;
        loadedDeckName = null;
        DeleteDeck(deckName);
        ShowMessage($"Deleted {deckName}.");
    }

    private void LoadDeck(SavedDeck deck)
    {
        if (deckInput == null)
        {
            ShowMessage("Assign Deck Input on DeckLibraryManager.");
            return;
        }

        deckInput.LoadSavedDeck(deck.deckText);
        deckInput.deckDisplay.SetCardInteractionsEnabled(false);
        deckInput.deckDisplay.RestoreCommanderByName(GetCommanderName(deck));
        deckInput.deckDisplay.RestoreCompanionByName(deck.companionName);
        loadedDeckName = deck.name;
        ShowMessage($"Loaded {deck.name}.");
    }

    public void RefreshDeckList()
    {
        if (deckListContent == null) return;

        for (int i = deckListContent.childCount - 1; i >= 0; i--)
            Destroy(deckListContent.GetChild(i).gameObject);

        foreach (SavedDeck deck in savedDecks.decks)
            CreateDeckButton(deck);
    }

    private void CreateDeckButton(SavedDeck deck)
    {
        GameObject buttonObject = new GameObject(deck.name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(deckListContent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.18f, 1f);
        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.78f, 0.2f, 1f);
        outline.effectDistance = new Vector2(8f, -8f);
        outline.enabled = false;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => SelectAndLoadDeck(deck, image, outline));

        if (string.Equals(deck.name, loadedDeckName, StringComparison.OrdinalIgnoreCase))
            SetSelectedDeckTile(image, outline);

        GameObject artObject = new GameObject("Commander Art", typeof(RectTransform), typeof(Image));
        artObject.transform.SetParent(buttonObject.transform, false);
        Image commanderArt = artObject.GetComponent<Image>();
        commanderArt.preserveAspect = true;
        RectTransform artRect = artObject.GetComponent<RectTransform>();
        artRect.anchorMin = new Vector2(0.08f, 0.2f);
        artRect.anchorMax = new Vector2(0.92f, 0.98f);
        artRect.offsetMin = Vector2.zero;
        artRect.offsetMax = Vector2.zero;
        StartCoroutine(LoadCommanderArt(deck, commanderArt));

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = $"<size=65%>{GetCommanderName(deck) ?? "No commander"}</size>\n{deck.name}";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 42;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.Normal;
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.anchorMax = new Vector2(1f, 0.2f);
        labelRect.offsetMin = new Vector2(20, 10);
        labelRect.offsetMax = new Vector2(-20, -10);
    }

    private void SelectAndLoadDeck(SavedDeck deck, Image image, Outline outline)
    {
        SetSelectedDeckTile(image, outline);
        LoadDeck(deck);
    }

    private void SetSelectedDeckTile(Image image, Outline outline)
    {
        if (selectedDeckTile != null)
        {
            selectedDeckTile.color = new Color(0.15f, 0.15f, 0.18f, 1f);
            Outline previousOutline = selectedDeckTile.GetComponent<Outline>();
            if (previousOutline != null) previousOutline.enabled = false;
        }

        selectedDeckTile = image;
        selectedDeckTile.color = new Color(0.29f, 0.22f, 0.08f, 1f);
        outline.enabled = true;
    }

    private string GetCurrentCommanderName(string deckText)
    {
        string selectedCommander = deckInput != null && deckInput.deckDisplay != null
            ? deckInput.deckDisplay.PrimaryCommanderName
            : null;
        if (!string.IsNullOrWhiteSpace(selectedCommander))
            return selectedCommander;

        ParsedDeck parsed = DeckParser.Parse(deckText);
        return parsed.commanders.Count > 0 ? parsed.commanders[0].name : null;
    }

    private static string GetCommanderName(SavedDeck deck)
    {
        if (!string.IsNullOrWhiteSpace(deck.commanderName))
            return deck.commanderName;

        ParsedDeck parsed = DeckParser.Parse(deck.deckText);
        return parsed.commanders.Count > 0 ? parsed.commanders[0].name : null;
    }

    private string GetCurrentCompanionName()
    {
        return deckInput != null && deckInput.deckDisplay != null
            ? deckInput.deckDisplay.CompanionName
            : null;
    }

    private System.Collections.IEnumerator LoadCommanderArt(SavedDeck deck, Image targetImage)
    {
        string commanderName = GetCommanderName(deck);
        if (string.IsNullOrWhiteSpace(commanderName) || deckInput?.deckDisplay == null)
            yield break;

        CardImageCache cache = deckInput.deckDisplay.cache;
        if (cache != null && cache.TryGet(commanderName, out Texture2D cachedTexture, out _))
        {
            SetCommanderSprite(targetImage, cachedTexture);
            yield break;
        }

        ScryfallCard cardData = null;
        Texture2D texture = null;
        bool success = false;
        yield return StartCoroutine(deckInput.deckDisplay.scryfall.GetCardData(commanderName, (loaded, data, image) =>
        {
            success = loaded;
            cardData = data;
            texture = image;
        }));

        if (!success || texture == null || targetImage == null)
            yield break;

        cache?.Store(commanderName, texture, cardData);
        SetCommanderSprite(targetImage, texture);
    }

    private static void SetCommanderSprite(Image targetImage, Texture2D texture)
    {
        if (targetImage == null || texture == null)
            return;

        targetImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        targetImage.color = Color.white;
    }

    private static int CountCards(string deckText)
    {
        int total = 0;
        ParsedDeck parsed = DeckParser.Parse(deckText);
        foreach (var entry in parsed.mainboard) total += entry.count;
        foreach (var entry in parsed.sideboard) total += entry.count;
        foreach (var entry in parsed.commanders) total += entry.count;
        return total;
    }

    private void LoadLibrary()
    {
        string json = PlayerPrefs.GetString(SavedDecksKey, string.Empty);
        savedDecks = string.IsNullOrEmpty(json)
            ? new SavedDeckCollection()
            : JsonUtility.FromJson<SavedDeckCollection>(json) ?? new SavedDeckCollection();
    }

    private void PersistLibrary()
    {
        PlayerPrefs.SetString(SavedDecksKey, JsonUtility.ToJson(savedDecks));
        PlayerPrefs.Save();
    }

    private void ShowMessage(string message)
    {
        Debug.Log(message);
        if (messageText != null) messageText.text = message;
    }
}
