using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using UnityEngine;
using Vaga;
using Vaga.Classes;
using Random = UnityEngine.Random;

namespace Vaga.Mods
{
    /// <summary>
    /// BanGun — gun-targeted "ban" actions on a player:
    /// mass report (scoreboard HateSpeech), CloseConnection kick (if master),
    /// and kick-style RaiseEvents. BanAll hits every other player in the room.
    /// </summary>
    internal static class BanGun
    {
        public static bool Enabled;

        public static float FireRate = 0.08f;
        public static int EventBurst = 8;

        public static bool UseScoreboardReport = true;
        public static bool UseCloseConnection = true;   // requires master
        public static bool UseKickEvents = true;
        public static bool AutoTakeMaster = true;

        private static float _nextFire;

        public static void Update()
        {
            if (!Enabled || !PhotonNetwork.InRoom) return;

            if (AutoTakeMaster && !PhotonNetwork.IsMasterClient)
            {
                try { PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer); }
                catch { }
            }

            GunLib.StartPointerSystem(() =>
            {
                if (Time.time < _nextFire) return;
                _nextFire = Time.time + FireRate;

                VRRig targetRig = GunLib.LockedPlayer;
                if (targetRig == null)
                    targetRig = GunLib.ResolvePlayerFromHit(GunLib.raycastHit);

                if (targetRig == null || targetRig == GorillaTagger.Instance.offlineVRRig)
                    return;

                Player target = RigManager.GetPlayerFromVRRig(targetRig);
                if (target == null || target.IsLocal) return;

                BanPlayer(target, targetRig);
            }, true);
        }

        public static void BanPlayer(Player target, VRRig rig = null)
        {
            if (target == null || target.IsLocal) return;

            if (UseScoreboardReport)
                ReportPlayer(target);

            if (UseCloseConnection && PhotonNetwork.IsMasterClient)
            {
                try { PhotonNetwork.CloseConnection(target); }
                catch { }
            }

            if (UseKickEvents)
                KickEventBurst(target, rig);
        }

        /// <summary>Ban every other player in the room.</summary>
        public static void BanAll()
        {
            if (!PhotonNetwork.InRoom) return;

            if (AutoTakeMaster && !PhotonNetwork.IsMasterClient)
            {
                try { PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer); }
                catch { }
            }

            foreach (Player p in PhotonNetwork.PlayerListOthers)
            {
                if (p == null || p.IsLocal) continue;
                VRRig rig = null;
                try { rig = RigManager.GetVRRigFromPlayer(p); } catch { }
                BanPlayer(p, rig);
            }
        }

        // ── report via scoreboard lines ──────────────────────────────────

        private static void ReportPlayer(Player target)
        {
            try
            {
                GorillaPlayerScoreboardLine[] lines =
                    UnityEngine.Object.FindObjectsOfType<GorillaPlayerScoreboardLine>();
                if (lines == null) return;

                foreach (var line in lines)
                {
                    if (line == null || line.linePlayer == null) continue;
                    // Match by actor / userid / nick
                    bool match = false;
                    try
                    {
                        if (line.linePlayer.ActorNumber == target.ActorNumber) match = true;
                        else if (!string.IsNullOrEmpty(target.UserId) &&
                                 line.linePlayer.UserId == target.UserId) match = true;
                    }
                    catch { }

                    if (!match) continue;

                    // HateSpeech is the common report button type in GT
                    try
                    {
                        line.PressButton(true, GorillaPlayerLineButton.ButtonType.HateSpeech);
                    }
                    catch
                    {
                        // Fallback: try integer report-ish values
                        try { line.PressButton(true, (GorillaPlayerLineButton.ButtonType)1); } catch { }
                        try { line.PressButton(true, (GorillaPlayerLineButton.ButtonType)2); } catch { }
                    }
                }
            }
            catch { }
        }

        // ── kick-style events ────────────────────────────────────────────

        private static void KickEventBurst(Player target, VRRig rig)
        {
            try
            {
                RaiseEventOptions opts = new RaiseEventOptions
                {
                    TargetActors = new[] { target.ActorNumber },
                    Receivers = ReceiverGroup.Others,
                    CachingOption = EventCaching.DoNotCache
                };

                for (int i = 0; i < EventBurst; i++)
                {
                    object[] payload = new object[]
                    {
                        target.ActorNumber,
                        PhotonNetwork.LocalPlayer.ActorNumber,
                        "ban",
                        target.UserId ?? "",
                        Random.Range(int.MinValue, int.MaxValue),
                        new byte[64]
                    };

                    PhotonNetwork.RaiseEvent(200, payload, opts, SendOptions.SendReliable);
                    PhotonNetwork.RaiseEvent(203, new object[] { target.UserId, true }, opts, SendOptions.SendReliable);
                    PhotonNetwork.RaiseEvent(254, payload, opts, SendOptions.SendUnreliable); // leave-style
                }

                // Ownership thrash if we have a rig
                if (rig != null)
                {
                    try
                    {
                        PhotonView view = RigManager.GetPhotonViewFromVRRig(rig);
                        if (view != null)
                        {
                            view.RequestOwnership();
                            PhotonNetwork.RaiseEvent(209,
                                new object[] { view.ViewID, PhotonNetwork.LocalPlayer.ActorNumber },
                                opts, SendOptions.SendReliable);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static void BanClosest()
        {
            if (!PhotonNetwork.InRoom) return;
            VRRig closest = RigManager.GetClosestVRRig();
            if (closest == null || closest == GorillaTagger.Instance.offlineVRRig) return;
            Player p = RigManager.GetPlayerFromVRRig(closest);
            if (p == null) return;
            BanPlayer(p, closest);
        }

        public static void Status()
        {
            Debug.Log($"[BanGun] Enabled={Enabled} Report={UseScoreboardReport} " +
                      $"CloseConn={UseCloseConnection} KickEvents={UseKickEvents} " +
                      $"Master={PhotonNetwork.IsMasterClient}");
        }
    }
}
