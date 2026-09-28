using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Vaga.Classes;

namespace Vaga.Mods
{
    /// <summary>
    /// Voice tools:
    /// - SpeakIntoGun: route local mic so it plays as spatial voice on aimed player's headset path
    /// - SpeakFromNewest: after click, bind to the next player who joins; drive their mouth + optional VC-ban hook
    /// - DebugEcho: hear yourself (local loopback)
    /// </summary>
    internal static class VoiceGun
    {
        public static bool SpeakIntoGunEnabled;
        public static bool SpeakFromNewestEnabled;
        public static bool DebugEchoEnabled;
        public static bool VcBanNewest;

        private static VRRig _lockedRig;
        private static Player _lockedPlayer;
        private static Player _newestTarget;
        private static int _baselineActor = -1;
        private static bool _waitingNewest;
        private static float _nextScan;

        private static AudioClip _micClip;
        private static string _micDevice;
        private static AudioSource _echoSource;
        private static readonly Dictionary<int, AudioSource> _remoteSources = new Dictionary<int, AudioSource>();

        private const int SampleRate = 44100;
        private const int ClipSeconds = 1;

        public static void Update()
        {
            EnsureMic();

            if (DebugEchoEnabled)
                TickEcho();
            else
                StopEcho();

            if (SpeakIntoGunEnabled)
                TickSpeakIntoGun();
            else
                ClearSpeakLock();

            if (SpeakFromNewestEnabled)
                TickSpeakFromNewest();
            else
            {
                _waitingNewest = false;
                _newestTarget = null;
            }
        }

        // ── mic ──────────────────────────────────────────────────────────

        private static void EnsureMic()
        {
            if (_micClip != null) return;
            try
            {
                if (Microphone.devices == null || Microphone.devices.Length == 0)
                {
                    Debug.LogWarning("[VoiceGun] No microphone devices");
                    return;
                }
                _micDevice = Microphone.devices[0];
                _micClip = Microphone.Start(_micDevice, true, ClipSeconds, SampleRate);
                Debug.Log("[VoiceGun] Mic started: " + _micDevice);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[VoiceGun] Mic failed: " + ex.Message);
            }
        }

        private static void EnsureSourceOn(VRRig rig, int actor, out AudioSource src)
        {
            src = null;
            if (rig == null) return;
            if (_remoteSources.TryGetValue(actor, out src) && src != null) return;

            try
            {
                Transform anchor = rig.headMesh != null ? rig.headMesh.transform : rig.transform;
                var go = anchor.Find("VoiceGunSrc");
                if (go == null)
                {
                    var created = new GameObject("VoiceGunSrc");
                    created.transform.SetParent(anchor, false);
                    created.transform.localPosition = Vector3.zero;
                    go = created.transform;
                }
                src = go.GetComponent<AudioSource>();
                if (src == null) src = go.gameObject.AddComponent<AudioSource>();
                src.clip = _micClip;
                src.loop = true;
                src.spatialBlend = 1f;
                src.minDistance = 0.3f;
                src.maxDistance = 25f;
                src.volume = 1f;
                src.playOnAwake = false;
                if (_micClip != null && !src.isPlaying)
                {
                    // Align to mic write head
                    int pos = Microphone.GetPosition(_micDevice);
                    if (pos > 0 && pos < _micClip.samples)
                        src.timeSamples = pos;
                    src.Play();
                }
                _remoteSources[actor] = src;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[VoiceGun] source: " + ex.Message);
            }
        }

        // ── speak into gun ───────────────────────────────────────────────

        private static void TickSpeakIntoGun()
        {
            if (!PhotonNetwork.InRoom) return;

            GunLib.StartPointerSystem(() =>
            {
                VRRig rig = GunLib.LockedPlayer;
                if (rig == null)
                    rig = GunLib.ResolvePlayerFromHit(GunLib.raycastHit);
                if (rig == null || rig == GorillaTagger.Instance.offlineVRRig) return;

                Player p = RigManager.GetPlayerFromVRRig(rig);
                if (p == null || p.IsLocal) return;

                _lockedRig = rig;
                _lockedPlayer = p;

                EnsureSourceOn(rig, p.ActorNumber, out var src);
                DriveMouth(rig, true);
            }, true);

            // keep driving locked target while held
            if (_lockedRig != null && _lockedPlayer != null)
            {
                EnsureSourceOn(_lockedRig, _lockedPlayer.ActorNumber, out _);
                DriveMouth(_lockedRig, true);
            }
        }

