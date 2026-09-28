using System;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace Vaga.Mods
{
    /// <summary>
    /// Memory leak monitor — tracks mono heap, Unity reserved, GC counts, object spikes.
    /// Logs warnings when growth exceeds thresholds; optional on-screen overlay.
    /// </summary>
    public static class MemoryMonitor
    {
        public static bool Enabled = true;
        public static bool Overlay = false;
        public static bool LogToConsole = true;

        /// <summary>Seconds between samples.</summary>
        public static float SampleInterval = 2f;
        /// <summary>Warn if mono heap grows more than this (MB) between samples.</summary>
        public static float WarnHeapDeltaMb = 8f;
        /// <summary>Warn if total reserved grows more than this (MB) between samples.</summary>
        public static float WarnReservedDeltaMb = 16f;
        /// <summary>Warn if GC collection count jumps by more than this per sample.</summary>
        public static int WarnGcDelta = 4;

        public static long LastMonoBytes;
        public static long LastTotalReserved;
        public static long LastTotalAllocated;
        public static int LastGc0, LastGc1, LastGc2;
        public static int LastRigCount;
        public static int LastLineCount;
        public static int SpikeCount;
        public static string LastReport = "MemoryMonitor: idle";

        private static float _next;
        private static float _startTime;
        private static long _startMono;
        private static bool _baselined;
        private static GUIStyle _style;
        private static Rect _rect = new Rect(12, 12, 420, 140);

        public static void Update()
        {
            if (!Enabled) return;
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + SampleInterval;
            Sample();
        }

        public static void Sample()
        {
            long mono = GC.GetTotalMemory(false);
            long reserved = 0;
            long allocated = 0;
            try
            {
                reserved = Profiler.GetTotalReservedMemoryLong();
                allocated = Profiler.GetTotalAllocatedMemoryLong();
            }
            catch
            {
                try
                {
                    reserved = Profiler.GetTotalReservedMemory();
                    allocated = Profiler.GetTotalAllocatedMemory();
                }
                catch { }
            }

            int gc0 = GC.CollectionCount(0);
            int gc1 = GC.CollectionCount(1);
            int gc2 = GC.CollectionCount(2);

            int rigs = 0;
            try { rigs = LeakGuard.Rigs()?.Length ?? 0; } catch { }

            if (!_baselined)
            {
                _baselined = true;
                _startTime = Time.unscaledTime;
                _startMono = mono;
                LastMonoBytes = mono;
                LastTotalReserved = reserved;
                LastTotalAllocated = allocated;
                LastGc0 = gc0; LastGc1 = gc1; LastGc2 = gc2;
                LastRigCount = rigs;
                LastReport = Format(mono, reserved, allocated, gc0, gc1, gc2, rigs, 0, 0, 0, false);
                return;
            }

            long dMono = mono - LastMonoBytes;
            long dRes = reserved - LastTotalReserved;
            int dGc = (gc0 - LastGc0) + (gc1 - LastGc1) + (gc2 - LastGc2);

            bool warn = false;
            if (dMono > (long)(WarnHeapDeltaMb * 1024 * 1024)) warn = true;
            if (dRes > (long)(WarnReservedDeltaMb * 1024 * 1024)) warn = true;
            if (dGc > WarnGcDelta) warn = true;

            if (warn) SpikeCount++;

            LastReport = Format(mono, reserved, allocated, gc0, gc1, gc2, rigs, dMono, dRes, dGc, warn);

            if (LogToConsole)
            {
                if (warn) Debug.LogWarning("[MORPHINE][MEM] " + LastReport);
                else Debug.Log("[MORPHINE][MEM] " + LastReport);
            }

            LastMonoBytes = mono;
            LastTotalReserved = reserved;
            LastTotalAllocated = allocated;
            LastGc0 = gc0; LastGc1 = gc1; LastGc2 = gc2;
            LastRigCount = rigs;
        }

        public static void ForceGc()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch { }
            LeakGuard.Invalidate();
            Sample();
            Debug.Log("[MORPHINE][MEM] Forced GC — " + LastReport);
        }

        public static void Dump()
        {
            Sample();
            var sb = new StringBuilder(256);
            sb.AppendLine("[MORPHINE][MEM] DUMP");
            sb.AppendLine(LastReport);
            sb.Append("uptime_s=").Append((Time.unscaledTime - _startTime).ToString("F0"));
            sb.Append(" start_mono_mb=").Append(((_startMono / 1024f) / 1024f).ToString("F1"));
            sb.Append(" spikes=").Append(SpikeCount);
            Debug.Log(sb.ToString());
        }

        public static void Toggle() => Enabled = !Enabled;
        public static void ToggleOverlay() => Overlay = !Overlay;
        public static void ToggleLog() => LogToConsole = !LogToConsole;

        /// <summary>Call from a MonoBehaviour OnGUI if overlay is enabled.</summary>
        public static void DrawOverlay()
        {
            if (!Overlay || !Enabled) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 13,
                    richText = false
                };
                _style.normal.textColor = Color.white;
            }
            GUI.Box(_rect, LastReport + "\nspikes=" + SpikeCount, _style);
        }

        private static string Format(long mono, long reserved, long allocated, int gc0, int gc1, int gc2, int rigs,
            long dMono, long dRes, int dGc, bool warn)
        {
            float monoMb = (mono / 1024f) / 1024f;
            float resMb = (reserved / 1024f) / 1024f;
            float allocMb = (allocated / 1024f) / 1024f;
            float dMonoMb = (dMono / 1024f) / 1024f;
            float dResMb = (dRes / 1024f) / 1024f;
            return string.Format(
                "{0} mono={1:F1}MB ({2:+0.0;-0.0}MB) reserved={3:F1}MB ({4:+0.0;-0.0}MB) alloc={5:F1}MB GC={6}/{7}/{8} (d{9}) rigs={10}",
                warn ? "WARN" : "ok",
                monoMb, dMonoMb, resMb, dResMb, allocMb, gc0, gc1, gc2, dGc, rigs);
        }
    }
}
