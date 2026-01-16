using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

using Photon.VR.Player;
using Photon.VR.Cosmetics;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice;

using ExitGames.Client.Photon;

namespace Photon.VR
{
    [Serializable]
    public struct PhotonAppCredentials
    {
        public string Name;
        public string AppId;
        public string VoiceAppId;
    }

    public class PhotonVRManager : MonoBehaviourPunCallbacks
    {
        public static PhotonVRManager Manager { get; private set; }

        [Header("Photon Servers")]
        public List<PhotonAppCredentials> ServerList = new List<PhotonAppCredentials>();
        [Tooltip("Please read https://doc.photonengine.com/en-us/pun/current/connection-and-authentication/regions for more information")]
        public string Region = "eu";

        [Header("Status (Read Only)")]
        public string ConnectedServerName;
        public string ConnectedAppId;
        public string ConnectedVoiceAppId;

        [Header("Player")]
        public Transform Head;
        public Transform Body;
        public Transform LeftHand;
        public Transform RightHand;
        public Color Colour;
        public PhotonVRCosmeticsData Cosmetics { get; private set; } = new PhotonVRCosmeticsData();

        [Header("Networking")]
        public string DefaultQueue = "Default";
        public int DefaultRoomLimit = 16;

        [Header("Other")]
        [Tooltip("If the user shall connect when this object has awoken")]
        public bool ConnectOnAwake = true;
        [Tooltip("If the user shall join a room when they connect")]
        public bool JoinRoomOnConnect = true;

        [NonSerialized]
        public PhotonVRPlayer LocalPlayer;

        private RoomOptions options;
        private ConnectionState State = ConnectionState.Disconnected;
        private int currentServerIndex = 0;
        private AuthenticationValues lastAuthValues;

        private void Start()
        {
            if (Manager == null)
                Manager = this;
            else
            {
                Debug.LogError("There can't be multiple PhotonVRManagers in a scene");
                Application.Quit();
            }

            if (ConnectOnAwake)
                Connect();

            if (!string.IsNullOrEmpty(PlayerPrefs.GetString("Colour")))
                Colour = JsonUtility.FromJson<Color>(PlayerPrefs.GetString("Colour"));
            if (!string.IsNullOrEmpty(PlayerPrefs.GetString("Cosmetics")))
                Cosmetics = JsonUtility.FromJson<PhotonVRCosmeticsData>(PlayerPrefs.GetString("Cosmetics"));
        }

        public static bool Connect()
        {
            if (Manager.ServerList == null || Manager.ServerList.Count == 0)
            {
                Debug.LogError("Server List is empty");
                return false;
            }

            Manager.lastAuthValues = null;
            Manager.currentServerIndex = 0;
            return Manager.AttemptConnection();
        }

        public static bool ConnectAuthenticated(string username, string token)
        {
            if (Manager.ServerList == null || Manager.ServerList.Count == 0)
            {
                Debug.LogError("Server List is empty");
                return false;
            }

            AuthenticationValues authentication = new AuthenticationValues { AuthType = CustomAuthenticationType.Custom };
            authentication.AddAuthParameter("username", username);
            authentication.AddAuthParameter("token", token);

            Manager.lastAuthValues = authentication;
            Manager.currentServerIndex = 0;
            return Manager.AttemptConnection();
        }

        private bool AttemptConnection()
        {
            if (currentServerIndex >= ServerList.Count)
            {
                Debug.LogError("ALL SERVERS ARE FULL OR INVALID");
                ConnectedServerName = "NONE - ALL FULL";
                State = ConnectionState.Disconnected;
                return false;
            }

            PhotonAppCredentials current = ServerList[currentServerIndex];

            if (string.IsNullOrEmpty(current.AppId))
            {
                currentServerIndex++;
                return AttemptConnection();
            }

            PhotonNetwork.AuthValues = lastAuthValues;
            State = ConnectionState.Connecting;

            ConnectedServerName = current.Name;
            ConnectedAppId = current.AppId;
            ConnectedVoiceAppId = current.VoiceAppId;

            PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = current.AppId;
            PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice = current.VoiceAppId;
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = Region;

            PhotonNetwork.ConnectUsingSettings();
            Debug.Log($"Attempting connection to {current.Name} ({currentServerIndex + 1}/{ServerList.Count})");
            return true;
        }

        public void Disconnect()
        {
            PhotonNetwork.Disconnect();
        }

        public static void SetUsername(string Name)
        {
            PhotonNetwork.LocalPlayer.NickName = Name;
            PlayerPrefs.SetString("Username", Name);

            if (PhotonNetwork.InRoom)
                if (Manager.LocalPlayer != null)
                    Manager.LocalPlayer.RefreshPlayerValues();
        }

        public static void SetColour(Color PlayerColour)
        {
            Manager.Colour = PlayerColour;
            ExitGames.Client.Photon.Hashtable hash = PhotonNetwork.LocalPlayer.CustomProperties;
            hash["Colour"] = JsonUtility.ToJson(PlayerColour);
            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);
            PlayerPrefs.SetString("Colour", JsonUtility.ToJson(PlayerColour));

            if (PhotonNetwork.InRoom)
                if (Manager.LocalPlayer != null)
                    Manager.LocalPlayer.RefreshPlayerValues();
        }

