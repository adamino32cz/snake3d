// Parametry z příkazové řádky (-startlevel, -autoplay...).
// Ve WebGL čteme query string (?screenshots&shotmotion&crawlstyle=2).
public static class CmdArgs
{
    static string[] cache;
    public static string[] Get()
    {
        if (cache != null) return cache;
#if UNITY_WEBGL && !UNITY_EDITOR
        cache = FromUrl();
#else
        try { cache = System.Environment.GetCommandLineArgs(); }
        catch { cache = new string[0]; }
#endif
        return cache;
    }

    static string[] FromUrl()
    {
        string url = UnityEngine.Application.absoluteURL;
        if (string.IsNullOrEmpty(url)) return new string[0];
        int q = url.IndexOf('?');
        if (q < 0) return new string[0];
        string query = url.Substring(q + 1);
        int hash = query.IndexOf('#');
        if (hash >= 0) query = query.Substring(0, hash);
        var list = new System.Collections.Generic.List<string>();
        foreach (var raw in query.Split('&'))
        {
            if (string.IsNullOrEmpty(raw)) continue;
            int eq = raw.IndexOf('=');
            string key = eq >= 0 ? raw.Substring(0, eq) : raw;
            string val = eq >= 0 ? raw.Substring(eq + 1) : "";
            key = System.Uri.UnescapeDataString(key);
            val = System.Uri.UnescapeDataString(val);
            if (key.Length == 0) continue;
            if (!key.StartsWith("-")) key = "-" + key;
            list.Add(key);
            if (val.Length > 0) list.Add(val);
        }
        return list.ToArray();
    }
}
