using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Vaga.Mods
{
    /// <summary>
    /// UI theme loaded from AssetBundle when present, else procedural high-quality materials.
    /// Bundle search order:
    ///   1) BepInEx/plugins/MORPHINE/morphine_ui
    ///   2) next to plugin DLL: morphine_ui
    ///   3) embedded resource stream "morphine_ui" (optional)
    /// Expected bundle assets (optional): "PanelMat", "ButtonMat", "ButtonOnMat", "AccentMat", "TitleFont"
    /// </summary>
    public static class UiTheme
    {
        public static bool Loaded;
        public static bool FromBundle;

        public static Material PanelMat;
        public static Material OutlineMat;
        public static Material ButtonOffMat;
        public static Material ButtonOnMat;
        public static Material AccentMat;
        public static Material GlassMat;
        public static Font TitleFont;
        public static Font BodyFont;

        // Palette — Fade.wtf style (dark phone + rose accent)
        public static Color Bg = new Color(0.08f, 0.08f, 0.09f, 0.98f);
        public static Color BgGlass = new Color(0.10f, 0.10f, 0.11f, 0.94f);
        public static Color Outline = new Color(0.18f, 0.18f, 0.20f, 1f);
        public static Color Accent = new Color(1.00f, 0.45f, 0.55f, 1f);      // rose/pink
        public static Color AccentHot = new Color(1.00f, 0.55f, 0.65f, 1f);
        public static Color ButtonOff = new Color(0.12f, 0.12f, 0.13f, 1f);
        public static Color ButtonOn = new Color(0.22f, 0.14f, 0.16f, 1f);
        public static Color TextOff = new Color(0.95f, 0.95f, 0.97f, 1f);
        public static Color TextOn = new Color(1.00f, 0.70f, 0.75f, 1f);
        public static Color Danger = new Color(0.85f, 0.25f, 0.35f, 1f);

        private static AssetBundle _bundle;
        private static Texture2D _noise;

        public static void Ensure()
        {
            if (Loaded) return;
            Loaded = true;
            TryLoadBundle();
            BuildProceduralFallback();
        }

        public static void Unload()
        {
            if (_bundle != null)
            {
                try { _bundle.Unload(false); } catch { }
                _bundle = null;
            }
            FromBundle = false;
        }

        private static void TryLoadBundle()
        {
            string[] candidates = new string[]
            {
                Path.Combine(PathsSafe(), "MORPHINE", "morphine_ui"),
                Path.Combine(PathsSafe(), "morphine_ui"),
                Path.Combine(AssemblyDir(), "morphine_ui"),
                Path.Combine(AssemblyDir(), "Assets", "morphine_ui"),
            };

            foreach (var path in candidates)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                try
                {
                    _bundle = AssetBundle.LoadFromFile(path);
                    if (_bundle == null) continue;
                    FromBundle = true;
                    PanelMat = _bundle.LoadAsset<Material>("PanelMat") ?? _bundle.LoadAsset<Material>("panel");
                    ButtonOffMat = _bundle.LoadAsset<Material>("ButtonMat") ?? _bundle.LoadAsset<Material>("button_off");
                    ButtonOnMat = _bundle.LoadAsset<Material>("ButtonOnMat") ?? _bundle.LoadAsset<Material>("button_on");
                    AccentMat = _bundle.LoadAsset<Material>("AccentMat") ?? _bundle.LoadAsset<Material>("accent");
                    TitleFont = _bundle.LoadAsset<Font>("TitleFont") ?? _bundle.LoadAsset<Font>("title");
                    BodyFont = _bundle.LoadAsset<Font>("BodyFont") ?? _bundle.LoadAsset<Font>("body");
                    Debug.Log("[MORPHINE] UI AssetBundle loaded: " + path);
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[MORPHINE] AssetBundle load failed: " + path + " — " + ex.Message);
                    _bundle = null;
                }
            }

            // Embedded resource fallback
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                foreach (var name in asm.GetManifestResourceNames())
                {
                    if (name.IndexOf("morphine_ui", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    using (var stream = asm.GetManifestResourceStream(name))
                    {
                        if (stream == null) continue;
                        var buf = new byte[stream.Length];
                        stream.Read(buf, 0, buf.Length);
                        _bundle = AssetBundle.LoadFromMemory(buf);
                        if (_bundle != null)
                        {
                            FromBundle = true;
                            PanelMat = _bundle.LoadAsset<Material>("PanelMat");
                            ButtonOffMat = _bundle.LoadAsset<Material>("ButtonMat");
                            ButtonOnMat = _bundle.LoadAsset<Material>("ButtonOnMat");
                            AccentMat = _bundle.LoadAsset<Material>("AccentMat");
                            Debug.Log("[MORPHINE] UI AssetBundle loaded from embedded resource: " + name);
                            return;
                        }
                    }
                }
            }
            catch { }
        }

        private static void BuildProceduralFallback()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("GUI/Text Shader");

            if (PanelMat == null) PanelMat = MakeMat(sh, Bg, 0.15f, 0.4f);
            if (GlassMat == null) GlassMat = MakeMat(sh, BgGlass, 0.05f, 0.2f);
            if (OutlineMat == null) OutlineMat = MakeMat(sh, Outline, 0.0f, 0.9f);
            if (ButtonOffMat == null) ButtonOffMat = MakeMat(sh, ButtonOff, 0.25f, 0.35f);
            if (ButtonOnMat == null) ButtonOnMat = MakeMat(sh, ButtonOn, 0.1f, 0.7f);
            if (AccentMat == null) AccentMat = MakeMat(sh, Accent, 0.0f, 1.0f);

            if (TitleFont == null)
                TitleFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Helvetica" }, 28);
            if (BodyFont == null)
                BodyFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 22);

            // soft noise for subtle texture on panels
            if (_noise == null)
            {
                _noise = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                _noise.wrapMode = TextureWrapMode.Repeat;
                _noise.filterMode = FilterMode.Bilinear;
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float n = UnityEngine.Random.Range(0.92f, 1f);
                        _noise.SetPixel(x, y, new Color(n, n, n, 1f));
                    }
                _noise.Apply();
                if (PanelMat != null && PanelMat.HasProperty("_MainTex"))
                    PanelMat.mainTexture = _noise;
            }
        }

        private static Material MakeMat(Shader sh, Color c, float smooth, float metallic)
        {
            var m = new Material(sh);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 0.15f);
            }
            m.hideFlags = HideFlags.DontSave;
            return m;
        }

        public static void ApplyToRenderer(Renderer r, Material mat)
        {
            if (r == null || mat == null) return;
            r.sharedMaterial = mat;
        }

        public static void ApplyButton(Renderer r, bool on)
        {
            ApplyToRenderer(r, on ? ButtonOnMat : ButtonOffMat);
        }

        private static string PathsSafe()
        {
            try
            {
                // BepInEx plugins folder
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(dir))
                {
                    var parent = Directory.GetParent(dir);
                    if (parent != null) return parent.FullName;
                    return dir;
                }
            }
            catch { }
            return Application.dataPath;
        }

        private static string AssemblyDir()
        {
            try { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "."; }
            catch { return "."; }
        }
    }
}
