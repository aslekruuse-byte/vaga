using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Vaga.Patches.Internal;

namespace Vaga.Mods
{
    /// <summary>
    /// AntiBan — disables / neuters client-side anti-cheat, report, telemetry,
    /// and force-disconnect paths used by Gorilla Tag / MonkeAgent / PlayFab.
    /// Toggleable. All heavy lifting is Harmony Prefix return-false.
    /// </summary>
    internal static class AntiBan
    {
        public static bool Enabled = true;

        // Sub-toggles
        public static bool BlockReports = true;
        public static bool BlockTelemetry = true;
        public static bool BlockForceDisconnect = true;
        public static bool BlockQuitOnBan = true;
        public static bool SpoofDisplayName = false;
        public static bool MasterClientLock = false;   // optional room lockdown
        public static bool BlockScoreboardReport = true;

        private static bool _applied;
        private static Harmony _harmony;

        public static void Enable()
        {
            Enabled = true;
            TelemetryPatches.enabled = true;
            ApplyIfNeeded();
            Debug.Log("[AntiBan] Enabled");
        }

        public static void Disable()
        {
            Enabled = false;
            TelemetryPatches.enabled = false;
            // Harmony patches stay applied; Prefixes check Enabled where needed.
            // Full unpatch is optional and more aggressive — left intact for stability.
            Debug.Log("[AntiBan] Disabled (patches remain, gates closed)");
        }

        public static void ApplyIfNeeded()
        {
            if (_applied) return;
            _harmony = new Harmony("vaga.antiban");
            try
            {
                // Existing internal patch classes are already applied by PatchHandler.
                // Extra runtime patches for scoreboard / report buttons.
                PatchScoreboardReports();
                _applied = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AntiBan] Apply failed: {ex}");
            }
        }

        private static void PatchScoreboardReports()
        {
            // Neutralize report button presses on scoreboard lines
            Type lineType = typeof(GorillaPlayerScoreboardLine);
            MethodInfo press = AccessTools.Method(lineType, "PressButton");
            if (press != null)
            {
                _harmony.Patch(press, prefix: new HarmonyMethod(typeof(AntiBan), nameof(PressButtonPrefix)));
            }
        }

        private static bool PressButtonPrefix(GorillaPlayerLineButton.ButtonType buttonType)
        {
            if (!Enabled || !BlockScoreboardReport) return true;
            // Block report-style buttons (HateSpeech + numeric fallbacks)
            try
            {
                if (buttonType == GorillaPlayerLineButton.ButtonType.HateSpeech)
                    return false;
            }
            catch { }
            // Enum integer fallback for report variants across GT versions
            int v = (int)buttonType;
            if (v == 1 || v == 2 || v == 3)
                return false;
            return true;
        }

        /// <summary>
        /// Call every frame / from menu method when AntiBan is toggled on.
        /// Handles optional master-client lockdown.
        /// </summary>
        public static void Update()
        {
            if (!Enabled) return;

            if (MasterClientLock && PhotonNetwork.InRoom)
            {
                if (!PhotonNetwork.IsMasterClient)
                {
                    try
                    {
                        // Attempt to take master (may be rate-limited / detected by other clients)
                        PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// One-shot: force private + master + kick others (aggressive anti-report room lock).
        /// Use only in private / testing contexts.
        /// </summary>
        public static void LockRoomAggressive()
        {
            if (!PhotonNetwork.InRoom) return;
            try
            {
                PhotonNetwork.CurrentRoom.IsVisible = false;
                PhotonNetwork.CurrentRoom.IsOpen = false;
                if (!PhotonNetwork.IsMasterClient)
                    PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer);

                // Optional: remove other players so reports cannot be filed from inside
                foreach (Player p in PhotonNetwork.PlayerListOthers)
                {
                    PhotonNetwork.CloseConnection(p);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AntiBan] LockRoomAggressive: {ex.Message}");
            }
        }

        public static void Status()
        {
            string s = $"[AntiBan] Enabled={Enabled} Reports={BlockReports} Telemetry={BlockTelemetry} " +
                       $"ForceDC={BlockForceDisconnect} QuitOnBan={BlockQuitOnBan} " +
                       $"Scoreboard={BlockScoreboardReport} MasterLock={MasterClientLock}";
            Debug.Log(s);
            // If NotifiLib exists in project it can be hooked here
        }
    }
}
