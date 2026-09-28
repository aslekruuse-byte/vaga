using System;
using System.Collections.Generic;
using UnityEngine;
using Vaga.Classes;
using Vaga.Menu;

namespace Vaga.Mods
{
    /// <summary>
    /// Fade-inspired desktop UI — left sidebar categories, clean module list, dark chrome.
    /// Toggle with H (rebindable). World-space VR menu is separate; this is the PC panel.
    /// </summary>
    public static class DesktopGui
    {
        public static bool Visible = false; // PC overlay off by default
        public static KeyCode ToggleKey = KeyCode.H;
        public static bool WaitingForBind;

        private static readonly KeyCode[] BindOptions =
        {
            KeyCode.H, KeyCode.G, KeyCode.F, KeyCode.J, KeyCode.K, KeyCode.L,
            KeyCode.RightShift, KeyCode.LeftShift, KeyCode.RightControl, KeyCode.LeftControl,
            KeyCode.RightAlt, KeyCode.Insert, KeyCode.Delete, KeyCode.Home, KeyCode.End,
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Mouse3, KeyCode.Mouse4
        };

        public static string ToggleKeyName => ToggleKey.ToString();

        public static void CycleToggleKey()
        {
            int idx = 0;
            for (int i = 0; i < BindOptions.Length; i++)
                if (BindOptions[i] == ToggleKey) { idx = i; break; }
            idx = (idx + 1) % BindOptions.Length;
            ToggleKey = BindOptions[idx];
            Debug.Log("[MORPHINE] GUI key -> " + ToggleKey);
        }

        public static void StartRebind()
        {
            WaitingForBind = true;
            Debug.Log("[MORPHINE] Press any key to bind GUI...");
        }

        private static int _cat;
        private static Vector2 _scroll;
        private static bool _stylesReady;
        private static float _anim;

        private static GUIStyle _win, _title, _sideBtn, _sideBtnOn, _row, _rowLabel, _rowHint, _pill, _close;
        private static Texture2D _texBg, _texSide, _texRow, _texRowHover, _texAccent, _texTrack, _texKnob, _texLine;

        // Fade-style categories (sidebar)
        private static readonly (string name, int cat)[] Cats =
        {
            ("Combat", 8),
            ("Movement", 3),
            ("Visuals", 6),
            ("Players", 4),
            ("GameMode", 5),
            ("Global", 7),
            ("Exploits", 9),
            ("AntiBan", 10),
            ("Master", 11),
            ("Infection", 12),
            ("Settings", 1),
        };

        public static void Toggle() => Visible = !Visible;

        public static void Update()
        {
            try
            {
                if (WaitingForBind)
                {
                    foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
                    {
                        if (k == KeyCode.None || k == KeyCode.Mouse0 || k == KeyCode.Mouse1) continue;
                        if (Input.GetKeyDown(k))
                        {
                            ToggleKey = k;
                            WaitingForBind = false;
                            Debug.Log("[MORPHINE] GUI key bound -> " + k);
                            break;
                        }
                    }
                }
                else if (Input.GetKeyDown(ToggleKey))
                {
                    Visible = !Visible;
                }
            }
            catch { }
            _anim = Mathf.MoveTowards(_anim, Visible ? 1f : 0f, Time.unscaledDeltaTime * 10f);
        }

        public static void Draw()
        {
            if (_anim < 0.01f) return;
            EnsureStyles();

            float a = _anim;
            float w = 640f, h = 420f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            Color prev = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.45f * a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = new Color(1, 1, 1, a);

            GUI.Window(918274, new Rect(x, y, w, h), id =>
            {
                DrawBody(w, h);
                GUI.DragWindow(new Rect(0, 0, w - 40, 28));
            }, "", _win);

            GUI.color = prev;
        }

