// Parametry z příkazové řádky (-startlevel, -autoplay...).
// V prohlížeči (WebGL) žádné nejsou, tak vracíme prázdné pole.
public static class CmdArgs
{
    static string[] cache;
    public static string[] Get()
    {
        if (cache != null) return cache;
#if UNITY_WEBGL && !UNITY_EDITOR
        cache = new string[0];
#else
        try { cache = System.Environment.GetCommandLineArgs(); }
        catch { cache = new string[0]; }
#endif
        return cache;
    }
}