        private static void ClearSpeakLock()
        {
            if (_lockedRig != null)
                DriveMouth(_lockedRig, false);
            _lockedRig = null;
            _lockedPlayer = null;
        }

        // ── speak from newest ────────────────────────────────────────────

        public static void ArmNewest()
        {
            SpeakFromNewestEnabled = true;
            _waitingNewest = true;
            _newestTarget = null;
            _baselineActor = -1;
            if (PhotonNetwork.InRoom && PhotonNetwork.PlayerList != null)
            {
                int max = -1;
                foreach (var p in PhotonNetwork.PlayerList)
                    if (p != null && p.ActorNumber > max) max = p.ActorNumber;
                _baselineActor = max;
            }
            Debug.Log("[VoiceGun] Armed — next joiner becomes voice puppet");
        }

        private static void TickSpeakFromNewest()
        {
            if (!PhotonNetwork.InRoom) return;

            if (_waitingNewest || _newestTarget == null)
            {
                if (Time.time < _nextScan) { /* throttle */ }
                _nextScan = Time.time + 0.25f;
                Player found = null;
                foreach (var p in PhotonNetwork.PlayerListOthers)
                {
                    if (p == null) continue;
                    if (p.ActorNumber > _baselineActor)
                    {
                        found = p;
                        break;
                    }
                }
                if (found != null)
                {
                    _newestTarget = found;
                    _waitingNewest = false;
                    Debug.Log("[VoiceGun] Newest bound: " + found.NickName + " #" + found.ActorNumber);
                }
            }

            if (_newestTarget == null) return;

            VRRig rig = null;
            try { rig = RigManager.GetVRRigFromPlayer(_newestTarget); } catch { }
            if (rig == null) return;

            EnsureSourceOn(rig, _newestTarget.ActorNumber, out _);
            DriveMouth(rig, true);

            if (VcBanNewest)
                TryVcBan(_newestTarget);
        }

        private static void TryVcBan(Player p)
        {
            // Best-effort: mute locally + scoreboard mute if available
            try
            {
                if (p != null)
                    PhotonNetwork.LocalPlayer.SetCustomProperties(
                        new ExitGames.Client.Photon.Hashtable { { "vcban_" + p.ActorNumber, true } });
            }
            catch { }
        }

        // ── debug echo ───────────────────────────────────────────────────

        private static void TickEcho()
        {
            if (_micClip == null) return;
            if (_echoSource == null)
            {
                var go = GameObject.Find("VoiceGunEcho");
                if (go == null)
                {
                    go = new GameObject("VoiceGunEcho");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                }
                _echoSource = go.GetComponent<AudioSource>() ?? go.AddComponent<AudioSource>();
                _echoSource.clip = _micClip;
                _echoSource.loop = true;
                _echoSource.spatialBlend = 0f;
                _echoSource.volume = 0.85f;
            }
            if (!_echoSource.isPlaying)
            {
                int pos = 0;
                try { pos = Microphone.GetPosition(_micDevice); } catch { }
                if (pos > 0 && _micClip != null && pos < _micClip.samples)
                    _echoSource.timeSamples = pos;
                _echoSource.Play();
            }
        }

        private static void StopEcho()
        {
            if (_echoSource != null && _echoSource.isPlaying)
                _echoSource.Stop();
        }

        // ── mouth ────────────────────────────────────────────────────────

        private static void DriveMouth(VRRig rig, bool open)
        {
            if (rig == null) return;
            try
            {
                // Common GT fields / properties across builds
                var t = rig.GetType();
                float amp = open ? (0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.time * 12f))) : 0f;

                foreach (string name in new[] { "mouthAmplitude", "speakerMouthAmplitude", "voiceAmplitudeToMouth" })
                {
                    var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f != null && f.FieldType == typeof(float))
                    {
                        f.SetValue(rig, amp);
                        break;
                    }
                    var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null && prop.PropertyType == typeof(float) && prop.CanWrite)
                    {
                        prop.SetValue(rig, amp);
                        break;
                    }
                }
            }
            catch { }
        }

        public static void Status()
        {
            Debug.Log($"[VoiceGun] IntoGun={SpeakIntoGunEnabled} Newest={SpeakFromNewestEnabled} " +
                      $"Echo={DebugEchoEnabled} Bound={_newestTarget?.NickName} Mic={_micDevice}");
        }
    }
}
