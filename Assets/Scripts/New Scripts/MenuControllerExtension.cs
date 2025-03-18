using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Extends the existing MenuController to add Ping Pong game launch functionality.
/// </summary>
public class MenuControllerExtension : MonoBehaviour
{
    [Header("Ping Pong Game")]
    [SerializeField] private GameObject pingPongPanel;
    [SerializeField] private Button pingPongButton;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject pingPongPrefab;

    [Header("Room Options")]
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button joinRandomButton;
    [SerializeField] private Button backButton;

    private GameInitializer gameInitializer;

    private void Awake()
    {
        // Initially hide the ping pong panel
        if (pingPongPanel)
            pingPongPanel.SetActive(false);

        // Set up the ping pong button
        if (pingPongButton)
            pingPongButton.onClick.AddListener(OnPingPongButtonClicked);

        // Set up the back button
        if (backButton)
            backButton.onClick.AddListener(OnBackButtonClicked);

        // Set up room buttons if they exist
        if (createRoomButton && joinRoomButton && joinRandomButton)
        {
            // These will be connected to the GameInitializer when the ping pong game is initialized
        }
    }

    public void OnPingPongButtonClicked()
    {
        // Hide the main menu and show ping pong panel
        if (mainMenuPanel)
            mainMenuPanel.SetActive(false);

        if (pingPongPanel)
            pingPongPanel.SetActive(true);

        // Initialize the ping pong game if it doesn't exist
        if (gameInitializer == null && pingPongPrefab != null)
        {
            GameObject pingPongObj = Instantiate(pingPongPrefab);
            gameInitializer = pingPongObj.GetComponent<GameInitializer>();

            // Connect the UI buttons to the game initializer
            ConnectUIToGameInitializer();
        }
    }

    public void OnBackButtonClicked()
    {
        // Hide ping pong panel and show main menu
        if (pingPongPanel)
            pingPongPanel.SetActive(false);

        if (mainMenuPanel)
            mainMenuPanel.SetActive(true);
    }

    private void ConnectUIToGameInitializer()
    {
        if (gameInitializer == null) return;

        // Connect room creation and joining buttons
        if (createRoomButton)
            createRoomButton.onClick.AddListener(gameInitializer.OnCreateRoomButtonClicked);

        if (joinRoomButton)
            joinRoomButton.onClick.AddListener(gameInitializer.OnJoinRoomButtonClicked);

        if (joinRandomButton)
            joinRandomButton.onClick.AddListener(gameInitializer.OnJoinRandomRoomButtonClicked);

        // Set the room name input field
        if (roomNameInput)
        {
            // You might want to set a default room name here
            roomNameInput.text = "PingPong_" + Random.Range(1000, 9999);
        }
    }
}