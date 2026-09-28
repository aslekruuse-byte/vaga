using GorillaGameModes;
using GorillaNetworking;
using GorillaTagScripts;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Vaga.Mods
{
    internal class Room
    {
        public static void JoinRandom()
        {
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.Disconnect();
            }
            else
            {
                string text = PhotonNetworkController.Instance.currentJoinTrigger == null ? "forest" : PhotonNetworkController.Instance.currentJoinTrigger.networkZone;
                PhotonNetworkController.Instance.AttemptToJoinPublicRoom(GorillaComputer.instance.GetJoinTriggerForZone(text), 0);
            }
        }
        public static void RestartGame()
        {
            Process.Start("steam://rungameid/1533390");
            Application.Quit();
        }

        public static void QuitGame()
        {
            Application.Quit();
        }

        
        public static void Reconnect()
        {
            string name = PhotonNetwork.CurrentRoom.Name;
            NetworkSystem.Instance.ReturnToSinglePlayer();
            PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(name, GorillaNetworking.JoinType.Solo);
        }
        public static void OpenGorillaTagFolder()
        {
            string filePath = Assembly.GetExecutingAssembly().Location.Split("BepInEx\\")[0];
            Process.Start(filePath);
        }
        public static void ReportAll()
        {
            GorillaPlayerScoreboardLine[] Board = UnityEngine.Object.FindObjectsOfType<GorillaPlayerScoreboardLine>();
            foreach (GorillaPlayerScoreboardLine report in Board)
            {
                if (report.linePlayer != null)
                {
                    report.PressButton(true, GorillaPlayerLineButton.ButtonType.HateSpeech);
                }
            }
        }
        public static void MuteAll()
        {
            GorillaPlayerScoreboardLine[] Board = UnityEngine.Object.FindObjectsOfType<GorillaPlayerScoreboardLine>();
            foreach (GorillaPlayerScoreboardLine mute in Board)
            {
                if (mute.linePlayer != null)
                {
                    mute.PressButton(true, GorillaPlayerLineButton.ButtonType.Mute);
                    mute.muteButton.isOn = true;
                    mute.muteButton.UpdateColor();
                }
            }
        }
    }
}
