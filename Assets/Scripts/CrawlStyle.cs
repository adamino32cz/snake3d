using UnityEngine;

// Výběr stylu pohybu postavy (procedurální + Mixamo klipy). Uložené v PlayerPrefs.
public static class CrawlStyle
{
    public const string PrefsKey = "Snake3D_CrawlStyle";
    public const int Count = 5;
    public const int Slither = 0;
    public const int AllFours = 1;
    public const int Mixamo1 = 2;
    public const int Mixamo2 = 3;
    public const int Mixamo3 = 4;

    public static readonly string[] StateNames = { "", "", "Mixamo1", "Mixamo2", "Mixamo3" };

    // Tentativní názvy podle délky klipu (1.8 s / 2.33 s / 3.97 s); po snímcích se upřesní.
    static readonly string[] Names =
    {
        "Plazení",
        "Po čtyřech",
        "Mixamo: plazení s kopem",
        "Mixamo: legrace (na zádech)",
        "Mixamo: Low Crawl"
    };

    public static int Current
    {
        get
        {
            int v;
            if (PlayerPrefs.HasKey(PrefsKey))
                v = Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, 0), 0, Count - 1);
            else
                v = PlayerPrefs.GetInt(DeltaGirlCrawler.PrefsAllFours, 0) == 1 ? AllFours : Slither;
            // anim2 (legrace na zádech) je cursed – vypadává z cyklu
            if (v == Mixamo2) v = Mixamo3;
            return v;
        }
        set
        {
            int v = Mathf.Clamp(value, 0, Count - 1);
            PlayerPrefs.SetInt(PrefsKey, v);
            PlayerPrefs.SetInt(DeltaGirlCrawler.PrefsAllFours, v == AllFours ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static int Cycle()
    {
        int v = Current;
        do { v = (v + 1) % Count; } while (v == Mixamo2);
        Current = v;
        return Current;
    }

    public static bool IsMixamo(int s) => s >= Mixamo1 && s <= Mixamo3;
    public static bool IsMixamoCurrent => IsMixamo(Current);
    public static string DisplayName(int s) => Names[Mathf.Clamp(s, 0, Count - 1)];
    public static string DisplayNameCurrent => DisplayName(Current);
    public static string AnimatorState(int s) => IsMixamo(s) ? StateNames[s] : "";
}
