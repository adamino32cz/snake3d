using UnityEngine;

// Cesta jedné překážky: jezdí tam a zpět mezi buňkami "from" a "to".
[System.Serializable]
public class ObstaclePath
{
    public Vector2Int from, to;
    public float speed = 2f;
    public float size = 1f;      // 1 = normální pila, 2.2 = obří "boss" pila
}

// Druh úkolu (cíle) v levelu kampaně.
public enum Objective
{
    EatFood,      // sněz N jakýchkoli jídel
    EatKind,      // sněz N jídel určitého druhu (foodKind)
    ReachLength,  // dorůst na délku N článků
    Combo,        // dosáhni combo xN (aspoň jednou)
    Survive,      // přežij N sekund
}

// Nastavení jednoho levelu: barvy arény, rychlost hada, kolik bodů je potřeba na další level a překážky.
[System.Serializable]
public class LevelData
{
    public string name;
    public Color gridColor;       // barva svítící mřížky na podlaze
    public Color wallColorA;      // barva dvou okrajů (nahoře/dole)
    public Color wallColorB;      // barva dvou okrajů (vlevo/vpravo)
    public Color outerColor;      // barva mřížky kolem arény
    public Color fogColor;        // barva mlhy
    public float stepInterval;    // menší číslo = rychlejší had
    public int scoreToNext;       // (už se nepoužívá – postupuje se splněním cíle)
    [Header("Cíl levelu (kampaň)")]
    public Objective objective;   // co musí hráč splnit
    public int target;            // kolik (jídel, článků, sekund, combo)
    public FoodKind foodKind;     // jen pro EatKind
    public bool noTurbo;          // navíc: nesmíš sníst turbo (jinak level začíná znovu)
    public float timeLimit;       // časový limit v sekundách (0 = bez limitu)
    public ObstaclePath[] obstacles;  // pozor: překážka č. 3 (index 2) je dlouhá (2 pily za sebou)
    public bool dark;             // tma: svítí jen lampa nad hlavou hada
    public string hint;           // krátký popis, co je v levelu nového
}
