using UnityEngine;

namespace Vaga.Mods
{
    public class DesktopGuiHost : MonoBehaviour
    {
        private static DesktopGuiHost _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("MORPHINE_DesktopGui");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _instance = go.AddComponent<DesktopGuiHost>();
        }

        private void Update()
        {
            DesktopGui.Update();
            MemoryMonitor.Update();
        }

        private void OnGUI()
        {
            DesktopGui.Draw();
            MemoryMonitor.DrawOverlay();
        }
    }
}
