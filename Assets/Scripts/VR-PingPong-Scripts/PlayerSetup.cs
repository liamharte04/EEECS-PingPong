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

    [Header("Settings")]
    [SerializeField] private bool preferHandTracking = true;

    private GameObject paddleInstance;

    private void Start()
    {
        SampleController.Log("Player setup initializing...");

        // Wait a moment for the room to fully connect
        StartCoroutine(DelayedSetup());
    }

    private IEnumerator DelayedSetup()
    {
        yield return new WaitForSeconds(1f);

        // Create player paddle
        CreatePlayerPaddle();
    }

    private void CreatePlayerPaddle()
    {
        // Determine which player we are based on actor number
        bool isPlayer1 = PhotonNetwork.LocalPlayer.ActorNumber == PhotonNetwork.MasterClient.ActorNumber;

        // Choose the appropriate paddle prefab and spawn point
        GameObject paddlePrefab = isPlayer1 ? player1PaddlePrefab : player2PaddlePrefab;
        Transform spawnPoint = isPlayer1 ? player1SpawnPoint : player2SpawnPoint;

        if (paddlePrefab == null)
        {
            SampleController.LogError("Paddle prefab is missing!");
            return;
        }

        if (spawnPoint == null)
        {
            // Create default spawn points if not assigned
            Vector3 position = new Vector3(0, 1.0f, isPlayer1 ? -1.5f : 1.5f);
            GameObject spawnObj = new GameObject(isPlayer1 ? "Player1SpawnPoint" : "Player2SpawnPoint");
            spawnObj.transform.position = position;
            spawnPoint = spawnObj.transform;
        }

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
            dominantHand.HandConfidence == OVRHand.TrackingConfidence.High)
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