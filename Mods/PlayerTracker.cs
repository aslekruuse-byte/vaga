using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Vaga.Classes;
using Vaga.Notifications;

namespace Vaga.Mods
{
    internal static class PlayerTracker
    {
        public static bool Enabled = false;
        public static bool LogJoins = true;
        public static bool LogLeaves = true;
        public static bool HighlightKnown = true;
        public static bool ShowDistance = true;
        public static bool AutoNotifyKnown = true;

        // Lobby hop scanner
        public static bool HopEnabled = false;
        public static float HopDelay = 4.5f;          // seconds to stay in lobby before hopping if no known
        public static float JoinSettleTime = 2.0f;    // wait after join before scanning
        public static bool StopOnFound = true;        // stop hopping when known player found
        public static bool HopOnlyEmptyish = false;

        private static float _lastScan;
        private static float _roomEnterTime;
        private static float _nextHopTime;
        private static bool _scannedThisRoom;
        private static string _lastRoomCode = "";
        private static int _hopsThisSession;
        private static readonly HashSet<int> _notifiedActors = new HashSet<int>();
        private static readonly HashSet<string> _foundKnownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static readonly Dictionary<string, string> KnownPlayers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "9DBC90CF7449EF64", "StyledSnail" },
            { "6FE5FF4D5DF68843", "Pine" },
            { "10D31D3BDCCE5B1F", "Deezey" },
            { "BAC5807405123060", "britishmonke" },
            { "A6FFC7318E1301AF", "jmancurly" },
            { "3B9FD2EEF24ACB3", "VMT" },
            { "04005517920EBO", "K9?" },
            { "33FFA45DBFD33B01", "will" },
            { "D6971CA01F82A975", "Elliot" },
            { "636D8846E76C9B5A", "Clown" },
            { "65CB0CCF1AED2BF", "Ethyb" },
            { "48437FE432DE48BE", "BBVR" },
            { "61AD990FF3A423B7", "Boda 1" },
            { "AAB44BFD0BA34829", "Boda 2" },
            { "6713DA80D2E9BFB5", "AHauntedArmy" },
            { "B4A3FF01312B55B1", "Pluto" },
            { "339E0D392565DC39", "kishark" },
            { "F08CE3118F9E793E", "TurboAlligator" },
            { "D6E20BE9655C798", "TTTPIG 1" },
            { "71AA09D13C0F408D", "TTTPIG 2" },
            { "1D6E20BE9655C798", "TTTPIG 3" },
            { "22A7BCEFFD7A0BBA", "TTTPIG 4" },
            { "C3878B068886F6C3", "ZZEN" },
            { "6F79BE7CB34642AC", "CodyO'Quinn" },
            { "5AA1231973BE8A62", "Apollo" },
            { "7F31BEEC604AE189", "ElectronicWall 1" },
            { "42C809327652ECDD", "ElectronicWall 2" },
            { "ECDE8A2FF8510934", "Antoca" },
            { "80279945E7D3B57D", "Jolyne" },
            { "7E44E8337DF02CC1", "Nunya" },
            { "F5B5C64914C13B83", "HatGirl" },
            { "660814E013F31EFA", "HOLLOWZZGT" },
            { "2E408ED946D55D51", "Haunted" },
            { "498D4C2F23853B37", "POGTROLL" },
            { "BC9764E1EADF8BE0", "Circuit" },
            { "D0CB396539676DD8", "FrogIlla" },
            { "A1A99D33645E4A94", "STEAMVRAVTS/YEAT" },
            { "CBCCBBB6C28A94CF", "PTMstar" },
            { "6DC06EEFFE9DBD39", "Lucio" },
            { "4ACA3C76B334B17F", "Wihz" },
            { "571776944B6162F1", "CubCub" },
            { "BC99FA914F506AB8", "Lemming Steam" },
            { "3A16560CA65A51DE", "Lemming Quest" },
            { "59F3FE769DE93AB9", "Lemming Unity" },
            { "EE9FB127CF7DBBD5", "NOTMARK" },
            { "54DCB69545BE0800", "Biffbish" },
            { "A04005517920EB0", "K9" },
            { "5CCCAA8A225A468B", "furina" },
            { "ABD60175B46E45C5", "Saltwater" },
            { "964C4A68F65A804C", "YottaBite" },
            { "4D5EB238C8253D04", "Person" },
            { "B4E45E48C5CE0656", "ZBR" },
            { "8A062E735BBC89ED", "GLTCH" },
            { "A100E9E6C4D91E75", "MYCRAFTS" },
            { "7952F9E08FEF8E83", "MYCRAFTS" },
            { "10E12F25533C13F2", "KIRPI4" },
            { "10621E029A675705", "AA_MIKE" },
            { "F8FF7B812B0B2F72", "FOGGY" },
            { "1E8298E1E1F40CB2", "FAADU" },
            { "289C8FAD58A09D6D", "PIXEL" },
            { "172E4982BEE4A8AD", "H4KPY" },
            { "A339740A8ED97FC2", "COFFEEPERSON" },
            { "502575B001FE6FCD", "MIKEYOURMAN" },
            { "2FB3C7950D2159AF", "CLYDE" },
            { "378D7E14A11734FF", "ERIK1515" },
            { "FD39927817389160", "FOOJ" },
            { "184F12FF07A0BC9E", "CHAOSVR" },
            { "A2EADA9BE5CF6238", "MANJO" },
            { "6790C05198B8F76B", "ITZTAPU" },
            { "F5A7710573B60137", "ZEEP" },
            { "52529F0635BE0CDF", "PapaSmurf" },
            { "28579AFACDE1FB19", "Pepsi Dee" },
            { "DF64F7147DDA2783", "EddieGt" },
            { "D1E4A70539D65788", "Horizon" },
            { "CCB1C711632603E2", "The Doctor" },
            { "7C0234D9AAEA2D04", "StarBoyVR" },
            { "C8D2B81BCA39663D", "The Great Aqua" },
            { "D2E5322571FBE776", "ZoomMeta" },
            { "24654A0098DEC89", "Zellix" },
            { "6DC3706AB3CDCD71", "Ghosty" },
            { "E5C93C594117714D", "OliverSage" },
            { "CCE1DFF266E49868", "MooseVR" },
            { "36FD11C9FB61E50B", "Cryptik" },
            { "93F1FB8CA70CEC7A", "Archie" },
            { "62AF723065924DDF", "Jawdat" },
            { "49711FD5F83DE549", "Juan" },
            { "37A90E7453C556DE", "Shroom" },
            { "D51261E11BBA1099", "FishTaco" },
            { "592FBBCDBED74C11", "Goat" },
            { "1F4C2B7569C0A633", "Razzle" },
            { "6E1B5925485E2540", "TBM Jay" },
            { "35019113279BF26C", "Waterman" },
            { "F1A846A0B44E9C69", "Cat Craze" },
            { "D1B352029BB4F9E3", "Zellix" },
        };

        public static string ResolveName(Player p)
        {
            if (p == null) return "???";
            string uid = GetUserId(p);
            if (!string.IsNullOrEmpty(uid) && KnownPlayers.TryGetValue(uid, out string known))
                return known;
            return p.NickName ?? "Unknown";
        }

        public static string GetUserId(Player p)
        {
            if (p == null) return null;
            if (!string.IsNullOrEmpty(p.UserId))
                return p.UserId;
            if (p.CustomProperties != null)
            {
                if (p.CustomProperties.TryGetValue("playFabId", out object pf) && pf != null)
                    return pf.ToString();
                if (p.CustomProperties.TryGetValue("UserId", out object uid) && uid != null)
                    return uid.ToString();
            }
            return null;
        }

        public static bool IsKnown(Player p)
        {
            string uid = GetUserId(p);
            return !string.IsNullOrEmpty(uid) && KnownPlayers.ContainsKey(uid);
        }

        public static List<Player> GetKnownInRoom()
        {
            var list = new List<Player>();
            if (!PhotonNetwork.InRoom) return list;
            foreach (Player p in PhotonNetwork.PlayerList)
            {
                if (p == null || p.IsLocal) continue;
                if (IsKnown(p)) list.Add(p);
            }
            return list;
        }

        public static void OnPlayerJoined(Player newPlayer)
        {
            if (!Enabled || newPlayer == null || newPlayer.IsLocal) return;

            string display = ResolveName(newPlayer);
            string uid = GetUserId(newPlayer) ?? "no-id";
            bool known = IsKnown(newPlayer);

            if (LogJoins)
            {
                string tag = known
                    ? "<color=grey>[</color><color=yellow>KNOWN JOIN</color><color=grey>]</color>"
                    : "<color=grey>[</color><color=green>JOIN</color><color=grey>]</color>";
                NotifiLib.SendNotification($"{tag} <color=white>{display}</color> <color=grey>({uid})</color>");
            }

            if (known && AutoNotifyKnown)
            {
                _notifiedActors.Add(newPlayer.ActorNumber);
                if (!string.IsNullOrEmpty(uid))
                    _foundKnownIds.Add(uid);
                Debug.Log($"[PlayerTracker] KNOWN: {display} | {uid} | Actor {newPlayer.ActorNumber}");

                if (DiscordWebhook.SendJoins || DiscordWebhook.SendKnownOnly)
                    DiscordWebhook.NotifyKnown(display, uid, PhotonNetwork.CurrentRoom?.Name, "KNOWN JOIN");

                if (HopEnabled && StopOnFound)
                {
                    HopEnabled = false;
                    NotifiLib.SendNotification($"<color=yellow>[HOP]</color> STOPPED — found <color=white>{display}</color>. Staying.");
                }
            }
        }

        public static void OnPlayerLeft(Player otherPlayer)
        {
            if (!Enabled || otherPlayer == null || otherPlayer.IsLocal) return;

            string display = ResolveName(otherPlayer);
            string uid = GetUserId(otherPlayer) ?? "no-id";
            bool known = IsKnown(otherPlayer);

            if (LogLeaves)
            {
                string tag = known
                    ? "<color=grey>[</color><color=orange>KNOWN LEAVE</color><color=grey>]</color>"
                    : "<color=grey>[</color><color=red>LEAVE</color><color=grey>]</color>";
                NotifiLib.SendNotification($"{tag} <color=white>{display}</color> <color=grey>({uid})</color>");
            }

            if (known && DiscordWebhook.SendLeaves)
                DiscordWebhook.NotifyKnown(display, uid, PhotonNetwork.CurrentRoom?.Name, "KNOWN LEAVE");

            _notifiedActors.Remove(otherPlayer.ActorNumber);
        }

        public static void ScanRoom()
        {
            if (!PhotonNetwork.InRoom)
            {
                NotifiLib.SendNotification("<color=red>[TRACKER]</color> Not in a room.");
                return;
            }

            var sb = new StringBuilder();
            string room = PhotonNetwork.CurrentRoom?.Name ?? "?";
            sb.AppendLine($"<color=cyan>[TRACKER]</color> Room: {room} | Players: {PhotonNetwork.PlayerList.Length}");

            int knownCount = 0;
            foreach (Player p in PhotonNetwork.PlayerList)
            {
                if (p == null || p.IsLocal) continue;
                string display = ResolveName(p);
                string uid = GetUserId(p) ?? "?";
                bool known = IsKnown(p);
                if (known) knownCount++;

                string distStr = "";
                if (ShowDistance)
                {
                    float d = GetDistanceTo(p);
                    if (d >= 0f)
                        distStr = $" | {d:F1}m";
                }

                string marker = known ? "<color=yellow>*</color> " : "  ";
                sb.AppendLine($"{marker}<color=white>{display}</color> <color=grey>[{uid}]{distStr}</color>");
            }

            sb.AppendLine($"<color=yellow>Known in room: {knownCount}</color>");
            NotifiLib.SendNotification(sb.ToString());
            Debug.Log(sb.ToString());
        }

        public static bool ScanForKnown(bool notify)
        {
            if (!PhotonNetwork.InRoom) return false;

            var known = GetKnownInRoom();
            if (known.Count == 0)
            {
                if (notify)
                    NotifiLib.SendNotification($"<color=grey>[HOP]</color> No known — room {PhotonNetwork.CurrentRoom?.Name} ({PhotonNetwork.PlayerList.Length}p)");
                return false;
            }

            var names = new StringBuilder();
            foreach (Player p in known)
            {
                string display = ResolveName(p);
                string uid = GetUserId(p) ?? "?";
                names.Append(display).Append(" ");
                if (!string.IsNullOrEmpty(uid))
                    _foundKnownIds.Add(uid);
                _notifiedActors.Add(p.ActorNumber);
            }

            NotifiLib.SendNotification(
                $"<color=yellow>[HOP FOUND]</color> <color=white>{names}</color> in {PhotonNetwork.CurrentRoom?.Name} | hops={_hopsThisSession}");
            Debug.Log($"[PlayerTracker] FOUND known: {names} room={PhotonNetwork.CurrentRoom?.Name}");
            if (DiscordWebhook.SendHopFound)
                DiscordWebhook.NotifyKnown(names.ToString().Trim(), "multi", PhotonNetwork.CurrentRoom?.Name, "HOP FOUND");
            return true;
        }

        public static float GetDistanceTo(Player p)
        {
            try
            {
                VRRig rig = RigManager.GetVRRigFromPlayer(p);
                if (rig == null || GorillaTagger.Instance == null) return -1f;
                Vector3 myPos = GorillaTagger.Instance.headCollider.transform.position;
                return Vector3.Distance(myPos, rig.transform.position);
            }
            catch
            {
                return -1f;
            }
        }

        public static void DumpAllIds()
        {
            if (!PhotonNetwork.InRoom)
            {
                NotifiLib.SendNotification("<color=red>[TRACKER]</color> Not in a room.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("<color=cyan>[TRACKER DUMP]</color>");
            foreach (Player p in PhotonNetwork.PlayerList)
            {
                if (p == null) continue;
                string uid = GetUserId(p) ?? "null";
                string nick = p.NickName ?? "?";
                string known = IsKnown(p) ? " KNOWN" : "";
                sb.AppendLine($"{nick} | {uid} | Actor {p.ActorNumber}{known}");
            }
            Debug.Log(sb.ToString());
            NotifiLib.SendNotification($"<color=cyan>[TRACKER]</color> Dumped {PhotonNetwork.PlayerList.Length} players to log.");
        }

        public static void ToggleHop()
        {
            HopEnabled = !HopEnabled;
            if (HopEnabled)
            {
                Enabled = true;
                _hopsThisSession = 0;
                _scannedThisRoom = false;
                _roomEnterTime = Time.time;
                _nextHopTime = Time.time + JoinSettleTime;
                NotifiLib.SendNotification("<color=green>[HOP]</color> Lobby hop + known scan ON. StopOnFound=" + StopOnFound);
            }
            else
            {
                NotifiLib.SendNotification("<color=red>[HOP]</color> Lobby hop OFF.");
            }
        }

        public static void HopNow()
        {
            DoHop("manual");
        }

        private static void DoHop(string reason)
        {
            _hopsThisSession++;
            _scannedThisRoom = false;
            _notifiedActors.Clear();
            string room = PhotonNetwork.CurrentRoom?.Name ?? "?";
            NotifiLib.SendNotification($"<color=cyan>[HOP]</color> Leaving {room} → random ({reason}) #{_hopsThisSession}");

            try
            {
                if (PhotonNetwork.InRoom)
                    PhotonNetwork.Disconnect();
                _nextHopTime = Time.time + 1.2f;
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerTracker] Hop error: " + e.Message);
            }
        }

        private static void TryJoinRandom()
        {
            try
            {
                string zone = "forest";
                if (PhotonNetworkController.Instance != null && PhotonNetworkController.Instance.currentJoinTrigger != null)
                    zone = PhotonNetworkController.Instance.currentJoinTrigger.networkZone;

                if (GorillaComputer.instance != null)
                {
                    var trigger = GorillaComputer.instance.GetJoinTriggerForZone(zone);
                    if (trigger != null && PhotonNetworkController.Instance != null)
                    {
                        PhotonNetworkController.Instance.AttemptToJoinPublicRoom(trigger, 0);
                        return;
                    }
                }

                PhotonNetwork.JoinRandomRoom();
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerTracker] JoinRandom failed: " + e.Message);
                try { PhotonNetwork.JoinRandomRoom(); } catch { }
            }
        }

        public static void Update()
        {
            if (!Enabled && !HopEnabled) return;

            string currentRoom = PhotonNetwork.InRoom ? (PhotonNetwork.CurrentRoom?.Name ?? "") : "";
            if (currentRoom != _lastRoomCode)
            {
                _lastRoomCode = currentRoom;
                if (PhotonNetwork.InRoom)
                {
                    _roomEnterTime = Time.time;
                    _scannedThisRoom = false;
                    _nextHopTime = Time.time + JoinSettleTime + HopDelay;
                }
            }

            if (PhotonNetwork.InRoom && Enabled && Time.time - _lastScan >= 1.5f)
            {
                _lastScan = Time.time;
                if (AutoNotifyKnown)
                {
                    foreach (Player p in PhotonNetwork.PlayerList)
                    {
                        if (p == null || p.IsLocal) continue;
                        if (_notifiedActors.Contains(p.ActorNumber)) continue;
                        if (IsKnown(p))
                        {
                            _notifiedActors.Add(p.ActorNumber);
                            string display = ResolveName(p);
                            string uid = GetUserId(p) ?? "?";
                            if (!string.IsNullOrEmpty(uid)) _foundKnownIds.Add(uid);
                            NotifiLib.SendNotification($"<color=grey>[</color><color=yellow>KNOWN</color><color=grey>]</color> <color=white>{display}</color> <color=grey>({uid})</color>");
                            DiscordWebhook.NotifyKnown(display, uid, PhotonNetwork.CurrentRoom?.Name, "KNOWN");

                            if (HopEnabled && StopOnFound)
                            {
                                HopEnabled = false;
                                NotifiLib.SendNotification($"<color=yellow>[HOP]</color> STOPPED — found <color=white>{display}</color>.");
                            }
                        }
                    }
                }
            }

            if (!HopEnabled) return;

            if (!PhotonNetwork.InRoom)
            {
                if (Time.time >= _nextHopTime)
                {
                    _nextHopTime = Time.time + 3f;
                    TryJoinRandom();
                }
                return;
            }

            if (!_scannedThisRoom && Time.time - _roomEnterTime >= JoinSettleTime)
            {
                _scannedThisRoom = true;
                bool found = ScanForKnown(notify: true);
                if (found && StopOnFound)
                {
                    HopEnabled = false;
                    NotifiLib.SendNotification("<color=yellow>[HOP]</color> Target acquired. Hop disabled.");
                    return;
                }
                _nextHopTime = Time.time + HopDelay;
            }

            if (_scannedThisRoom && Time.time >= _nextHopTime)
            {
                DoHop("no-known");
            }
        }

        public static void ClearNotified()
        {
            _notifiedActors.Clear();
        }

        public static void Status()
        {
            string hop = HopEnabled ? "ON" : "OFF";
            string room = PhotonNetwork.InRoom ? (PhotonNetwork.CurrentRoom?.Name ?? "?") : "none";
            int knownHere = GetKnownInRoom().Count;
            NotifiLib.SendNotification(
                $"<color=cyan>[TRACKER]</color> hop={hop} room={room} known={knownHere} hops={_hopsThisSession} foundTotal={_foundKnownIds.Count}");
        }
    }
}
