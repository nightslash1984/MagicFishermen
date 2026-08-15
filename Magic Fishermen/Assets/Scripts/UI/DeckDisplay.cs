using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
using TMPro;

public class DeckDisplay : MonoBehaviour
{
    public Transform content;
    public GameObject cardPrefab;

    public ScryfallClient scryfall;
    public CardImageCache cache;

    public TMP_Text errorText;

    public List<string> currentDeck = new();
    private Coroutine buildDeckRoutine;
    private Coroutine restoreCommanderRoutine;
    private Coroutine restoreCompanionRoutine;
    private bool isBuildingDeck;
    private List<CardUI> selectedCards = new List<CardUI>();
    private List<CardUI> commanders = new List<CardUI>();
    private CardUI background;
    private CardUI companion;
    private readonly HashSet<string> sideboardCardNames = new();
    private bool cardInteractionsEnabled = true;

    public HashSet<string> CommanderColors = new();

    public string PrimaryCommanderName => commanders.Count > 0 ? commanders[0].cardName : null;
    public string CompanionName => companion != null ? companion.cardName : null;
    public bool CardInteractionsEnabled => cardInteractionsEnabled;

    public void SetCardInteractionsEnabled(bool enabled)
    {
        cardInteractionsEnabled = enabled;
    }

    private List<string> companionStrings = new() {
        "Your starting deck contains only cards with even mana values.",
        "No card in your starting deck has more than one of the same mana symbol in its mana cost.",
        "Each creature card in your starting deck is a Cat, Elemental, Nightmare, Dinosaur, or Beast card.",
        "Your starting deck contains only cards with mana value 3 or greater and land cards.",
        "Each permanent card in your starting deck has mana value 2 or less.",
        "Your starting deck contains only cards with odd mana values and land cards.",
        "Each nonland card in your starting deck shares a card type.",
        "Your starting deck contains at least twenty cards more than the minimum deck size.",
        "Each permanent card in your starting deck has an activated ability."
    };

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void RemoveCommander(CardUI card)
    {
        commanders.Remove(card);

        if (companion == card)
            companion = null;

        if (background == card)
            background = null;

        UpdateCommanderColors();
        RefreshValidation();
    }

    public void SetSideboardCards(IEnumerable<string> cardNames)
    {
        sideboardCardNames.Clear();
        foreach (string cardName in cardNames)
            sideboardCardNames.Add(CardImageCache.NormalizeName(cardName));
    }

    public void AddSideboardCards(IEnumerable<string> cardNames)
    {
        foreach (string cardName in cardNames)
            sideboardCardNames.Add(CardImageCache.NormalizeName(cardName));
    }

    private bool IsStartingDeckCard(CardUI card)
    {
        return card != null && card.gameObject.activeInHierarchy && card.cardData != null && card != companion &&
               !sideboardCardNames.Contains(CardImageCache.NormalizeName(card.cardName));
    }

    private int StartingDeckCardCount()
    {
        int count = 0;
        foreach (Transform child in content)
        {
            if (IsStartingDeckCard(child.GetComponent<CardUI>()))
                count++;
        }
        return count;
    }

    public void SetCommander(CardUI card)
    {
        if (card == null || card.cardData == null)
        {
            Debug.LogWarning("Cannot set a commander before its card data is loaded.");
            return;
        }

        var data = card.cardData;

        // 🔹 Companion logic
        if (IsCompanion(data) && commanders.Count > 0)
        {
            if (companion != null)
                companion.SetCommander(false);

            companion = card;
            companion.SetCommander(true);
            companion.transform.SetAsFirstSibling();

            Debug.Log("Companion set: " + card.cardName);
            RefreshValidation();
            return;
        }

        // 🔹 Background logic (already implemented)
        if (IsBackground(data))
        {
            if (commanders.Count == 0 || !CanChooseBackground(commanders[0].cardData))
            {
                Debug.LogWarning("Commander cannot use background");
            }

            if (background != null)
                background.SetCommander(false);

            background = card;
            background.SetCommander(true);

            UpdateCommanderColors();
            RefreshValidation();
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
                || string.Equals(GetPartnerWithName(existing.cardData), card.cardName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetPartnerWithName(data), existing.cardName, StringComparison.OrdinalIgnoreCase);

            if (!validPair)
            {
                Debug.LogWarning("These commanders cannot partner");
                return;
            }
        }


        if (companion != card)
        {
            card.SetCommander(true);
            commanders.Add(card);
        }

        UpdateCommanderColors();
        RefreshValidation();

        card.transform.SetAsFirstSibling();
    }