        private static void DrawBody(float w, float h)
        {
            // Title bar
            GUI.Label(new Rect(14, 8, 220, 22), "fade  ·  morphine", _title);
            string keyHint = WaitingForBind ? "rebind..." : ("[" + ToggleKeyName + "]");
            GUI.Label(new Rect(w - 110, 10, 70, 18), keyHint, _rowHint);
            if (GUI.Button(new Rect(w - 34, 6, 24, 22), "x", _close))
                Visible = false;

            // accent line under title
            GUI.DrawTexture(new Rect(0, 32, w, 1), _texAccent);

            float sideW = 128f;
            float contentX = sideW + 8f;
            float contentW = w - contentX - 10f;
            float top = 40f;
            float contentH = h - top - 10f;

            // Sidebar background
            GUI.DrawTexture(new Rect(0, top, sideW, contentH), _texSide);

            // Sidebar buttons
            float sy = top + 8f;
            for (int i = 0; i < Cats.Length; i++)
            {
                bool on = i == _cat;
                var st = on ? _sideBtnOn : _sideBtn;
                if (GUI.Button(new Rect(8, sy, sideW - 16, 26), Cats[i].name, st))
                {
                    _cat = i;
                    _scroll = Vector2.zero;
                }
                if (on)
                    GUI.DrawTexture(new Rect(0, sy + 4, 3, 18), _texAccent);
                sy += 28f;
            }

            // Module list
            int catIdx = Cats[Mathf.Clamp(_cat, 0, Cats.Length - 1)].cat;
            ButtonInfo[] list = null;
            try
            {
                if (Buttons.buttons != null && catIdx >= 0 && catIdx < Buttons.buttons.Length)
                    list = Buttons.buttons[catIdx];
            }
            catch { }

            if (list == null)
            {
                GUI.Label(new Rect(contentX + 8, top + 12, 300, 24), "empty", _rowLabel);
                return;
            }

            var mods = new List<ButtonInfo>();
            for (int i = 0; i < list.Length; i++)
            {
                var b = list[i];
                if (b == null) continue;
                string t = b.buttonText ?? "";
                if (t.StartsWith("</>") || t == "-" || b.Label) continue;
                mods.Add(b);
            }

            float rowH = 34f;
            float contentInnerH = mods.Count * rowH + 8f;
            _scroll = GUI.BeginScrollView(
                new Rect(contentX, top, contentW, contentH),
                _scroll,
                new Rect(0, 0, contentW - 18, contentInnerH));

            for (int i = 0; i < mods.Count; i++)
            {
                float ry = i * rowH;
                DrawRow(new Rect(0, ry, contentW - 20, rowH - 2), mods[i]);
            }

            GUI.EndScrollView();
        }

        private static void DrawRow(Rect r, ButtonInfo b)
        {
            GUI.Box(r, "", _row);

            string name = b.buttonText ?? "mod";
            if (name.Length > 28) name = name.Substring(0, 27) + "...";
            GUI.Label(new Rect(r.x + 10, r.y + 7, r.width - 70, 20), name, _rowLabel);

            if (b.isTogglable)
            {
                Rect tg = new Rect(r.x + r.width - 42, r.y + 8, 32, 16);
                if (DrawToggle(tg, b.enabled))
                    ToggleButton(b);
            }
            else
            {
                if (GUI.Button(new Rect(r.x + r.width - 52, r.y + 5, 44, 22), "run", _pill))
                {
                    try { b.method?.Invoke(); } catch { }
                }
            }
        }

        private static void ToggleButton(ButtonInfo b)
        {
            if (b.isTogglable)
            {
                b.enabled = !b.enabled;
                try
                {
                    if (b.enabled) b.enableMethod?.Invoke();
                    else b.disableMethod?.Invoke();
                }
                catch { }
            }
            else
            {
                try { b.method?.Invoke(); } catch { }
            }
        }

        private static bool DrawToggle(Rect r, bool on)
        {
            GUI.DrawTexture(r, on ? _texAccent : _texTrack);
            float kx = on ? r.x + r.width - 14 : r.x + 2;
            GUI.DrawTexture(new Rect(kx, r.y + 1, 12, 12), _texKnob);
            return GUI.Button(r, "", GUIStyle.none);
        }

        private static void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            // Fade palette: near-black, soft gray text, violet accent
            Color bg = new Color(0.06f, 0.06f, 0.07f, 0.98f);
            Color side = new Color(0.08f, 0.08f, 0.09f, 1f);
            Color row = new Color(0.10f, 0.10f, 0.11f, 1f);
            Color accent = new Color(0.62f, 0.42f, 1f, 1f); // soft violet like many Fade builds
            Color text = new Color(0.88f, 0.88f, 0.90f, 1f);
            Color muted = new Color(0.45f, 0.45f, 0.50f, 1f);

            _texBg = Solid(bg);
            _texSide = Solid(side);
            _texRow = Solid(row);
            _texRowHover = Solid(new Color(0.13f, 0.13f, 0.15f, 1f));
            _texAccent = Solid(accent);
            _texTrack = Solid(new Color(0.22f, 0.22f, 0.25f, 1f));
            _texKnob = Solid(Color.white);
            _texLine = Solid(accent);

            _win = new GUIStyle(GUI.skin.window)
            {
                normal = { background = _texBg, textColor = text },
                onNormal = { background = _texBg },
                border = new RectOffset(4, 4, 4, 4),
                padding = new RectOffset(0, 0, 0, 0)
            };
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = text }
            };
            _sideBtn = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 4, 0, 0),
                normal = { background = _texSide, textColor = muted },
                hover = { background = _texRow, textColor = text },
                active = { background = _texRow, textColor = accent }
            };
            _sideBtnOn = new GUIStyle(_sideBtn)
            {
                fontStyle = FontStyle.Bold,
                normal = { background = _texRow, textColor = accent },
                hover = { background = _texRow, textColor = accent }
            };
            _row = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texRow },
                border = new RectOffset(2, 2, 2, 2)
            };
            _rowLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = text },
                clipping = TextClipping.Clip
            };
            _rowHint = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = muted }
            };
            _pill = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _texTrack, textColor = text },
                hover = { background = _texAccent, textColor = Color.white },
                active = { background = _texAccent, textColor = Color.white }
            };
            _close = new GUIStyle(_pill)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            t.hideFlags = HideFlags.DontSave;
            return t;
        }
    }
}
