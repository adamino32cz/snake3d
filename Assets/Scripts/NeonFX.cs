using UnityEngine;

// Efekty: "výbuch" svítících částic, když had sní jídlo nebo narazí.
// Používá jeden systém částic pro všechno – je to rychlejší než pořád vytvářet nové.
public class NeonFX : MonoBehaviour
{
    public Material particleMaterial;   // průhledný svítící materiál (nastaví SnakeSceneSetup)
    ParticleSystem ps;

    void Awake()
    {
        var go = new GameObject("NeonBurst");
        go.transform.SetParent(transform);
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;                                   // základní nastavení částic
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.gravityModifier = 0.8f;                          // částice trochu padají
        main.maxParticles = 2000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission; emission.enabled = false; // částice vypouštíme ručně (Emit)
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.2f;

        // postupné mizení a zmenšování
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        if (particleMaterial) r.sharedMaterial = particleMaterial;
        ps.Play();
        CreateBackgroundMotes();
    }

    // Pozadí: pomalu stoupající svítící "prach" kolem arény – scéna pak nepůsobí staticky.
    void CreateBackgroundMotes()
    {
        var go = new GameObject("BackgroundMotes");
        go.transform.SetParent(transform);
        go.transform.position = new Vector3(0, -1f, 0);
        var motes = go.AddComponent<ParticleSystem>();
        motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = motes.main;
        main.loop = true;
        main.startLifetime = 9f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 2f, 2.5f), new Color(2.5f, 0.3f, 2f)); // azurová až růžová
        main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;                                   // hned od začátku je prach všude
        var emission = motes.emission; emission.rateOverTime = 35f;
        var shape = motes.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(44f, 0.5f, 44f);
        shape.rotation = new Vector3(-90f, 0f, 0f);            // částice letí nahoru
        var col = motes.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        if (particleMaterial) r.sharedMaterial = particleMaterial;
        motes.Play();
    }

    // Vypustí "count" částic na pozici "pos" v barvě "color".
    // size = velikost částic (větší pro náraz).
    public void Burst(Vector3 pos, Color color, int count = 40, float size = 0.2f)
    {
        for (int i = 0; i < count; i++)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = pos, startColor = color, applyShapeToPosition = true,
                startSize = size * Random.Range(0.5f, 1.5f)          // každá částice trochu jinak velká
            };
            ps.Emit(p, 1);
        }
    }
}