    public bool ValidateCompanion()
    {
        if (companion?.cardData?.oracle_text == null)
            return true;

        bool valid = IsWithinColorIdentity(companion.cardData, CommanderColors);
        HashSet<CardUI> violations = GetCompanionViolations();
        foreach (CardUI card in violations)
            card.image.color = Color.red;

        return valid && violations.Count == 0;
    }

    private HashSet<CardUI> GetCompanionViolations()
    {
        var violations = new HashSet<CardUI>();
        if (companion?.cardData?.oracle_text == null)
            return violations;

        string text = companion.cardData.oracle_text.ToLowerInvariant();
        int ruleIndex = companionStrings.FindIndex(rule => text.Contains(rule.ToLowerInvariant()));
        if (ruleIndex < 0)
            return violations;

        var startingCards = new List<CardUI>();
        foreach (Transform child in content)
        {
            CardUI card = child.GetComponent<CardUI>();
            if (IsStartingDeckCard(card))
                startingCards.Add(card);
        }

        foreach (CardUI card in startingCards)
        {
            ScryfallCard data = card.cardData;
            string type = data.type_line ?? string.Empty;
            bool isLand = type.Contains("Land");
            bool isPermanent = !type.Contains("Instant") && !type.Contains("Sorcery");

            switch (ruleIndex)
            {
                case 0: // Gyruda
                    if (data.cmc % 2 != 0) violations.Add(card);
                    break;
                case 1: // Jegantha
                    var symbols = Regex.Matches(data.mana_cost ?? string.Empty, @"\{([^}]*)\}")
                        .Select(match => match.Groups[1].Value);
                    if (symbols.GroupBy(symbol => symbol).Any(group => group.Count() > 1)) violations.Add(card);
                    break;
                case 2: // Kaheera
                    if (type.Contains("Creature") && !new[] { "Cat", "Elemental", "Nightmare", "Dinosaur", "Beast" }.Any(type.Contains)) violations.Add(card);
                    break;
                case 3: // Keruga
                    if (!isLand && data.cmc < 3) violations.Add(card);
                    break;
                case 4: // Lurrus
                    if (isPermanent && data.cmc > 2) violations.Add(card);
                    break;
                case 5: // Obosh
                    if (!isLand && data.cmc % 2 == 0) violations.Add(card);
                    break;
                case 8: // Zirda
                    if (isPermanent && (string.IsNullOrEmpty(data.oracle_text) || !data.oracle_text.Contains(":"))) violations.Add(card);
                    break;
            }
        }

        // Yorion and Umori are whole-deck constraints; make every relevant
        // card red when the constraint cannot be met.
        if (ruleIndex == 7 && startingCards.Count < 120)
            violations.UnionWith(startingCards);
        if (ruleIndex == 6 && !StartingDeckSharesCardType(startingCards))
            violations.UnionWith(startingCards.Where(card => !card.cardData.type_line.Contains("Land")));

        return violations;
    }

    private static bool StartingDeckSharesCardType(List<CardUI> cards)
    {
        string sharedType = null;
        foreach (CardUI card in cards)
        {
            string type = card.cardData.type_line ?? string.Empty;
            if (type.Contains("Land"))
                continue;

            string cardType = type.Split('—')[0].Replace("Legendary ", "").Trim();
            if (sharedType == null)
                sharedType = cardType;
            else if (sharedType != cardType)
                return false;
        }
        return true;
    }

    public bool ValidateRule(int ruleIndex)
    {
        if (ruleIndex < 0 || ruleIndex >= companionStrings.Count)
            return false;
        Debug.Log("Validating" + companionStrings[ruleIndex]);
        switch (ruleIndex)
        {
            case 0:
                return EvenMana();
            case 1:
                return NoSameMana();
            case 2:
                return CreatureTypes();
            case 3:
                return CMCThreeOrGreater();
            case 4:
                return CMCTwoOrLess();
            case 5:
                return OddMana();
            case 6:
                return ShareCardType();
            case 7:
                return StartingDeckCardCount() >= 120;
            case 8:
                return EveryPermanentHasActivatedAbility();
            default:
                return false;
        }
    }

    private bool ShareCardType()
    {
        bool valid = true;
        string type = "";
        foreach (Transform child in content)
        {
            CardUI card = child.GetComponent<CardUI>();
            if (!IsStartingDeckCard(card) || card.cardData.type_line == null)
                continue;

            // Lands are exempt from the Umori restriction.
            if (card.cardData.type_line.Contains("Land"))
                continue;

            string cardType = card.cardData.type_line.Split('—')[0]
                          .Replace("Legendary ", "")
                          .Trim();

            if (type == "")
            {
                type = cardType;
            }
            else
            {
                valid = cardType == type;
            }

            if (!valid)
                break;
        }
        return valid;
    }

