using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Vaga.Mods
{
    /// <summary>
    /// AntiMute — mute BYPASS:
    /// - Clears local mute paths (client)
    /// - Writes replicated actor + room voice props so the room state says unmuted
    /// - If master (or after taking master), strips mute flags for self from room-level tables
    /// - Broadcasts RaiseEvent voice-unmute so peers that listen re-enable speaker
    ///
    /// Does not override a foreign client's personal scoreboard mute of you.
    /// Does not lift official moderation VC bans on public infra.
    /// </summary>
    internal static class AntiMute
    {
        public static bool Enabled = true;
        public static bool TakeMasterIfNeeded = true;
        public static bool BroadcastUnmuteEvent = true;
        public static bool WriteRoomVoiceTable = true;

        private static Harmony _harmony;
        private static bool _patched;
        private static float _nextForce;
        private static float _nextBroadcast;

        // Event code for peer unmute hint (custom; ignored if unused)
        private const byte UnmuteEventCode = 176;

        public static void Enable()
        {
            Enabled = true;
            EnsurePatches();
            ForceUnmute();
            ServerUnmute();
            Debug.Log("[AntiMute] Enabled — mute bypass active");
        }

        public static void Disable()
        {
            Enabled = false;
            Debug.Log("[AntiMute] Disabled");
        }

        public static void Update()
        {
            if (!Enabled) return;
            EnsurePatches();
            if (Time.time >= _nextForce)
            {
                _nextForce = Time.time + 0.15f; // aggressive bypass re-assert
                ForceUnmute();
                ServerUnmute();
            }
        }

        /// <summary>Full pass: local + replicated room/actor state.</summary>
        public static void ForceUnmute()
        {
            LocalUnmute();
            ServerUnmute();
        }

        // ── replicated / room authority ──────────────────────────────────

        public static void ServerUnmute()
        {
            if (!PhotonNetwork.InRoom) return;

            try
            {
                if (TakeMasterIfNeeded && !PhotonNetwork.IsMasterClient)
                {
                    try { PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer); }
                    catch { }
                }
            }
            catch { }

            int actor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
            string uid = PhotonNetwork.LocalPlayer?.UserId ?? "";

            // 1) Actor custom properties (replicated)
            try
            {
                var actorProps = new Hashtable
                {
                    { "muted", false },
                    { "autoMuted", false },
                    { "voiceMuted", false },
                    { "isMuted", false },
                    { "mute", false },
                    { "vcmute", false },
                    { "vrMute", false },
                    { "am_unmute", PhotonNetwork.ServerTimestamp }
                };
                PhotonNetwork.LocalPlayer.SetCustomProperties(actorProps);
            }
            catch { }

            // 2) Room properties — voice table / mute list style keys
            if (WriteRoomVoiceTable)
            {
                try
                {
                    var room = PhotonNetwork.CurrentRoom;
                    if (room != null)
                    {
                        var rp = new Hashtable
                        {
                            { "mute_" + actor, false },
                            { "muted_" + actor, false },
                            { "voiceMute_" + actor, false },
                            { "am_voice_" + actor, 1 },
                            { "am_ts", PhotonNetwork.ServerTimestamp }
                        };
                        if (!string.IsNullOrEmpty(uid))
                        {
                            rp["mute_" + uid] = false;
                            rp["muted_" + uid] = false;
                        }

                        // If master, also rewrite a combined mute map when present
                        if (PhotonNetwork.IsMasterClient)
                        {
                            rp["serverMuted"] = false;
                            rp["globalMute"] = false;
                            rp["voiceBan"] = false;
                        }

                        room.SetCustomProperties(rp);
                    }
                }
                catch { }
            }

            // 3) Broadcast unmute hint to all peers
            if (BroadcastUnmuteEvent && Time.time >= _nextBroadcast)
            {
                _nextBroadcast = Time.time + 1.0f;
                try
                {
                    object[] payload =
                    {
                        actor,
                        uid,
                        false, // muted
                        PhotonNetwork.ServerTimestamp
                    };
                    var opt = new RaiseEventOptions
                    {
                        Receivers = ReceiverGroup.All,
                        CachingOption = EventCaching.DoNotCache
                    };
                    PhotonNetwork.RaiseEvent(UnmuteEventCode, payload, opt, SendOptions.SendReliable);
                }
                catch { }
            }

            // 4) Master: clear mute on scoreboard lines for local player for everyone if we can drive
            if (PhotonNetwork.IsMasterClient)
                MasterClearLocalMuteLines();
        }

        private static void MasterClearLocalMuteLines()
        {
            try
            {
                var lines = UnityEngine.Object.FindObjectsOfType<GorillaPlayerScoreboardLine>();
                if (lines == null) return;
                foreach (var line in lines)
                {
                    if (line == null || line.linePlayer == null) continue;
                    if (!line.linePlayer.IsLocal && line.linePlayer.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
                        continue;
                    try
                    {
                        foreach (string field in new[] { "mute", "isMuted", "muted" })
                        {
                            var f = line.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (f != null && f.FieldType == typeof(bool))
                                f.SetValue(line, false);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ── local ────────────────────────────────────────────────────────

        private static void LocalUnmute()
        {
            try
            {
                var tagger = GorillaTagger.Instance;
                if (tagger == null) return;
                var t = tagger.GetType();
                foreach (string name in new[] { "myRecorder", "recorder", "voiceRecorder" })
                {
                    var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f == null) continue;
                    var rec = f.GetValue(tagger);
                    if (rec == null) continue;
                    var rt = rec.GetType();
                    foreach (string pn in new[] { "TransmitEnabled", "transmitEnabled", "RecordingEnabled" })
                    {
                        var prop = rt.GetProperty(pn);
                        if (prop != null && prop.CanWrite) prop.SetValue(rec, true);
                    }
                    foreach (string pn in new[] { "IsMuted", "Muted", "muted" })
                    {
                        var prop = rt.GetProperty(pn);
                        if (prop != null && prop.CanWrite) prop.SetValue(rec, false);
                    }
                }
            }
            catch { }

            try
            {
                var lines = UnityEngine.Object.FindObjectsOfType<GorillaPlayerScoreboardLine>();
                if (lines == null) return;
                foreach (var line in lines)
                {
                    if (line == null || line.linePlayer == null || !line.linePlayer.IsLocal) continue;
                    var f = line.GetType().GetField("mute", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f != null && f.FieldType == typeof(bool)) f.SetValue(line, false);
                }
            }
            catch { }
        }

        private static void EnsurePatches()
        {
            if (_patched) return;
            try
            {
                _harmony ??= new Harmony("kyra.antimutepatch.v2");
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type line = null;
                    try { line = asm.GetType("GorillaPlayerScoreboardLine"); } catch { }
                    if (line == null) continue;
                    foreach (var m in line.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (m.Name == "PressButton" || m.Name == "MutePlayer" || m.Name == "SetMuted")
                        {
                            try
                            {
                                _harmony.Patch(m, prefix: new HarmonyMethod(typeof(AntiMute), nameof(BlockMutePrefix)));
                            }
                            catch { }
                        }
                    }
                    break;
                }
                _patched = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AntiMute] patch: " + ex.Message);
            }
        }

        private static bool BlockMutePrefix()
        {
            // When enabled, skip mute handlers that would mute local player paths
            return !Enabled;
        }

        public static void Status()
        {
            Debug.Log($"[AntiMute] On={Enabled} Master={PhotonNetwork.IsMasterClient} " +
                      $"TakeMaster={TakeMasterIfNeeded} RoomWrite={WriteRoomVoiceTable} " +
                      $"Broadcast={BroadcastUnmuteEvent} Patched={_patched}");
        }
    }
}
