using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class DeckParser
{
    enum Section
    {
        Main,
        Sideboard,
        Commander
    }

    public static ParsedDeck Parse(string input)
    {
        ParsedDeck deck = new ParsedDeck();

        var lines = input.Split('\n');
        Section currentSection = Section.Main;

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // SECTION HEADERS
            if (line.ToLower().Contains("sideboard"))
            {
                currentSection = Section.Sideboard;
                continue;
            }

            if (line.ToLower().Contains("commander"))
            {
                currentSection = Section.Commander;
                continue;
            }

            if (line.ToLower().Contains("deck"))
            {
                currentSection = Section.Main;
                continue;
            }

            // PARSE CARD LINE
            var parsed = ParseLine(line);
            if (parsed == null) continue;

            var (name, count) = parsed.Value;

            switch (currentSection)
            {
                case Section.Main:
                    deck.mainboard.Add((name, count));
                    break;
                case Section.Sideboard:
                    deck.sideboard.Add((name, count));
                    break;
                case Section.Commander:
                    deck.commanders.Add((name, count));
                    break;
            }
        }

        return deck;
    }

    private static (string name, int count)? ParseLine(string line)
    {
        // Handles:
        // "4 Lightning Bolt"
        // "Lightning Bolt x4"
        // "4x Lightning Bolt"

        line = line.Replace("\r", "").Trim();

        // Pattern 1: "4 Card Name"
        var match1 = Regex.Match(line, @"^(\d+)\s+(.+)$");
        if (match1.Success)
        {
            int count = int.Parse(match1.Groups[1].Value);
            string name = CleanCardName(match1.Groups[2].Value);
            return (name, count);
        }

        // Pattern 2: "Card Name x4"
        var match2 = Regex.Match(line, @"^(.+)\s+x(\d+)$", RegexOptions.IgnoreCase);
        if (match2.Success)
        {
            string name = CleanCardName(match2.Groups[1].Value);
            int count = int.Parse(match2.Groups[2].Value);
            return (name, count);
        }

        // Pattern 3: "4x Card Name"
        var match3 = Regex.Match(line, @"^(\d+)x\s+(.+)$", RegexOptions.IgnoreCase);
        if (match3.Success)
        {
            int count = int.Parse(match3.Groups[1].Value);
            string name = CleanCardName(match3.Groups[2].Value);
            return (name, count);
        }

        // fallback: assume 1 copy
        return (CleanCardName(line), 1);
    }

    public static List<string> ExpandDeck(ParsedDeck deck)
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


    private static string CleanCardName(string raw)
    {
        return raw
            .Replace("*", "")
            .Replace("[", "")
            .Replace("]", "")
            .Trim();
    }
}