    private bool OddMana()
    {
        bool valid = true;
        foreach (Transform child in content)
        {
            if (!valid)
                break;
            CardUI card = child.GetComponent<CardUI>();

            if (!IsStartingDeckCard(card) || card.cardData.type_line == null)
                continue;

            if (card.cardData.type_line.Contains("Land"))
            {
                valid = true;
            }
            else
            {
                valid = card.cardData.cmc % 2 == 1;
            }
        }
        return valid;
    }

    private bool EveryPermanentHasActivatedAbility()
    {
        foreach (Transform child in content)
        {
            CardUI card = child.GetComponent<CardUI>();
            if (!IsStartingDeckCard(card))
                continue;

            string type = card.cardData.type_line ?? string.Empty;
            if (type.Contains("Instant") || type.Contains("Sorcery"))
                continue;

            if (string.IsNullOrEmpty(card.cardData.oracle_text) || !card.cardData.oracle_text.Contains(":"))
                return false;
        }

        return true;
    }

    private bool CMCTwoOrLess()
    {
        bool valid = true;
        foreach (Transform child in content)
        {
            if (!valid)
                break;
            CardUI card = child.GetComponent<CardUI>();

            if (!IsStartingDeckCard(card))
                continue;

            if (!card.cardData.type_line.Contains("Instant") && !card.cardData.type_line.Contains("Sorcery"))
            {
                valid = card.cardData.cmc <= 2;
            }
        }
        return valid;
    }

    private bool CMCThreeOrGreater()
    {
        bool valid = true;
        foreach (Transform child in content)
        {
            if (!valid)
                break;
            CardUI card = child.GetComponent<CardUI>();

            if (!IsStartingDeckCard(card))
                continue;

            if (card.cardData.type_line.Contains("Land"))
            {
                valid = true;
            }
            else
            {
                valid = card.cardData.cmc >= 3;
            }
        }
        return valid;
    }

    private bool CreatureTypes()
    {
        bool valid = true;
        List<string> validCreatureTypes = new() { "Cat", "Elemental", "Nightmare", "Dinosaur", "Beast" };
        foreach (Transform child in content)
        {
            if (!valid)
                break;
            CardUI card = child.GetComponent<CardUI>();

            if (!IsStartingDeckCard(card))
                continue;

            if (card.cardData.type_line.Contains("Creature"))
            {
                valid = validCreatureTypes.Any(creatureType => card.cardData.type_line.Contains(creatureType));
            }
        }

        return valid;
    }

    private bool NoSameMana()
    {
        bool valid = true;
        foreach (Transform child in content)
        {
            if (!valid)
                break;
            CardUI card = child.GetComponent<CardUI>();

            if (!IsStartingDeckCard(card))
                continue;

            string[] manaColors = Regex.Matches(card.cardData.mana_cost, @"\{([^}]*)\}")
                        .Select(m => m.Groups[1].Value)
                        .ToArray();
            string prev = "";
            foreach (string color in manaColors)
            {
                if (color == prev)
                {
                    valid = false;
                    break;
                }
                valid = true;
                prev = color;
            }
        }
        return valid;
    }

