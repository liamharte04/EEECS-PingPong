using UnityEngine;
using Photon.Pun;
using System.Collections;

/// <summary>
/// Extends existing room creation with ping pong game initialization.
/// Attach to the same GameObject as PhotonAnchorManager.
/// </summary>
public class EnhancedRoomManager : MonoBehaviourPunCallbacks
{
    [Header("Ping Pong Game")]
    [SerializeField] private GameObject gameManagerPrefab;
    [SerializeField] private GameObject tablePrefab;
    [SerializeField] private Transform tableSpawnPosition;
    [SerializeField] private GameObject pingPongUI;
    [SerializeField] private GameObject player1PaddlePrefab;
    [SerializeField] private GameObject player2PaddlePrefab;
    [SerializeField] private GameObject ballPrefab;

    private PhotonAnchorManager photonAnchorManager;
    private bool gameInitialized = false;
    private GameObject tableInstance;

    private void Awake()
    {
        photonAnchorManager = GetComponent<PhotonAnchorManager>();

        if (!photonAnchorManager)
        {
            Debug.LogError("EnhancedRoomManager must be attached to the same GameObject as PhotonAnchorManager");
        }
    }

    /// <summary>
    /// Enhanced create room functionality that also initializes the ping pong game
    /// </summary>
    public void CreateRoomWithPingPong()
    {
        // First call the original room creation method
        photonAnchorManager.OnCreateRoomButtonPressed();

        // Game initialization will happen in OnJoinedRoom callback
        SampleController.Log("Creating room with ping pong setup...");
    }

    public override void OnJoinedRoom()
    {
        // Let the base photon callback handle its functions
        base.OnJoinedRoom();

        // Initialize game once we're in the room
        StartCoroutine(InitializeGameWhenReady());
    }

    private IEnumerator InitializeGameWhenReady()
    {
        // Wait a moment for room connection to stabilize
        yield return new WaitForSeconds(1f);

        if (PhotonNetwork.IsMasterClient && !gameInitialized)
        {
            SampleController.Log("Initializing ping pong game as master client...");

            // Spawn game manager
            if (gameManagerPrefab != null)
            {
                GameObject gameManager = Instantiate(gameManagerPrefab);
                gameManager.name = "PingPongGameManager";
                SampleController.Log("PingPong Game Manager created");

                // Set up the game manager with references
                PingPongGameManager pingPongManager = gameManager.GetComponent<PingPongGameManager>();
                if (pingPongManager)
                {
                    pingPongManager.SetupReferences(player1PaddlePrefab, player2PaddlePrefab, ballPrefab);
                }
            }
            else
            {
                SampleController.LogError("Game Manager Prefab is not assigned!");
            }

            // Show ping pong UI if available
            if (pingPongUI != null)
            {
                pingPongUI.SetActive(true);
                SampleController.Log("Ping Pong UI activated");
            }

            // If we're the room creator, spawn the ping pong table
            if (tablePrefab != null && tableSpawnPosition != null)
            {
                yield return StartCoroutine(SpawnPingPongTable());
            }
            else
            {
                SampleController.LogError("Table Prefab or Spawn Position is not assigned!");
            }

            gameInitialized = true;
            SampleController.Log("Ping Pong game initialized successfully");
        }
        else if (!PhotonNetwork.IsMasterClient)
        {
            SampleController.Log("Joining existing ping pong game...");

            // For non-master clients, just show the UI and wait for shared anchors
            if (pingPongUI != null)
            {
                pingPongUI.SetActive(true);
            }

            // Look for existing table anchor
            yield return StartCoroutine(FindTableAnchor());
        }
    }

    private IEnumerator SpawnPingPongTable()
    {
        SampleController.Log("Spawning ping pong table...");

        // Instantiate the table at the defined position
        tableInstance = Instantiate(tablePrefab, tableSpawnPosition.position, tableSpawnPosition.rotation);
        tableInstance.name = "PingPongTable";

        // Make sure it has the necessary components
        TableSetup tableSetup = tableInstance.GetComponent<TableSetup>();
        if (!tableSetup)
        {
            tableSetup = tableInstance.AddComponent<TableSetup>();
            SampleController.Log("Added TableSetup component to table");
        }

        SharedAnchor sharedAnchor = tableInstance.GetComponent<SharedAnchor>();
        if (!sharedAnchor)
        {
            // If it doesn't have a SharedAnchor component, we need to add one
            // But first make sure it has an OVRSpatialAnchor
            OVRSpatialAnchor spatialAnchor = tableInstance.GetComponent<OVRSpatialAnchor>();
            if (!spatialAnchor)
            {
                spatialAnchor = tableInstance.AddComponent<OVRSpatialAnchor>();
                SampleController.Log("Added OVRSpatialAnchor component to table");
            }

            // Now add the SharedAnchor component
            sharedAnchor = tableInstance.AddComponent<SharedAnchor>();
            SampleController.Log("Added SharedAnchor component to table");
        }

        // Wait for the anchor to be created
        yield return new WaitUntil(() => sharedAnchor.Uuid != System.Guid.Empty);
        SampleController.Log($"Table anchor created with UUID: {sharedAnchor.Uuid}");

        // Share the anchor with others in the room
        sharedAnchor.ShareWithRoom();
        SampleController.Log("Table anchor shared with room");

        // Register with game manager
        PingPongGameManager gameManager = FindObjectOfType<PingPongGameManager>();
        if (gameManager)
        {
            gameManager.SetTableAnchor(sharedAnchor);
            SampleController.Log("Table registered with PingPongGameManager");
        }

        // Set as alignment anchor if there's an AlignPlayer component
        AlignPlayer alignPlayer = FindObjectOfType<AlignPlayer>();
        if (alignPlayer)
        {
            alignPlayer.SetAlignmentAnchor(sharedAnchor);
            SampleController.Log("Table set as alignment anchor");
        }
    }

    private IEnumerator FindTableAnchor()
    {
        SampleController.Log("Looking for existing table anchor...");
        SharedAnchor tableAnchor = null;

        // Try for up to 30 seconds to find the table anchor
        float searchStartTime = Time.time;

        while (tableAnchor == null && Time.time < searchStartTime + 30f)
        {
            // Try to find a table anchor by checking all shared anchors
            foreach (var anchor in SharedAnchor.All)
            {
                // Look for an anchor that has a TableSetup component
                if (anchor.gameObject.GetComponent<TableSetup>() != null)
                {
                    tableAnchor = anchor;
                    SampleController.Log($"Found table anchor: {tableAnchor.Uuid}");

                    // Register with game manager
                    PingPongGameManager gameManager = FindObjectOfType<PingPongGameManager>();
                    if (gameManager)
                    {
                        gameManager.SetTableAnchor(tableAnchor);
                        SampleController.Log("Registered found table with PingPongGameManager");
                    }

                    break;
                }
            }

            // If we haven't found it yet, wait a bit before checking again
            if (tableAnchor == null)
            {
                yield return new WaitForSeconds(1f);
            }
        }

        if (tableAnchor == null)
        {
            SampleController.LogError("Failed to find table anchor after 30 seconds!");
        }
    }
}