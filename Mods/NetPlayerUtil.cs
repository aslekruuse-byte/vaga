using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Vaga.Mods
{
    /// <summary>
    /// GT: VRRig.Creator is NetPlayer. Photon APIs want Photon.Realtime.Player.
    /// Resolve via PhotonView.Owner first, else ActorNumber match on PlayerList.
    /// </summary>
    public static class NetPlayerUtil
    {
        public static Player ToPlayer(VRRig rig)
        {
            if (rig == null) return null;

            try
            {
                var view = rig.GetComponent<PhotonView>();
                if (view != null && view.Owner != null)
                    return view.Owner;
            }
            catch { }

            try
            {
                var creator = rig.Creator;
                if (creator == null) return null;

                int actor = -1;
                try { actor = creator.ActorNumber; } catch { }
                if (actor < 0) return null;

                return FindByActor(actor);
            }
            catch { }

            return null;
        }

        public static Player ToPlayer(object maybePlayerOrNet)
        {
            if (maybePlayerOrNet == null) return null;

            if (maybePlayerOrNet is Player)
                return (Player)maybePlayerOrNet;

            try
            {
                int actor = -1;
                var prop = maybePlayerOrNet.GetType().GetProperty("ActorNumber");
                if (prop != null)
                    actor = (int)prop.GetValue(maybePlayerOrNet);
                if (actor < 0) return null;
                return FindByActor(actor);
            }
            catch { }
            return null;
        }

        private static Player FindByActor(int actor)
        {
            if (PhotonNetwork.PlayerList == null) return null;
            foreach (var p in PhotonNetwork.PlayerList)
                if (p != null && p.ActorNumber == actor)
                    return p;
            return null;
        }

        public static string Nick(VRRig rig)
        {
            var p = ToPlayer(rig);
            if (p != null && !string.IsNullOrEmpty(p.NickName)) return p.NickName;
            try
            {
                var c = rig?.Creator;
                if (c != null)
                {
                    try { return c.NickName; } catch { }
                    var n = c.GetType().GetProperty("NickName")?.GetValue(c) as string;
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }
            catch { }
            return null;
        }
    }
}
