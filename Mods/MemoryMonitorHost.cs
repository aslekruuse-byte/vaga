using UnityEngine;

namespace Vaga.Mods
{
    /// <summary>
    /// DontDestroy host so MemoryMonitor overlay can draw OnGUI.
    /// </summary>
    public class MemoryMonitorHost : MonoBehaviour
    {
        private static MemoryMonitorHost _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("MORPHINE_MemoryMonitor");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _instance = go.AddComponent<MemoryMonitorHost>();
        }

        private void Update()
        {
            MemoryMonitor.Update();
        }

        private void OnGUI()
        {
            MemoryMonitor.DrawOverlay();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
