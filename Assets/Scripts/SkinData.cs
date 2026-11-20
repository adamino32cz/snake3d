using UnityEngine;

// Jeden vzhled hada: barvy hlavy a těla + záře (emission) pro neonový bloom.
// Seznam je v SkinCatalog – nový skin přidáš jako další položku v All.
[System.Serializable]
public class SkinData
{
    public string id;              // stabilní klíč (PlayerPrefs, parametr -skin)
    public string displayName;     // název v menu
    public Color headColor;        // barva hlavy (povrch)
    public Color bodyColor;        // barva těla / konce ocasu
    public Color emissionColor;    // záře (může být jasnější než bílá = HDR)
    public float headGlow = 1.8f;  // síla záře u hlavy
    public float bodyGlow = 1f;    // síla záře na ocase
    public bool secret;            // tajný skin: odemkne se kódem
    public string unlockCode;      // tajný kód (porovnává se bez ohledu na velikost písmen)
    // Háček pro později: vlastní model hlavy (např. anime postava). Zatím se nepoužívá.
    public string customHeadId;
}

// Datově řízený katalog skinů + uložení výběru a odemčení do PlayerPrefs.
public static class SkinCatalog
{
    public const string PrefsSelected = "Snake3D_Skin";
    public const string PrefsUnlockPrefix = "Snake3D_SkinUnlocked_";

    // Výchozí skiny. Další přidej sem (a případně vlastní model hlavy do customHeadId).
    public static readonly SkinData[] All =
    {
        new SkinData {
            id = "neon", displayName = "Neon zelená",
            headColor = new Color(0.1f, 1f, 0.6f),
            bodyColor = new Color(0.1f, 0.3f, 1f),
            emissionColor = new Color(0.2f, 1.2f, 0.7f),
            headGlow = 1.8f, bodyGlow = 1f
        },
        new SkinData {
            id = "fire", displayName = "Ohnivý",
            headColor = new Color(1f, 0.45f, 0.05f),
            bodyColor = new Color(1f, 0.08f, 0.02f),
            emissionColor = new Color(1.4f, 0.45f, 0.05f),
            headGlow = 2.1f, bodyGlow = 1.3f
        },
        new SkinData {
            id = "ice", displayName = "Ledový",
            headColor = new Color(0.55f, 0.95f, 1f),
            bodyColor = new Color(0.15f, 0.35f, 1f),
            emissionColor = new Color(0.4f, 0.9f, 1.5f),
            headGlow = 2f, bodyGlow = 1.2f
        },
        new SkinData {
            id = "gold", displayName = "Zlatý",
            headColor = new Color(1f, 0.85f, 0.2f),
            bodyColor = new Color(1f, 0.5f, 0.05f),
            emissionColor = new Color(1.5f, 1.1f, 0.2f),
            headGlow = 2f, bodyGlow = 1.2f
        },
        new SkinData {
            id = "delta", displayName = "DELTA",
            headColor = new Color(0.08f, 0.01f, 0.12f),
            bodyColor = new Color(0.02f, 0f, 0.05f),
            emissionColor = new Color(0.7f, 0.1f, 1.4f),
            headGlow = 2.4f, bodyGlow = 1.6f,
            secret = true, unlockCode = "DELTA",
            customHeadId = "delta" // připraveno pro budoucí model postavy
        },
    };

    static string selectedId = "neon";

    public static SkinData Current() => Get(selectedId) ?? All[0];

    public static SkinData Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return All[0];
        foreach (var s in All) if (s.id == id) return s;
        return null;
    }

    public static int IndexOf(string id)
    {
        for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i;
        return 0;
    }

    public static bool IsUnlocked(SkinData s)
    {
        if (s == null) return false;
        if (!s.secret) return true;
        return PlayerPrefs.GetInt(PrefsUnlockPrefix + s.id, 0) == 1;
    }

    public static void Unlock(SkinData s)
    {
        if (s == null) return;
        PlayerPrefs.SetInt(PrefsUnlockPrefix + s.id, 1);
        PlayerPrefs.Save();
    }

    // Odemkne skin podle tajného kódu (bez ohledu na velikost písmen). Vrací odemčený skin, nebo null.
    public static SkinData UnlockWithCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        string c = code.Trim();
        foreach (var s in All)
        {
            if (!s.secret || string.IsNullOrEmpty(s.unlockCode)) continue;
            if (string.Equals(s.unlockCode, c, System.StringComparison.OrdinalIgnoreCase))
            {
                Unlock(s);
                return s;
            }
        }
        return null;
    }

    public static bool Select(string id)
    {
        var s = Get(id);
        if (s == null || !IsUnlocked(s)) return false;
        selectedId = s.id;
        PlayerPrefs.SetString(PrefsSelected, selectedId);
        PlayerPrefs.Save();
        return true;
    }

    // Načte uložený výběr a případně parametr -skin <id> (pro test i odemkne tajný skin).
    public static void LoadFromPrefsAndArgs(string[] args)
    {
        selectedId = PlayerPrefs.GetString(PrefsSelected, "neon");
        if (Get(selectedId) == null || !IsUnlocked(Get(selectedId))) selectedId = "neon";
        int a = System.Array.IndexOf(args, "-skin");
        if (a >= 0 && a + 1 < args.Length)
        {
            var s = Get(args[a + 1]);
            if (s != null)
            {
                if (s.secret) Unlock(s); // testovací příznak může vybrat i tajný skin
                selectedId = s.id;
                PlayerPrefs.SetString(PrefsSelected, selectedId);
                PlayerPrefs.Save();
            }
        }
    }
}
