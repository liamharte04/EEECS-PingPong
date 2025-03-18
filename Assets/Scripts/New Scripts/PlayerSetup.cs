using UnityEngine;
using Photon.Pun;
using System.Collections;

/// <summary>
/// Handles player initialization and connection to shared spatial anchors.
/// </summary>
public class PlayerSetup : MonoBehaviourPunCallbacks
{
    [Header("Player References")]
    [SerializeField] private GameObject player1PaddlePrefab;
    [SerializeField] private GameObject player2PaddlePrefab;
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Transform player2SpawnPoint;

    [Header("Table References")]
    [SerializeField] private GameObject tablePrefab;
    [SerializeField] private Transform tableSpawnPoint;

    [Header("Settings")]
    [SerializeField] private bool autoCreateTable = true;
    [SerializeField] private bool preferHandTracking = true;

    private GameObject paddleInstance;
    private GameObject tableInstance;
    private SharedAnchor tableAnchor;

    private void Start()
    {
        SampleController.Log("Player setup initializing...");

        // Wait a moment for the room to fully connect
        StartCoroutine(DelayedSetup());
    }

    private IEnumerator DelayedSetup()
    {
        yield return new WaitForSeconds(1f);

        // First, attempt to find existing table anchor
        TryFindExistingTable();

        // If no table was found and we're the master client, create one
        if (tableAnchor == null && PhotonNetwork.IsMasterClient && autoCreateTable)
        {
            CreateAndShareTable();
        }

        // Create player paddle
        CreatePlayerPaddle();
    }

    private void TryFindExistingTable()
    {
        SampleController.Log("Searching for existing table anchor...");

        // Look for any existing shared anchors in the scene
        SharedAnchor[] anchors = FindObjectsOfType<SharedAnchor>();

        foreach (var anchor in anchors)
        {
            // Check if this anchor has a Table tag or component
            if (anchor.gameObject.CompareTag("Table") || anchor.gameObject.GetComponentInChildren<Renderer>())
            {
                tableAnchor = anchor;
                SampleController.Log($"Found existing table anchor: {anchor.Uuid}");

                // Register the table with the game manager
                GameManager.Instance.SetTableAnchor(tableAnchor);
                return;
            }
        }

        SampleController.Log("No existing table anchor found.");
    }

    private void CreateAndShareTable()
    {
        SampleController.Log("Creating new table anchor...");

        // Instantiate table at spawn point
        tableInstance = Instantiate(tablePrefab, tableSpawnPoint.position, tableSpawnPoint.rotation);

        // Get or add SharedAnchor component
        tableAnchor = tableInstance.GetComponent<SharedAnchor>();
        if (tableAnchor == null)
        {
            tableAnchor = tableInstance.AddComponent<SharedAnchor>();
        }

        // Share the table with other players
        StartCoroutine(ShareTableWhenReady());
    }

    private IEnumerator ShareTableWhenReady()
    {
        // Wait for the anchor to be fully created and ready to share
        yield return new WaitUntil(() => tableAnchor.Uuid != System.Guid.Empty);

        SampleController.Log($"Table anchor created with UUID: {tableAnchor.Uuid}");

        // Share with room using the existing system
        tableAnchor.ShareWithRoom();
        SampleController.Log("Table anchor shared with room.");

        // Register the table with the game manager
        GameManager.Instance.SetTableAnchor(tableAnchor);

        // Set as alignment anchor
        AlignPlayer alignPlayer = FindObjectOfType<AlignPlayer>();
        if (alignPlayer)
        {
            alignPlayer.SetAlignmentAnchor(tableAnchor);
            SampleController.Log("Table set as alignment anchor.");
        }
    }

    private void CreatePlayerPaddle()
    {
        // Determine which player we are based on actor number
        bool isPlayer1 = PhotonNetwork.LocalPlayer.ActorNumber == PhotonNetwork.MasterClient.ActorNumber;

        // Choose the appropriate paddle prefab and spawn point
        GameObject paddlePrefab = isPlayer1 ? player1PaddlePrefab : player2PaddlePrefab;
        Transform spawnPoint = isPlayer1 ? player1SpawnPoint : player2SpawnPoint;

        // Instantiate the paddle
        paddleInstance = PhotonNetwork.Instantiate(
            paddlePrefab.name,
            spawnPoint.position,
            spawnPoint.rotation
        );

        // Set paddle player number
        PlayerPaddle paddleComponent = paddleInstance.GetComponent<PlayerPaddle>();
        if (paddleComponent)
        {
            // Initialize input method
            InitializeInputMethod(paddleComponent, isPlayer1 ? 1 : 2);
        }

        SampleController.Log($"Player {(isPlayer1 ? "1" : "2")} paddle created.");
    }

    private void InitializeInputMethod(PlayerPaddle paddle, int playerNumber)
    {
        if (!preferHandTracking)
        {
            // Default to controllers
            paddle.SetControllerMode(playerNumber == 1 ?
                OVRInput.Controller.RTouch : OVRInput.Controller.LTouch);
            SampleController.Log($"Using controller input for player {playerNumber}");
            return;
        }

        // Try to find appropriate hand
        OVRHand dominantHand = null;

        // Find the appropriate hand based on player number
        if (playerNumber == 1)
        {
            // Player 1 typically uses right hand
            dominantHand = GameObject.Find("RightHandAnchor")?.GetComponentInChildren<OVRHand>();
        }
        else
        {
            // Player 2 typically uses left hand
            dominantHand = GameObject.Find("LeftHandAnchor")?.GetComponentInChildren<OVRHand>();
        }

        // Check if hand tracking is available and working
        if (dominantHand != null && dominantHand.IsTracked &&
            dominantHand.HandConfidence == OVRHand.TrackingConfidence.High) // Fixed error: Changed HandConfidence to TrackingConfidence
        {
            // Use hand tracking
            paddle.SetHandTrackingMode(dominantHand);
            SampleController.Log($"Using hand tracking for player {playerNumber}");
        }
        else
        {
            // Fall back to controllers
            paddle.SetControllerMode(playerNumber == 1 ?
                OVRInput.Controller.RTouch : OVRInput.Controller.LTouch);
            SampleController.Log($"Using controller input for player {playerNumber} (hand tracking not available)");
        }
    }

    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        SampleController.LogError($"Disconnected from Photon: {cause}");
    }
}