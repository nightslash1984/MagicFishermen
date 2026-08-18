using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the deck assignment screen used by the Play menu.  Selections are
/// stored by deck name, so changing screens does not clear a match setup.
/// </summary>
public class PlayDeckSelector : MonoBehaviour
{
    private const string SavedDecksKey = "magic-fishermen.saved-decks.v1";
    private const string MatchDecksKey = "magic-fishermen.match-deck-slots.v1";
    private static readonly string[] SlotLabels = { "Player", "AI 1", "AI 2", "AI 3" };

    [Serializable]
    private class SavedDeck
    {
        public string name;
        public string deckText;
        public string commanderName;
    }

    [Serializable]
    private class SavedDeckCollection { public List<SavedDeck> decks = new(); }

    [Serializable]
    private class MatchDeckSlots { public string[] deckNames = new string[4]; }

    [SerializeField] private TMP_Text[] slotTexts = new TMP_Text[4];
    [SerializeField] private Button[] slotButtons = new Button[4];
    private readonly List<SavedDeck> savedDecks = new();
    private MatchDeckSlots selections = new();
    [SerializeField] private GameObject picker;
    [SerializeField] private Transform pickerContent;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button backButton;
    private Sprite uiSprite;

    private void Awake()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.GetComponent<GameUITheme>() == null)
            canvas.gameObject.AddComponent<GameUITheme>();
        BuildScreen();
        EnsureBackButton();
        EnsureSlotArtwork();
        BindButtons();
        ApplyScreenLayout();
    }

    private void OnEnable()
    {
        LoadDecks();
        LoadSelections();
        RefreshSlots();
    }

    public void BuildScreen()
    {
        RectTransform root = transform as RectTransform;
        if (root == null || root.childCount > 0) return;
        uiSprite = GetComponent<Image>() != null ? GetComponent<Image>().sprite : null;

        CreateText("Title", transform, "Choose Decks", 56, TextAlignmentOptions.Center, 76);
        CreateText("Subtitle", transform, "Select a saved deck for each seat", 28, TextAlignmentOptions.Center, 42);

        GameObject grid = new GameObject("Deck Slots", typeof(RectTransform), typeof(GridLayoutGroup));
        grid.transform.SetParent(transform, false);
        LayoutElement gridLayout = grid.AddComponent<LayoutElement>();
        gridLayout.preferredHeight = 550;
        GridLayoutGroup gridGroup = grid.GetComponent<GridLayoutGroup>();
        gridGroup.cellSize = new Vector2(700, 245);
        gridGroup.spacing = new Vector2(28, 28);
        gridGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridGroup.constraintCount = 2;
        gridGroup.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < SlotLabels.Length; i++)
            CreateSlot(grid.transform, i);

        CreateText("Hint", transform, "Click a box to browse your saved decks.", 24, TextAlignmentOptions.Center, 36);
        BuildPicker();
    }

    private void CreateSlot(Transform parent, int index)
    {
        GameObject slot = new GameObject(SlotLabels[index] + " Deck", typeof(RectTransform), typeof(Image), typeof(Button));
        slot.transform.SetParent(parent, false);
        Image background = slot.GetComponent<Image>();
        background.sprite = uiSprite;
        background.type = Image.Type.Sliced;
        background.color = index == 0 ? new Color(0.12f, 0.28f, 0.24f, 1f) : new Color(0.13f, 0.17f, 0.29f, 1f);
        Outline outline = slot.AddComponent<Outline>();
        outline.effectColor = new Color(0.96f, 0.73f, 0.26f, 0.8f);
        outline.effectDistance = new Vector2(3f, -3f);

        Button button = slot.GetComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.83f, 0.38f, 1f);
        colors.pressedColor = new Color(0.75f, 0.55f, 0.18f, 1f);
        button.colors = colors;
        slotButtons[index] = button;
        TMP_Text label = CreateText("Label", slot.transform, SlotLabels[index], 34, TextAlignmentOptions.Center, 54);
        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = new Vector2(0.06f, 0.64f);
        labelRect.anchorMax = new Vector2(0.94f, 0.94f);
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        label.fontStyle = FontStyles.Bold;

        TMP_Text selection = CreateText("Selected Deck", slot.transform, "No deck selected\n<size=62%>Click to choose</size>", 31, TextAlignmentOptions.Center, 100);
        RectTransform selectionRect = (RectTransform)selection.transform;
        selectionRect.anchorMin = new Vector2(0.08f, 0.08f);
        selectionRect.anchorMax = new Vector2(0.92f, 0.66f);
        selectionRect.offsetMin = selectionRect.offsetMax = Vector2.zero;
        selection.color = new Color(0.94f, 0.94f, 0.94f, 1f);
        slotTexts[index] = selection;
        AddCommanderArt(slot.transform, null, new Vector2(0.04f, 0.08f), new Vector2(0.25f, 0.58f));
    }

    private void BuildPicker()
    {
        picker = new GameObject("Deck Picker", typeof(RectTransform), typeof(Image));
        picker.transform.SetParent(transform, false);
        RectTransform pickerRect = picker.GetComponent<RectTransform>();
        pickerRect.anchorMin = pickerRect.anchorMax = new Vector2(0.5f, 0.5f);
        pickerRect.sizeDelta = new Vector2(950, 700);
        pickerRect.anchoredPosition = Vector2.zero;
        LayoutElement pickerLayout = picker.AddComponent<LayoutElement>();
        pickerLayout.ignoreLayout = true;
        Image pickerBackground = picker.GetComponent<Image>();
        pickerBackground.sprite = uiSprite;
        pickerBackground.type = Image.Type.Sliced;
        pickerBackground.color = new Color(0.06f, 0.07f, 0.11f, 0.98f);
        Outline pickerOutline = picker.AddComponent<Outline>();
        pickerOutline.effectColor = new Color(0.96f, 0.73f, 0.26f, 0.9f);
        pickerOutline.effectDistance = new Vector2(4f, -4f);

        VerticalLayoutGroup layout = picker.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(42, 42, 36, 36);
        layout.spacing = 16;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;

        CreateText("Picker Title", picker.transform, "Choose a deck", 42, TextAlignmentOptions.Center, 62);
        GameObject list = new GameObject("Deck List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        list.transform.SetParent(picker.transform, false);
        LayoutElement listElement = list.AddComponent<LayoutElement>();
        listElement.flexibleHeight = 1;
        VerticalLayoutGroup listLayout = list.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 10;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = false;
        listLayout.childForceExpandWidth = true;
        ContentSizeFitter fitter = list.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        pickerContent = list.transform;

        cancelButton = CreateButton("Cancel", picker.transform, "Cancel", 28, 58);
        picker.SetActive(false);
    }

    private void OpenPicker(int slotIndex)
    {
        LoadDecks();
        ClearPickerOptions();

        if (savedDecks.Count == 0)
        {
            CreateText("Empty", pickerContent, "No saved decks yet. Create and save a deck first.", 26, TextAlignmentOptions.Center, 70);
        }
        else
        {
            foreach (SavedDeck deck in savedDecks)
            {
                string deckName = deck.name;
                Button option = CreateButton(deckName, pickerContent, deckName, 30, 82);
                AddCommanderArt(option.transform, GetCommanderName(deck), new Vector2(0.02f, 0.1f), new Vector2(0.17f, 0.9f));
                SetButtonTextRegion(option, new Vector2(0.2f, 0f), Vector2.one);
                option.onClick.AddListener(() => SelectDeck(slotIndex, deckName));
            }
        }
        picker.SetActive(true);
        picker.transform.SetAsLastSibling();
    }

    private void SelectDeck(int slotIndex, string deckName)
    {
        selections.deckNames[slotIndex] = deckName;
        PlayerPrefs.SetString(MatchDecksKey, JsonUtility.ToJson(selections));
        PlayerPrefs.Save();
        RefreshSlots();
        picker.SetActive(false);
    }

    private void LoadDecks()
    {
        SavedDeckCollection library = JsonUtility.FromJson<SavedDeckCollection>(PlayerPrefs.GetString(SavedDecksKey, string.Empty)) ?? new SavedDeckCollection();
        savedDecks.Clear();
        if (library.decks != null) savedDecks.AddRange(library.decks);
    }

    private void LoadSelections()
    {
        selections = JsonUtility.FromJson<MatchDeckSlots>(PlayerPrefs.GetString(MatchDecksKey, string.Empty)) ?? new MatchDeckSlots();
        if (selections.deckNames == null || selections.deckNames.Length != SlotLabels.Length)
            selections.deckNames = new string[SlotLabels.Length];
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < slotTexts.Length; i++)
        {
            if (slotTexts[i] == null) continue;
            string selected = selections.deckNames[i];
            slotTexts[i].text = string.IsNullOrWhiteSpace(selected)
                ? "No deck selected\n<size=62%>Click to choose</size>"
                : selected;
            Image art = slotButtons[i] != null ? slotButtons[i].transform.Find("Commander Art")?.GetComponent<Image>() : null;
            if (art != null) art.gameObject.SetActive(!string.IsNullOrWhiteSpace(selected));
            SavedDeck deck = savedDecks.Find(candidate => candidate.name == selected);
            if (art != null && deck != null)
                StartCoroutine(LoadCommanderArt(art, GetCommanderName(deck)));
        }
    }

    private void ClearPickerOptions()
    {
        for (int i = pickerContent.childCount - 1; i >= 0; i--)
            Destroy(pickerContent.GetChild(i).gameObject);
    }

    private void BindButtons()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] == null) continue;
            int slotIndex = i;
            slotButtons[i].onClick.RemoveAllListeners();
            slotButtons[i].onClick.AddListener(() => OpenPicker(slotIndex));
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => picker.SetActive(false));
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(ReturnToMainMenu);
        }
    }

    private void ApplyScreenLayout()
    {
        VerticalLayoutGroup rootLayout = GetComponent<VerticalLayoutGroup>();
        if (rootLayout != null) rootLayout.enabled = false;

        SetRegion("Title", new Vector2(0.1f, 0.83f), new Vector2(0.9f, 0.94f));
        SetRegion("Subtitle", new Vector2(0.1f, 0.77f), new Vector2(0.9f, 0.83f));
        SetRegion("Deck Slots", new Vector2(0.1f, 0.25f), new Vector2(0.9f, 0.73f));
        SetRegion("Hint", new Vector2(0.1f, 0.17f), new Vector2(0.9f, 0.23f));

        Transform grid = transform.Find("Deck Slots");
        if (grid != null)
        {
            LayoutElement gridElement = grid.GetComponent<LayoutElement>();
            if (gridElement != null) gridElement.ignoreLayout = true;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)grid);
        }
    }

    private void SetRegion(string childName, Vector2 anchorMin, Vector2 anchorMax)
    {
        Transform child = transform.Find(childName);
        if (child is not RectTransform rect) return;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void EnsureBackButton()
    {
        if (backButton == null)
        {
            Transform existing = transform.Find("Back Button");
            backButton = existing != null ? existing.GetComponent<Button>() : CreateButton("Back Button", transform, "Back", 28, 58);
        }

        if (backButton == null) return;
        LayoutElement layout = backButton.GetComponent<LayoutElement>();
        if (layout != null) layout.ignoreLayout = true;
        RectTransform rect = (RectTransform)backButton.transform;
        rect.anchorMin = new Vector2(0.03f, 0.9f);
        rect.anchorMax = new Vector2(0.14f, 0.96f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void EnsureSlotArtwork()
    {
        for (int i = 0; i < slotButtons.Length; i++)
            if (slotButtons[i] != null)
                AddCommanderArt(slotButtons[i].transform, null, new Vector2(0.04f, 0.08f), new Vector2(0.25f, 0.58f));
    }

    private void ReturnToMainMenu()
    {
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate.name != "MainMenu" || candidate.GetComponentInParent<Canvas>() == null) continue;
            if (picker != null) picker.SetActive(false);
            candidate.gameObject.SetActive(true);
            gameObject.SetActive(false);
            return;
        }
    }

    private static string GetCommanderName(SavedDeck deck)
    {
        if (!string.IsNullOrWhiteSpace(deck.commanderName)) return deck.commanderName;
        ParsedDeck parsed = DeckParser.Parse(deck.deckText);
        return parsed.commanders.Count > 0 ? parsed.commanders[0].name : null;
    }

    private void AddCommanderArt(Transform parent, string commanderName, Vector2 anchorMin, Vector2 anchorMax)
    {
        Transform existing = parent.Find("Commander Art");
        Image art = existing != null ? existing.GetComponent<Image>() : new GameObject("Commander Art", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        if (existing == null) art.transform.SetParent(parent, false);
        art.raycastTarget = false;
        art.preserveAspect = true;
        RectTransform rect = (RectTransform)art.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        art.gameObject.SetActive(!string.IsNullOrWhiteSpace(commanderName));
        if (!string.IsNullOrWhiteSpace(commanderName)) StartCoroutine(LoadCommanderArt(art, commanderName));
    }

    private void SetButtonTextRegion(Button button, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (button.transform.Find("Text") is not RectTransform rect) return;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private System.Collections.IEnumerator LoadCommanderArt(Image target, string commanderName)
    {
        DeckInputHandler input = FindFirstObjectByType<DeckInputHandler>(FindObjectsInactive.Include);
        DeckDisplay display = input != null ? input.deckDisplay : null;
        if (target == null || display == null || string.IsNullOrWhiteSpace(commanderName)) yield break;
        if (display.cache != null && display.cache.TryGet(commanderName, out Texture2D cached, out _))
        {
            SetCommanderSprite(target, cached);
            yield break;
        }

        bool loaded = false;
        Texture2D texture = null;
        ScryfallCard cardData = null;
        yield return StartCoroutine(display.scryfall.GetCardData(commanderName, (success, data, image) =>
        {
            loaded = success;
            cardData = data;
            texture = image;
        }));
        if (!loaded || texture == null || target == null) yield break;
        display.cache?.Store(commanderName, texture, cardData);
        SetCommanderSprite(target, texture);
    }

    private static void SetCommanderSprite(Image target, Texture2D texture)
    {
        target.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        target.color = Color.white;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment, float height)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        layout.preferredHeight = height;
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    private Button CreateButton(string name, Transform parent, string text, float fontSize, float height)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = height;
        Image image = buttonObject.GetComponent<Image>();
        image.sprite = uiSprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.2f, 0.22f, 0.32f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        TMP_Text label = CreateText("Text", buttonObject.transform, text, fontSize, TextAlignmentOptions.Center, height);
        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        label.GetComponent<LayoutElement>().ignoreLayout = true;
        return button;
    }

    /// <summary>Used only by the editor scene builder to replace a previous generated layout.</summary>
    public void RebuildScreen()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
        if (layout != null)
            DestroyImmediate(layout);

        slotTexts = new TMP_Text[4];
        slotButtons = new Button[4];
        picker = null;
        pickerContent = null;
        cancelButton = null;
        BuildScreen();
        BindButtons();
        ApplyScreenLayout();
    }
}
