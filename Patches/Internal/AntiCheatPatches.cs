using HarmonyLib;
using UnityEngine;

namespace Vaga.Patches.Internal
{
    /// <summary>
    /// Expanded AntiCheat / AntiBan patches.
    /// All Prefixes return false to short-circuit the original method.
    /// Covers MonkeAgent report pipeline, force-disconnect, quit-on-ban,
    /// RPC tracking, and public-test grace period paths.
    /// </summary>
    public class AntiCheatPatches
    {
        [HarmonyPatch(typeof(MonkeAgent), "SendReport")]
        public class SendReportPatch
        {
            private static bool Prefix(string susReason, string susId, string susNick) => false;
        }

        [HarmonyPatch(typeof(MonkeAgent), "CheckReports")]
        public class NoCheckReports
        {
            private static bool Prefix() => false;
        }

        [HarmonyPatch(typeof(MonkeAgent), "DispatchReport")]
        public class NoDispatchReport
        {
            private static bool Prefix() => false;
        }

        [HarmonyPatch(typeof(MonkeAgent), "CloseInvalidRoom")]
        public class NoCloseInvalidRoom
        {
            private static bool Prefix() => false;
        }

        [HarmonyPatch(typeof(MonkeAgent), "ShouldDisconnectFromRoom")]
        public class NoShouldDisconnectFromRoom
        {
            private static bool Prefix(ref bool __result)
            {
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(MonkeAgent), "QuitDelay", MethodType.Enumerator)]
        public class NoQuitDelay
        {
            private static bool Prefix() => false;
        }

        [HarmonyPatch(typeof(GorillaGameManager), "ForceStopGame_DisconnectAndDestroy")]
        public class NoQuitOnBan
        {
            private static bool Prefix() => false;
        }

        [HarmonyPatch(typeof(MonkeAgent), "GetRPCCallTracker")]
        internal class NoGetRPCCallTracker
        {
            private static bool Prefix() => false;
        }

        [HarmonyPatch(typeof(MonkeAgent), "LogErrorCount")]
        public class NoLogErrorCount
        {
            private static bool Prefix(string logString, string stackTrace, LogType type) => false;
        }

        [HarmonyPatch(typeof(GorillaNetworkPublicTestsJoin), "GracePeriod")]
        public class GracePeriodPatch1
        {
            private static bool Prefix() => false;
        }

        [HarmonyPatch(typeof(GorillaNetworkPublicTestJoin2), "GracePeriod")]
        public class GracePeriodPatch2
        {
            private static bool Prefix() => false;
        }
    }
}
