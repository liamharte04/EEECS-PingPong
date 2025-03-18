using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using TMPro;

/// <summary>
/// Handles game initialization, Photon connection, and scene setup.
/// Acts as the entry point for the ping pong game.
/// </summary>
public class GameInitializer : MonoBehaviourPunCallbacks
{
    [Header("Prefabs")]
    [SerializeField] private GameObject gameManagerPrefab;
    [SerializeField] private GameObject playerSetupPrefab;

    [Header("Connection Settings")]
    [SerializeField] private string gameVersion = "1.0";
    [SerializeField] private string roomNamePrefix = "PingPong_";
    [SerializeField] private int maxPlayersPerRoom = 2;
    [SerializeField] private bool autoJoinRoom = true;

    [Header("UI References")]
    [SerializeField] private GameObject connectingPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TMP_InputField roomNameInput;

    private bool isConnecting = false;

    private void Awake()
    {
        // Make sure we don't destroy this object when loading new scenes
        DontDestroyOnLoad(gameObject);

        // Set the default room name
        if (roomNameInput != null)
        {
            roomNameInput.text = roomNamePrefix + Random.Range(1000, 9999);
        }
    }

    private void Start()
    {
        // Set up initial UI state
        if (connectingPanel) connectingPanel.SetActive(false);
        if (mainMenuPanel) mainMenuPanel.SetActive(true);

        // Set up Photon settings
        PhotonNetwork.AutomaticallySyncScene = true;

        // Connect to Photon if not already connected
        if (!PhotonNetwork.IsConnected)
        {
            Connect();
        }
    }

    public void Connect()
    {
        if (isConnecting) return;

        // Show connecting UI
        if (connectingPanel) connectingPanel.SetActive(true);
        if (mainMenuPanel) mainMenuPanel.SetActive(false);

        // Update status
        UpdateStatus("Connecting to Photon...");

        // Connect to Photon
        isConnecting = true;
        PhotonNetwork.GameVersion = gameVersion;
        PhotonNetwork.ConnectUsingSettings();
    }

    public void OnCreateRoomButtonClicked()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Connect();
            return;
        }

        // Get room name from input field or generate one
        string roomName = roomNameInput != null && !string.IsNullOrEmpty(roomNameInput.text)
            ? roomNameInput.text
            : roomNamePrefix + Random.Range(1000, 9999);

        // Create room options
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = (byte)maxPlayersPerRoom,
            IsVisible = true,
            IsOpen = true
        };

        // Create the room
        UpdateStatus($"Creating room: {roomName}");
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public void OnJoinRoomButtonClicked()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Connect();
            return;
        }

        // Get room name from input field
        if (roomNameInput == null || string.IsNullOrEmpty(roomNameInput.text))
        {
            UpdateStatus("Please enter a room name.");
            return;
        }

        // Join the room
        UpdateStatus($"Joining room: {roomNameInput.text}");
        PhotonNetwork.JoinRoom(roomNameInput.text);
    }

    public void OnJoinRandomRoomButtonClicked()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Connect();
            return;
        }

        // Join a random room
        UpdateStatus("Joining a random room...");
        PhotonNetwork.JoinRandomRoom();
    }

    #region Photon Callbacks

    public override void OnConnectedToMaster()
    {
        isConnecting = false;
        UpdateStatus("Connected to Photon. Ready to join or create a room.");

        // Show main menu
        if (connectingPanel) connectingPanel.SetActive(false);
        if (mainMenuPanel) mainMenuPanel.SetActive(true);

        // Auto-join a room if enabled
        if (autoJoinRoom)
        {
            OnJoinRandomRoomButtonClicked();
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        isConnecting = false;
        UpdateStatus($"Disconnected: {cause}. Attempting to reconnect...");

        // Show main menu
        if (connectingPanel) connectingPanel.SetActive(false);
        if (mainMenuPanel) mainMenuPanel.SetActive(true);

        // Attempt to reconnect after a short delay
        Invoke(nameof(Connect), 2f);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        UpdateStatus("No open rooms found. Creating a new room...");
        OnCreateRoomButtonClicked();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        UpdateStatus($"Failed to join room: {message}");

        // Show main menu
        if (connectingPanel) connectingPanel.SetActive(false);
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        UpdateStatus($"Failed to create room: {message}");

        // Show main menu
        if (connectingPanel) connectingPanel.SetActive(false);
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
    }

    public override void OnJoinedRoom()
    {
        UpdateStatus($"Joined room: {PhotonNetwork.CurrentRoom.Name} with {PhotonNetwork.CurrentRoom.PlayerCount} players.");

        // Initialize the game setup
        InitializeGameSetup();

        // Hide all UI panels as we're now in the game
        if (connectingPanel) connectingPanel.SetActive(false);
        if (mainMenuPanel) mainMenuPanel.SetActive(false);
    }

    #endregion

    private void UpdateStatus(string message)
    {
        SampleController.Log(message);
        if (statusText) statusText.text = message;
    }

    private void InitializeGameSetup()
    {
        // Spawn the game manager if it doesn't exist and we're the master client
        if (PhotonNetwork.IsMasterClient)
        {
            // Look for an existing GameManager
            if (FindObjectOfType<GameManager>() == null && gameManagerPrefab != null)
            {
                Instantiate(gameManagerPrefab);
                SampleController.Log("Game Manager created");
            }
        }

        // Create player setup for local player
        if (playerSetupPrefab != null)
        {
            Instantiate(playerSetupPrefab);
            SampleController.Log("Player Setup created");
        }
    }
}