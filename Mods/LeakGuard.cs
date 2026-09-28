using System.Collections.Generic;
using UnityEngine;

namespace Vaga.Mods
{
    public static class PayloadPool
    {
        private static readonly System.Collections.Generic.Dictionary<int, byte[]> _pool =
            new System.Collections.Generic.Dictionary<int, byte[]>();

        public static byte[] Get(int size)
        {
            if (size <= 0) size = 256;
            // round to bucket
            int bucket = 256;
            while (bucket < size) bucket *= 2;
            if (bucket > 65536) bucket = size; // huge one-offs not pooled long
            if (_pool.TryGetValue(bucket, out var buf) && buf != null && buf.Length >= size)
                return buf;
            buf = new byte[bucket];
            if (bucket <= 65536) _pool[bucket] = buf;
            return buf;
        }
    }

    /// <summary>
    /// Shared caches to stop per-frame FindObjectsOfType + Material/LineRenderer leaks.
    /// </summary>
    public static class LeakGuard
    {
        private static VRRig[] _rigCache = new VRRig[0];
        private static float _rigCacheTime;
        private static readonly float RigCacheTtl = 0.25f;

        private static Photon.Pun.PhotonView[] _viewCache = new Photon.Pun.PhotonView[0];
        private static float _viewCacheTime;

        private static Material _sharedLineMat;
        private static readonly List<LineRenderer> _lines = new List<LineRenderer>(32);
        private static readonly List<GameObject> _owned = new List<GameObject>(32);

        public static VRRig[] Rigs()
        {
            if (Time.unscaledTime - _rigCacheTime > RigCacheTtl || _rigCache == null)
            {
                _rigCacheTime = Time.unscaledTime;
                try { _rigCache = Object.FindObjectsOfType<VRRig>(); }
                catch { _rigCache = new VRRig[0]; }
            }
            return _rigCache ?? new VRRig[0];
        }

        public static Photon.Pun.PhotonView[] Views()
        {
            if (Time.unscaledTime - _viewCacheTime > RigCacheTtl || _viewCache == null)
            {
                _viewCacheTime = Time.unscaledTime;
                try { _viewCache = Object.FindObjectsOfType<Photon.Pun.PhotonView>(); }
                catch { _viewCache = new Photon.Pun.PhotonView[0]; }
            }
            return _viewCache ?? new Photon.Pun.PhotonView[0];
        }

        public static void Invalidate()
        {
            _rigCacheTime = 0f;
            _viewCacheTime = 0f;
        }

        public static Material LineMaterial()
        {
            if (_sharedLineMat == null)
            {
                var sh = Shader.Find("GUI/Text Shader");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                _sharedLineMat = new Material(sh != null ? sh : Shader.Find("Hidden/InternalErrorShader"));
                _sharedLineMat.hideFlags = HideFlags.DontSave;
            }
            return _sharedLineMat;
        }

        public static LineRenderer GetOrCreateLine(ref LineRenderer lr, string name, Color start, Color end, float width = 0.015f)
        {
            if (lr != null) return lr;
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.material = LineMaterial();
            lr.startColor = start;
            lr.endColor = end;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            _lines.Add(lr);
            _owned.Add(go);
            return lr;
        }

        public static void SetLineActive(LineRenderer lr, bool on)
        {
            if (lr == null) return;
            if (lr.enabled != on) lr.enabled = on;
            if (lr.gameObject != null && lr.gameObject.activeSelf != on)
                lr.gameObject.SetActive(on);
        }

        /// <summary>Call on menu unload / plugin disable if ever needed.</summary>
        public static void DisposeAll()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null) Object.Destroy(_owned[i]);
            }
            _owned.Clear();
            _lines.Clear();
            if (_sharedLineMat != null)
            {
                Object.Destroy(_sharedLineMat);
                _sharedLineMat = null;
            }
            _rigCache = null;
            _viewCache = null;
        }
    }
}
