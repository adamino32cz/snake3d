using System.Collections.Generic;
using UnityEngine;

// Tabulka 10 nejlepších výsledků. Ukládá se do PlayerPrefs jako text ve formátu JSON.
public static class HighScores
{
    [System.Serializable] public class Entry { public string name; public int score; public int level; }
    [System.Serializable] class Table { public List<Entry> entries = new List<Entry>(); }

    const string Key = "Snake3D_Top10";
    public const int MaxEntries = 10;

    public static List<Entry> Load()
    {
        string json = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(json)) return new List<Entry>();
        return JsonUtility.FromJson<Table>(json).entries;
    }

    // Dostane se skóre do TOP 10?
    public static bool Qualifies(int score)
    {
        if (score <= 0) return false;
        var list = Load();
        return list.Count < MaxEntries || score > list[list.Count - 1].score;
    }

    // Přidá výsledek, seřadí od nejlepšího a vrátí pořadí (1 = první místo).
    public static int Add(string name, int score, int level)
    {
        var list = Load();
        var e = new Entry { name = name, score = score, level = level };
        list.Add(e);
        list.Sort((a, b) => b.score.CompareTo(a.score));   // seřadit sestupně podle skóre
        if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(new Table { entries = list }));
        PlayerPrefs.Save();
        return list.IndexOf(e) + 1;
    }

    // Tabulka jako text pro UI. Řádek "highlight" (pořadí) se zvýrazní.
    public static string Format(int highlight)
    {
        var list = Load();
        var sb = new System.Text.StringBuilder("<color=#FF40D0>TOP 10</color>\n");
        for (int i = 0; i < MaxEntries; i++)
        {
            string line = i < list.Count
                ? $"{i + 1,2}. {list[i].name,-12} {list[i].score,5}  <size=70%>L{list[i].level}</size>"
                : $"{i + 1,2}. ---";
            sb.Append(i + 1 == highlight ? $"<color=#FFE14D>{line}</color>\n" : line + "\n");
        }
        return sb.ToString();
    }
}
