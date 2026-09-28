using Vaga.Classes;
using UnityEngine;

namespace Vaga
{
    public class Settings
    {
        public static ExtGradient backgroundColor = new ExtGradient
        {
            colors = ExtGradient.GetSolidGradient(new Color(0.06f, 0.07f, 0.10f, 1f))
        };

        public static ExtGradient[] buttonColors = new ExtGradient[]
        {
            new ExtGradient { colors = ExtGradient.GetSolidGradient(new Color(0.14f, 0.15f, 0.20f, 1f)) }, // off
            new ExtGradient { colors = ExtGradient.GetSolidGradient(new Color(0.28f, 0.16f, 0.42f, 1f)) }  // on
        };

        public static Color[] textColors = new Color[]
        {
            new Color(0.92f, 0.92f, 0.96f, 1f), // off
            new Color(1f, 0.85f, 1f, 1f)        // on
        };

        public static ExtGradient[] outlineColors = new ExtGradient[]
        {
            new ExtGradient { colors = ExtGradient.GetSimpleGradient(new Color(0.55f, 0.25f, 0.95f), new Color(0.70f, 0.35f, 1f)) },
            new ExtGradient { colors = ExtGradient.GetSimpleGradient(new Color(0.70f, 0.35f, 1f), new Color(1f, 0.40f, 0.75f)) }
        };

        public static Font currentFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 24);

        public static bool fpsCounter = true;
        public static bool disableNotifications;
        public static bool disconnectButton = true;
        public static bool homeButton = true;
        public static bool Settingsbutton = true;
        public static bool rightHanded;
        public static bool enableRainbowGradient = true;
        public static bool HomeButtonGradient = false;
        public static bool PageButtonGradiant = false;
        public static bool SettingsButtonGradiant = false;

        public static bool animateTitle = false;
        public static bool desktopHideBackground = true;
        public static bool desktopGui = false; // PC: no panel behind buttons
        public static KeyCode keyboardButton = KeyCode.Q;

        public static Vector3 menuSize = new Vector3(0.1f, 1.02f, 1.14f);
        public static int buttonsPerPage = 6;

        public static float gradientSpeed = 0.65f;

        public static Color CustomColor()
        {
            if (enableRainbowGradient && UnityEngine.XR.XRSettings.isDeviceActive)
            {
                float t = Time.time * gradientSpeed;
                return Color.HSVToRGB(Mathf.Repeat(t * 0.08f, 1f), 0.55f, 1f);
            }
            return outlineColors[1].GetCurrentColor();
        }
    }
}
