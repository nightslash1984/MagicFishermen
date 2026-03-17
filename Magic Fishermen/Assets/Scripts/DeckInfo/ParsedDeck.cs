using System.Collections.Generic;
using UnityEngine;

public class ParsedDeck
{
    public List<(string name, int count)> mainboard = new();
    public List<(string name, int count)> sideboard = new();
    public List<(string name, int count)> commanders = new();
}