        public static void SetCosmetics(PhotonVRCosmeticsData PlayerCosmetics)
        {
            Manager.Cosmetics = PlayerCosmetics;
            ExitGames.Client.Photon.Hashtable hash = PhotonNetwork.LocalPlayer.CustomProperties;
            hash["Cosmetics"] = JsonUtility.ToJson(PlayerCosmetics);
            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);
            PlayerPrefs.SetString("Cosmetics", JsonUtility.ToJson(PlayerCosmetics));

            if (PhotonNetwork.InRoom)
                if (Manager.LocalPlayer != null)
                    Manager.LocalPlayer.RefreshPlayerValues();
        }

        public static void SetCosmetic(CosmeticType Type, string CosmeticId)
        {
            PhotonVRCosmeticsData Cosmetics = Manager.Cosmetics;
            switch (Type)
            {
                case CosmeticType.Head:
                    Cosmetics.Head = CosmeticId;
                    break;
                case CosmeticType.Face:
                    Cosmetics.Face = CosmeticId;
                    break;
                case CosmeticType.Body:
                    Cosmetics.Body = CosmeticId;
                    break;
                case CosmeticType.BothHands:
                    Cosmetics.LeftHand = CosmeticId;
                    Cosmetics.RightHand = CosmeticId;
                    break;
                case CosmeticType.LeftHand:
                    Cosmetics.LeftHand = CosmeticId;
                    break;
                case CosmeticType.RightHand:
                    Cosmetics.RightHand = CosmeticId;
                    break;
            }
            Manager.Cosmetics = Cosmetics;
            ExitGames.Client.Photon.Hashtable hash = PhotonNetwork.LocalPlayer.CustomProperties;
            hash["Cosmetics"] = JsonUtility.ToJson(Cosmetics);
            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);
            PlayerPrefs.SetString("Cosmetics", JsonUtility.ToJson(Cosmetics));

            if (PhotonNetwork.InRoom)
                if (Manager.LocalPlayer != null)
                    Manager.LocalPlayer.RefreshPlayerValues();
        }

        public override void OnConnectedToMaster()
        {
            State = ConnectionState.Connected;
            Debug.Log($"Connected to {ServerList[currentServerIndex].Name}");

            PhotonNetwork.LocalPlayer.NickName = PlayerPrefs.GetString("Username", "Player");
            PhotonNetwork.LocalPlayer.CustomProperties["Colour"] = JsonUtility.ToJson(Colour);
            PhotonNetwork.LocalPlayer.CustomProperties["Cosmetics"] = JsonUtility.ToJson(Cosmetics);

            if (JoinRoomOnConnect)
                JoinRandomRoom(DefaultQueue, DefaultRoomLimit);
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (cause == DisconnectCause.MaxCcuReached)
            {
                Debug.LogWarning($"Server {ServerList[currentServerIndex].Name} is full. Trying next...");
                currentServerIndex++;
                AttemptConnection();
            }
            else
            {
                State = ConnectionState.Disconnected;
                ConnectedServerName = "Disconnected";
                Debug.Log($"Disconnected from server: {cause}");
            }
        }

        public static ConnectionState GetConnectionState()
        {
            return Manager.State;
        }

        public static void SwitchScenes(int SceneIndex, int MaxPlayers)
        {
            SceneManager.LoadScene(SceneIndex);
            JoinRandomRoom(SceneIndex.ToString(), MaxPlayers);
        }

        public static void SwitchScenes(int SceneIndex)
        {
            SceneManager.LoadScene(SceneIndex);
            JoinRandomRoom(SceneIndex.ToString(), Manager.DefaultRoomLimit);
        }

        public static void JoinRandomRoom(string Queue, int MaxPlayers) => _JoinRandomRoom(Queue, MaxPlayers);
        public static void JoinRandomRoom(string Queue) => _JoinRandomRoom(Queue, Manager.DefaultRoomLimit);

        private static void _JoinRandomRoom(string Queue, int MaxPlayers)
        {
            Manager.State = ConnectionState.JoiningRoom;
            ExitGames.Client.Photon.Hashtable hastable = new ExitGames.Client.Photon.Hashtable();
            hastable.Add("queue", Queue);
            hastable.Add("version", Application.version);

            RoomOptions roomOptions = new RoomOptions();
            roomOptions.MaxPlayers = (byte)MaxPlayers;
            roomOptions.IsVisible = true;
            roomOptions.IsOpen = true;
            roomOptions.CustomRoomProperties = hastable;
            roomOptions.CustomRoomPropertiesForLobby = new string[] { "queue", "version" };
            Manager.options = roomOptions;

            PhotonNetwork.JoinRandomRoom(hastable, (byte)roomOptions.MaxPlayers, MatchmakingMode.RandomMatching, null, null, null);
        }

        public static void JoinPrivateRoom(string RoomId, int MaxPlayers)
        {
            PhotonNetwork.JoinOrCreateRoom(RoomId, new RoomOptions()
            {
                IsVisible = false,
                IsOpen = true,
                MaxPlayers = (byte)MaxPlayers
            }, null, null);
        }

        public static void JoinPrivateRoom(string RoomId) => JoinPrivateRoom(RoomId, Manager.DefaultRoomLimit);

        public override void OnJoinedRoom()
        {
            Debug.Log("Joined a room");
            State = ConnectionState.InRoom;
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            string roomCode = new System.Random().Next(99999).ToString();
            PhotonNetwork.CreateRoom(roomCode, options, null, null);
        }
    }

    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        JoiningRoom,
        InRoom
    }
}