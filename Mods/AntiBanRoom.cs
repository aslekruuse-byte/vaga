using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Vaga.Mods
{
    /// <summary>
    /// AntiBanRoom — private code room with no public matchmaking / no AC queue.
    /// Creates a joinable private room by code; holds master; optional kick/lock.
    /// </summary>
    internal static class AntiBanRoom
    {
        public static bool Enabled;
        public static bool HoldMaster = true;
        public static bool KickJoiners = false;
        public static bool Closed = false;            // private code rooms stay joinable by code
        public static bool Invisible = true;          // not in public browser

        public static string PreferredCode = "";
        public static string LastCode = "";
        public static byte MaxPlayers = 10;

        private static float _nextMasterCheck;
        private static readonly HashSet<int> _seenActors = new HashSet<int>();

        public static void Update()
        {
            if (!Enabled || !PhotonNetwork.InRoom) return;

            try
            {
                var room = PhotonNetwork.CurrentRoom;
                if (room == null) return;

                // Stay off public lists
                if (Invisible && room.IsVisible)
                    room.IsVisible = false;

                // Optional: force closed (code join may still work depending on GT path)
                if (Closed && room.IsOpen)
                    room.IsOpen = false;

                if (HoldMaster && Time.time >= _nextMasterCheck)
                {
                    _nextMasterCheck = Time.time + 0.5f;
                    if (!PhotonNetwork.IsMasterClient)
                        PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer);
                }

                if (KickJoiners && PhotonNetwork.IsMasterClient)
                {
                    foreach (Player p in PhotonNetwork.PlayerListOthers)
                    {
                        if (p == null) continue;
                        if (_seenActors.Add(p.ActorNumber))
                        {
                            try { PhotonNetwork.CloseConnection(p); } catch { }
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Create a private room with a shareable code. Not listed publicly.
        /// Open for code join. No ranked / public AC queue properties.
        /// </summary>
        public static void Create()
        {
            try
            {
                if (PhotonNetwork.InRoom)
                {
                    try { PhotonNetwork.LeaveRoom(false); } catch { }
                }

                string code = string.IsNullOrWhiteSpace(PreferredCode)
                    ? RandomCode(5)
                    : PreferredCode.Trim().ToUpperInvariant();

                // Private code room:
                // - IsVisible = false  → not in public lobby browser
                // - IsOpen = true      → friends can join via code
                // - Custom props avoid public queue / ranked flags
                RoomOptions opts = new RoomOptions
                {
                    IsVisible = false,
                    IsOpen = true,
                    MaxPlayers = MaxPlayers,
                    PublishUserId = true,
                    EmptyRoomTtl = 0,
                    PlayerTtl = 0,
                    CleanupCacheOnLeave = true,
                    CustomRoomProperties = new Hashtable
                    {
                        { "gameMode", "private" },
                        { "queueName", "PRIVATE" },
                        { "ab", 1 },
                        { "noAC", 1 },
                        { "modded", true }
                    },
                    // Do NOT expose to public lobby filters
                    CustomRoomPropertiesForLobby = new string[] { }
                };

                Debug.Log("[AntiBanRoom] Creating private code room: " + code);
                bool ok = PhotonNetwork.CreateRoom(code, opts, TypedLobby.Default);
                if (!ok)
                    Debug.LogWarning("[AntiBanRoom] CreateRoom returned false");

                PreferredCode = code;
                LastCode = code;
                Invisible = true;
                Closed = false;
                Enabled = true;
                HoldMaster = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AntiBanRoom] Create failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Create private room then immediately apply full anti-ban hold.
        /// </summary>
        public static void CreatePrivateNoAC()
        {
            Create();
            // Hold activates on join via Enabled flag; convert props after in-room
            Enabled = true;
            HoldMaster = true;
            Invisible = true;
            Closed = false; // keep code-joinable
            KickJoiners = false;
        }

        public static void ConvertCurrent()
        {
            if (!PhotonNetwork.InRoom) return;
            try
            {
                if (!PhotonNetwork.IsMasterClient)
                    PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer);

                var room = PhotonNetwork.CurrentRoom;
                room.IsVisible = false;
                // keep IsOpen true so code joins still work unless user closes

                Hashtable props = new Hashtable
                {
                    { "ab", 1 },
                    { "noAC", 1 },
                    { "gameMode", "private" },
                    { "queueName", "PRIVATE" },
                    { "locked", PhotonNetwork.ServerTimestamp }
                };
                room.SetCustomProperties(props);

                Enabled = true;
                HoldMaster = true;
                Invisible = true;
                Closed = false;
                LastCode = room.Name;

                Debug.Log("[AntiBanRoom] Converted to private: " + room.Name);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AntiBanRoom] Convert failed: " + ex.Message);
            }
        }

        public static void LockAndClear()
        {
            ConvertCurrent();
            try
            {
                if (PhotonNetwork.CurrentRoom != null)
                    PhotonNetwork.CurrentRoom.IsOpen = false;
                Closed = true;
            }
            catch { }

            if (!PhotonNetwork.IsMasterClient) return;
            try
            {
                foreach (Player p in PhotonNetwork.PlayerListOthers)
                {
                    try { PhotonNetwork.CloseConnection(p); } catch { }
                }
                _seenActors.Clear();
            }
            catch { }
        }

        public static void OpenRoom()
        {
            if (!PhotonNetwork.InRoom) return;
            try
            {
                PhotonNetwork.CurrentRoom.IsOpen = true;
                Closed = false;
            }
            catch { }
        }

        public static void CloseRoom()
        {
            if (!PhotonNetwork.InRoom) return;
            try
            {
                PhotonNetwork.CurrentRoom.IsOpen = false;
                Closed = true;
            }
            catch { }
        }

        public static void Status()
        {
            string room = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom?.Name : "none";
            Debug.Log($"[AntiBanRoom] On={Enabled} Room={room} Code={LastCode} " +
                      $"Master={PhotonNetwork.IsMasterClient} Hold={HoldMaster} " +
                      $"KickJoin={KickJoiners} Closed={Closed} Invis={Invisible}");
        }

        private static string RandomCode(int len)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            char[] buf = new char[len];
            for (int i = 0; i < len; i++)
                buf[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
            return new string(buf);
        }
    }
}
