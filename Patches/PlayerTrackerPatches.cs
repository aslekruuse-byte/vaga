using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Vaga.Mods;

namespace Vaga.Patches
{
    [HarmonyPatch(typeof(MonoBehaviourPunCallbacks), "OnPlayerEnteredRoom")]
    public class TrackerJoinPatch
    {
        private static void Prefix(Player newPlayer)
        {
            try
            {
                PlayerTracker.OnPlayerJoined(newPlayer);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PlayerTracker] Join patch error: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(MonoBehaviourPunCallbacks), "OnPlayerLeftRoom")]
    public class TrackerLeavePatch
    {
        private static void Prefix(Player otherPlayer)
        {
            try
            {
                PlayerTracker.OnPlayerLeft(otherPlayer);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PlayerTracker] Leave patch error: " + e.Message);
            }
        }
    }
}
