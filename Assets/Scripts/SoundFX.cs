using UnityEngine;

// Zvuky a hudba. Zvuky jsou vyrobené programem (Tools/generate_audio.py) a leží v Assets/Audio.
// Hlasitost se dá měnit v Inspectoru na objektu "SoundFX".
public class SoundFX : MonoBehaviour
{
    [Header("Zvuky")]
    public AudioClip eatClip;      // snězení jídla ("blip")
    public AudioClip turboClip;    // zrychlení ("vžum")
    public AudioClip crashClip;    // náraz ("bum")
    public AudioClip musicClip;    // hudba v pozadí (opakuje se dokola)

    [Header("Hlasitost (0 = ticho, 1 = naplno)")]
    [Range(0, 1)] public float effectsVolume = 0.8f;
    [Range(0, 1)] public float musicVolume = 0.35f;

    AudioSource sfx, music;

    void Awake()
    {
        // načteme uloženou hlasitost (PlayerPrefs = malé úložiště nastavení na počítači)
        musicVolume = PlayerPrefs.GetFloat("Snake3D_MusicVolume", musicVolume);
        effectsVolume = PlayerPrefs.GetFloat("Snake3D_EffectsVolume", effectsVolume);
        sfx = gameObject.AddComponent<AudioSource>();     // pro krátké zvuky
        music = gameObject.AddComponent<AudioSource>();   // pro hudbu
        music.clip = musicClip; music.loop = true; music.volume = musicVolume;
        if (musicClip) music.Play();
    }

    // Uloží hlasitost, aby platila i při dalším spuštění hry.
    public void SaveVolumes()
    {
        PlayerPrefs.SetFloat("Snake3D_MusicVolume", musicVolume);
        PlayerPrefs.SetFloat("Snake3D_EffectsVolume", effectsVolume);
        PlayerPrefs.Save();
    }

    bool crashed;
    public float MusicTime => music && music.isPlaying ? music.time : Time.time; // čas v hudbě (pro blikání do rytmu)
    public float bpm = 110f;                                                      // tempo hudby (doby za minutu)
    void Update() { if (!crashed) music.volume = musicVolume; } // změna hlasitosti v Inspectoru funguje i za běhu

    // pitch = výška tónu (1 = normální). Malá náhodná změna, aby zvuk nezněl pořád stejně.
    void Play(AudioClip c, float pitch = 1f)
    {
        if (!c) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(c, effectsVolume);
    }

    public void Eat(float pitch = 1f) => Play(eatClip, pitch * Random.Range(0.95f, 1.05f));
    public void Turbo() => Play(turboClip);
    public void Crash() { Play(crashClip); music.volume = musicVolume * 0.3f; crashed = true; } // po nárazu hudba ztichne
}