    private bool EvenMana()
    {
        bool valid = true;
        foreach (Transform child in content)
        {
            if (!valid)
                break;
            CardUI card = child.GetComponent<CardUI>();
            if (!IsStartingDeckCard(card))
                continue;
            valid = card.cardData.cmc % 2 == 0;
        }
        return valid;
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

        // Doctor's companion is a commander-pairing ability, not a Companion
        // deck-building restriction. Doctor Who cards use Scryfall set code WHO.
        return !string.Equals(card.set, "who", StringComparison.OrdinalIgnoreCase) &&
               card.oracle_text.StartsWith("Companion", StringComparison.OrdinalIgnoreCase);
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

    bool IsLegalInCommander(ScryfallCard card)
    {
        return card?.legalities?.commander == "legal";
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

            if (!IsStartingDeckCard(card))
                continue;

            bool valid = IsWithinColorIdentity(card.cardData, CommanderColors) && IsLegalInCommander(card.cardData);

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

        if (buildDeckRoutine != null)
            StopCoroutine(buildDeckRoutine);

        buildDeckRoutine = StartCoroutine(BuildList(currentDeck, true));
    }

    public void AddCards(List<string> cardNames)
    {
        if (cardNames == null || cardNames.Count == 0)
            return;

        currentDeck.AddRange(cardNames);
        if (buildDeckRoutine != null)
            StopCoroutine(buildDeckRoutine);

        buildDeckRoutine = StartCoroutine(BuildList(cardNames, false));
    }

    IEnumerator BuildList(List<string> cardsToLoad, bool clearExistingCards)
    {
        isBuildingDeck = true;
        if (clearExistingCards)
        {
            foreach (Transform child in content)
                Destroy(child.gameObject);
        }

        // Fetch each unique card once. This matters for basic lands and for
        // pasted lists that contain the same card in more than one section.
        var copiesByName = new Dictionary<string, int>();
        var displayNames = new Dictionary<string, string>();
        foreach (string cardName in cardsToLoad)
        {
            string key = CardImageCache.NormalizeName(cardName);
            if (string.IsNullOrEmpty(key))
                continue;

            copiesByName[key] = copiesByName.TryGetValue(key, out int copies) ? copies + 1 : 1;
            if (!displayNames.ContainsKey(key))
                displayNames[key] = cardName.Trim();
        }

        var uncachedNames = new List<string>();
        foreach (var entry in copiesByName)
        {
            string cardName = displayNames[entry.Key];
            if (cache != null && cache.TryGet(cardName, out Texture2D cachedTex, out ScryfallCard cachedData))
            {
                Sprite cachedSprite = Sprite.Create(cachedTex, new Rect(0, 0, cachedTex.width, cachedTex.height), new Vector2(0.5f, 0.5f));
                CreateCards(cardName, cachedSprite, cachedData, entry.Value);
            }
            else
            {
                uncachedNames.Add(cardName);
            }
        }

        // Collection requests accept up to 75 names, avoiding one metadata
        // request per card during a Commander import.
        for (int start = 0; start < uncachedNames.Count; start += 75)
        {
            var batch = uncachedNames.GetRange(start, Math.Min(75, uncachedNames.Count - start));
            var lookupNames = new List<string>(batch.Count);
            foreach (string cardName in batch)
                lookupNames.Add(GetScryfallLookupName(cardName));

            Dictionary<string, ScryfallCard> batchCards = null;
            yield return StartCoroutine(scryfall.GetCardsDataBatch(lookupNames, cards => batchCards = cards));
            if (batchCards == null)
                continue;

            foreach (string cardName in batch)
            {
                string key = CardImageCache.NormalizeName(cardName);
                string lookupKey = CardImageCache.NormalizeName(GetScryfallLookupName(cardName));
                if (!batchCards.TryGetValue(key, out ScryfallCard cardData) &&
                    !batchCards.TryGetValue(lookupKey, out cardData))
                {
                    // Deck sites sometimes export just one face of a DFC.
                    // Resolve only this unmatched name and accept no guesses.
                    yield return StartCoroutine(scryfall.ResolveCardAlias(cardName, card => cardData = card));
                    if (cardData == null)
                    {
                        Debug.LogWarning("Skipping: " + cardName);
                        continue;
                    }
                }
                yield return StartCoroutine(LoadCardImage(cardName, copiesByName[key], cardData));
            }
        }

        isBuildingDeck = false;
        buildDeckRoutine = null;
        RefreshValidation();
    }

    private static string GetScryfallLookupName(string cardName)
    {
        // The collection API identifies transforming/modal DFCs by either
        // face, not the pasted "front // back" display name.
        int separator = cardName.IndexOf("//", StringComparison.Ordinal);
        return separator >= 0 ? cardName.Substring(0, separator).Trim() : cardName;
    }

    IEnumerator LoadCard(string cardName, int copies)
    {
        // try cache first
        if (cache != null && cache.TryGet(cardName, out Texture2D cachedTex, out ScryfallCard cachedData))
        {
            Sprite cachedSprite = Sprite.Create(
                cachedTex,
                new Rect(0, 0, cachedTex.width, cachedTex.height),
                new Vector2(0.5f, 0.5f)
            );

            CreateCards(cardName, cachedSprite, cachedData, copies);
            yield break;
        }

        bool success = false;
        ScryfallCard cardData = null;
        Texture2D texture = null;

        // ScryfallClient already handles transient 429 responses. Retrying a
        // failed name here multiplied a single bad import line into 12 calls.
        yield return StartCoroutine(
            scryfall.GetCardData(cardName, (s, data, tex) =>
            {
                success = s;
                cardData = data;
                texture = tex;
            })
        );

        if (!success || cardData == null || texture == null)
        {
            Debug.LogWarning("Skipping: " + cardName);
            yield break;
        }

        // store in cache
        cache?.Store(cardName, texture, cardData);

        Sprite loadedSprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );

        CreateCards(cardName, loadedSprite, cardData, copies);
    }

