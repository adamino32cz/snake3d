using UnityEngine;

public enum FoodKind { Normal, Gold, Speed, Time, Shrink, Shield, Magnet }  // Shield = štít (nesmrtelnost), Magnet = přitahuje jídlo

[System.Serializable]
public class FoodType
{
    public FoodKind kind;
    public string name;
    public Color color;
    public int points;
    public int grow;          // kolik článků přidá (záporné = zkrátí)
    public float bonusTime;   // přidaný čas v sekundách
    public float spawnWeight = 1f;
}
