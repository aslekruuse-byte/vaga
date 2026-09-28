using Photon.Realtime;
using Photon.Pun;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using GorillaNetworking;
using Vaga.Mods;

namespace Vaga.Classes
{
    internal class RigManager : BaseUnityPlugin
    {
        public static VRRig GetVRRigFromPlayer(Player p)
        {
            return GorillaGameManager.instance.FindPlayerVRRig(p);
        }
        public static bool RigIsInfected(VRRig rig)
        {
            bool result;
            if ((UnityEngine.Object)(object)rig == (UnityEngine.Object)null || !PhotonNetwork.InRoom || (UnityEngine.Object)(object)GorillaGameManager.instance == (UnityEngine.Object)null)
            {
                result = false;
            }
            else
            {
                NetPlayer netPlayerFromVRRig = RigManager.GetNetPlayerFromVRRig(rig);
                if (netPlayerFromVRRig == null)
                {
                    result = false;
                }
                else
                {
                    int num = (int)((int)GorillaGameManager.instance.GameType());
                    num = num - (num - 12) * (((uint)num > 11u) ? 1 : 0) + 57;
                    int num2 = num;
                    if (num2 != 58)
                    {
                        result = false;
                    }
                    else
                    {
                        GorillaGameManager instance = GorillaGameManager.instance;
                        GorillaTagManager val = (GorillaTagManager)(object)((instance is GorillaTagManager) ? instance : null);
                        if (val != null)
                        {
                            result = (val.isCurrentlyTag ? (val.currentIt == netPlayerFromVRRig) : (val.currentInfected?.Contains(netPlayerFromVRRig) ?? false));
                        }
                        else
                        {
                            SkinnedMeshRenderer mainSkin = rig.mainSkin;
                            if ((UnityEngine.Object)(object)((mainSkin != null) ? ((Renderer)mainSkin).material : null) == (UnityEngine.Object)null)
                            {
                                result = false;
                            }
                            else
                            {
                                string name = ((UnityEngine.Object)((Renderer)rig.mainSkin).material).name;
                                result = name.Contains("fected") || name.Contains("It");
                            }
                        }
                    }
                }
            }
            return result;
        }
        public static VRRig GetVRRigFromNetPlayer(NetPlayer netPlayer)
        {
            return (netPlayer == null) ? null : GorillaGameManager.StaticFindRigForPlayer(netPlayer);
        }
        public static NetPlayer GetNetPlayerFromVRRig(VRRig vrrig)
        {
            return vrrig.Creator ?? vrrig.OwningNetPlayer ?? NetworkSystem.Instance.GetPlayer(NetworkSystem.Instance.GetOwningPlayerID(((Component)vrrig).gameObject));
        }
        public static NetworkView GetNetworkViewFromVRRig(VRRig vrrig)
        {
            return ((UnityEngine.Object)(object)vrrig == (UnityEngine.Object)null) ? null : vrrig.GetComponent<NetworkView>();
        }
        public static VRRig GetRandomVRRig(bool includeSelf)
        {
            VRRig random = VRRigCache.ActiveRigs[UnityEngine.Random.Range(0, VRRigCache.ActiveRigs.Count - 1)];
            if (includeSelf)
            {
                return random;
            }
            else
            {
                if (random != GorillaTagger.Instance.offlineVRRig)
                {
                    return random;
                }
                else
                {
                    return GetRandomVRRig(includeSelf);
                }
            }
        }

        public static VRRig GetClosestVRRig()
        {
            float num = float.MaxValue;
            VRRig outRig = null;
            foreach (VRRig vrrig in VRRigCache.ActiveRigs)
            {
                if (Vector3.Distance(GorillaTagger.Instance.bodyCollider.transform.position, vrrig.transform.position) < num)
                {
                    num = Vector3.Distance(GorillaTagger.Instance.bodyCollider.transform.position, vrrig.transform.position);
                    outRig = vrrig;
                }
            }
            return outRig;
        }

        public static PhotonView GetPhotonViewFromVRRig(VRRig p)
        {
            return (PhotonView)Traverse.Create(p).Field("photonView").GetValue();
        }

        public static Photon.Realtime.Player GetRandomPlayer(bool includeSelf)
        {
            if (includeSelf)
            {
                return PhotonNetwork.PlayerList[UnityEngine.Random.Range(0, PhotonNetwork.PlayerList.Length - 1)];
            }
            else
            {
                return PhotonNetwork.PlayerListOthers[UnityEngine.Random.Range(0, PhotonNetwork.PlayerListOthers.Length - 1)];
            }
        }

        public static Photon.Realtime.Player GetPlayerFromVRRig(VRRig p)
        {
            try
            {
                var view = GetPhotonViewFromVRRig(p);
                return view != null ? view.Owner : null;
            }
            catch { return null; }
        }

        public static Photon.Realtime.Player GetPlayerFromID(string id)
        {
            Photon.Realtime.Player found = null;
            foreach (Photon.Realtime.Player target in PhotonNetwork.PlayerList)
            {
                if (target.UserId == id)
                {
                    found = target;
                    break;
                }
            }
            return found;
        }
        public static Photon.Realtime.Player GetPlayerFromVRRig1(VRRig p)
        {  
            return GetPhotonViewFromVRRig(p).Owner;
        }
        public static Color GetPlayerColor(VRRig Player)
        {
            if (Player.bodyRenderer.bodyType == GorillaBodyType.Skeleton)
                return Color.green;

            switch (Player.setMatIndex)
            {
                case 1:
                    return Color.red;
                case 2:
                case 11:
                    return new Color32(255, 128, 0, 255);
                case 3:
                case 7:
                    return Color.blue;
                case 12:
                    return Color.green;
                default:
                    return Player.playerColor;
            }
        }
    }
}