    IEnumerator LoadCardImage(string cardName, int copies, ScryfallCard cardData)
    {
        bool success = false;
        Texture2D texture = null;
        yield return StartCoroutine(scryfall.GetCardImage(cardData, (s, tex) =>
        {
            success = s;
            texture = tex;
        }));

        if (!success || texture == null)
        {
            Debug.LogWarning("Skipping image: " + cardName);
            yield break;
        }

        cache?.Store(cardName, texture, cardData);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        CreateCards(cardName, sprite, cardData, copies);
    }

    private void CreateCards(string cardName, Sprite sprite, ScryfallCard cardData, int copies)
    {
        for (int i = 0; i < copies; i++)
        {
            GameObject createdObj = Instantiate(cardPrefab, content);
            createdObj.GetComponent<CardUI>().Init(cardName, sprite, this, cardData);
        }

        if (!isBuildingDeck)
            RefreshValidation();
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

            card.gameObject.SetActive(false);
            Destroy(card.gameObject);
        }
        selectedCards.Clear();
        RefreshValidation();
    }

    public void ClearDeck()
    {
        if (buildDeckRoutine != null)
        {
            StopCoroutine(buildDeckRoutine);
            buildDeckRoutine = null;
        }

        if (restoreCommanderRoutine != null)
        {
            StopCoroutine(restoreCommanderRoutine);
            restoreCommanderRoutine = null;
        }

        if (restoreCompanionRoutine != null)
        {
            StopCoroutine(restoreCompanionRoutine);
            restoreCompanionRoutine = null;
        }

        isBuildingDeck = false;
        currentDeck.Clear();
        selectedCards.Clear();
        commanders.Clear();
        companion = null;
        background = null;
        sideboardCardNames.Clear();
        CommanderColors.Clear();
        foreach (Transform child in content)
        {
            // Destroy is deferred until the end of the frame. Hide immediately
            // so the old deck cannot remain visible when starting a new one.
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        RefreshValidation();
    }

    /// <summary>Reapplies a saved commander once this deck's card UI has finished loading.</summary>
    public void RestoreCommanderByName(string commanderName)
    {
        if (!string.IsNullOrWhiteSpace(commanderName))
        {
            if (restoreCommanderRoutine != null)
                StopCoroutine(restoreCommanderRoutine);
            restoreCommanderRoutine = StartCoroutine(RestoreCommanderWhenReady(commanderName));
        }
    }

    /// <summary>Reapplies a saved companion after the saved commander has been restored.</summary>
    public void RestoreCompanionByName(string companionName)
    {
        if (!string.IsNullOrWhiteSpace(companionName))
        {
            if (restoreCompanionRoutine != null)
                StopCoroutine(restoreCompanionRoutine);
            restoreCompanionRoutine = StartCoroutine(RestoreCompanionWhenReady(companionName));
        }
    }

    private IEnumerator RestoreCommanderWhenReady(string commanderName)
    {
        // Let DisplayDeck start its rebuild coroutine before checking its state.
        yield return null;
        while (isBuildingDeck)
            yield return null;

        string wantedName = CardImageCache.NormalizeName(commanderName);
        foreach (Transform child in content)
        {
            CardUI card = child.GetComponent<CardUI>();
            if (card != null && CardImageCache.NormalizeName(card.cardName) == wantedName)
            {
                SetCommander(card);
                restoreCommanderRoutine = null;
                yield break;
            }
        }

        Debug.LogWarning("Saved commander was not found in the loaded deck: " + commanderName);
        restoreCommanderRoutine = null;
    }

    private IEnumerator RestoreCompanionWhenReady(string companionName)
    {
        yield return null;
        while (isBuildingDeck)
            yield return null;
        while (restoreCommanderRoutine != null)
            yield return null;

        string wantedName = CardImageCache.NormalizeName(companionName);
        foreach (Transform child in content)
        {
            CardUI card = child.GetComponent<CardUI>();
            if (card != null && CardImageCache.NormalizeName(card.cardName) == wantedName)
            {
                SetCommander(card);
                restoreCompanionRoutine = null;
                yield break;
            }
        }

        Debug.LogWarning("Saved companion was not found in the loaded deck: " + companionName);
        restoreCompanionRoutine = null;
    }

    private void RefreshValidation()
    {
        ValidateDeck();

        if (companion == null)
        {
            errorText.text = string.Empty;
            return;
        }

        if (!ValidateCompanion())
        {
            Debug.LogWarning("Invalid companion");
            errorText.text = "Deck invalid for companion";
        }
        else
        {
            errorText.text = string.Empty;
        }
    }
}
