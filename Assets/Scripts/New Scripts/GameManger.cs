using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

/// <summary>
/// Manages the ping pong game state, scoring, and game flow.
/// </summary>
public class GameManager : MonoBehaviourPunCallbacks
{
    [Header("Game Settings")]
    [SerializeField] private int scoreToWin = 11;
    [SerializeField] private Transform ballSpawnPoint;
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private float serveDelay = 3f;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI player1ScoreText;
    [SerializeField] private TextMeshProUGUI player2ScoreText;
    [SerializeField] private TextMeshProUGUI gameStateText;

    [Header("Audio")]
    [SerializeField] private AudioClip scoreSound;
    [SerializeField] private AudioClip gameStartSound;
    [SerializeField] private AudioClip gameWinSound;

    // Game state tracking
    private int player1Score = 0;
    private int player2Score = 0;
    private bool gameInProgress = false;
    private bool awaitingServe = false;
    private GameObject currentBall;
    private int servingPlayer = 1; // 1 or 2

    // Shared anchor reference
    private SharedAnchor tableAnchor;
    private AudioSource audioSource;

    // Singleton instance
    public static GameManager Instance { get; private set; }

    public bool IsAwaitingServe() => awaitingServe;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        UpdateScoreUI();
        UpdateGameStateText("Waiting for players...");
        SampleController.Log("Ping Pong Game initialized. Waiting for players to join.");

        // If we're using the master client to start the game
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(DelayedGameStart());
        }
    }

    private IEnumerator DelayedGameStart()
    {
        yield return new WaitForSeconds(2f);

        // Once the shared anchor is initialized and both players connected
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount >= 2)
        {
            StartGame();
        }
        else
        {
            UpdateGameStateText("Waiting for second player...");
            SampleController.Log("Waiting for more players to join before starting the game.");
        }
    }

    public void SetTableAnchor(SharedAnchor anchor)
    {
        tableAnchor = anchor;
        SampleController.Log($"Table anchor set: {anchor.Uuid}");

        // If both players are here and we have the table anchor, we can start
        if (PhotonNetwork.IsMasterClient &&
            PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.PlayerCount >= 2 &&
            !gameInProgress)
        {
            StartCoroutine(DelayedGameStart());
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        SampleController.Log($"Player joined: {newPlayer.NickName}");

        UpdateGameStateText($"Player joined: {newPlayer.NickName}");

        if (PhotonNetwork.CurrentRoom.PlayerCount >= 2 && PhotonNetwork.IsMasterClient && !gameInProgress)
        {
            StartCoroutine(DelayedGameStart());
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        SampleController.Log($"Player left: {otherPlayer.NickName}");

        if (gameInProgress)
        {
            PauseGame();
            UpdateGameStateText($"Game paused: {otherPlayer.NickName} left");
        }
    }

    public void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        gameInProgress = true;
        player1Score = 0;
        player2Score = 0;
        servingPlayer = 1;

        UpdateScoreUI();

        // Play start sound
        if (audioSource && gameStartSound)
        {
            audioSource.PlayOneShot(gameStartSound);
        }

        // Share game state with all clients
        photonView.RPC("RPC_SyncGameState", RpcTarget.All, player1Score, player2Score, servingPlayer, true);

        // Start first serve
        StartNextRound();
    }

    private void PauseGame()
    {
        gameInProgress = false;
        awaitingServe = false;

        if (currentBall)
        {
            PhotonNetwork.Destroy(currentBall);
            currentBall = null;
        }

        // Sync game state
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("RPC_SyncGameState", RpcTarget.All, player1Score, player2Score, servingPlayer, false);
        }
    }

    public void StartNextRound()
    {
        if (!PhotonNetwork.IsMasterClient || !gameInProgress) return;

        // Clean up any existing ball
        if (currentBall != null)
        {
            PhotonNetwork.Destroy(currentBall);
            currentBall = null;
        }

        // Start countdown for next serve
        StartCoroutine(ServeCountdown());
    }

    private IEnumerator ServeCountdown()
    {
        awaitingServe = true;
        photonView.RPC("RPC_UpdateGameStateText", RpcTarget.All, $"Player {servingPlayer} serving in 3...");
        yield return new WaitForSeconds(1f);

        photonView.RPC("RPC_UpdateGameStateText", RpcTarget.All, $"Player {servingPlayer} serving in 2...");
        yield return new WaitForSeconds(1f);

        photonView.RPC("RPC_UpdateGameStateText", RpcTarget.All, $"Player {servingPlayer} serving in 1...");
        yield return new WaitForSeconds(1f);

        photonView.RPC("RPC_UpdateGameStateText", RpcTarget.All, "Play!");

        // Spawn the ball
        SpawnBall();
    }

    public void ServeBall()
    {
        if (!awaitingServe || !PhotonNetwork.IsMasterClient) return;

        // This is called when a player initiates the serve through input
        // (through pinch gesture with hand tracking or controller button)
        if (currentBall != null)
        {
            NetworkBall networkBall = currentBall.GetComponent<NetworkBall>();
            if (networkBall)
            {
                networkBall.Serve();
                awaitingServe = false;
            }
        }
    }

    private void SpawnBall()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Calculate ball position based on who's serving
        Vector3 spawnPos = ballSpawnPoint.position;
        if (servingPlayer == 2)
        {
            // Adjust spawn position for player 2 (opposite side)
            spawnPos = new Vector3(spawnPos.x, spawnPos.y, -spawnPos.z);
        }

        // Spawn the networked ball
        currentBall = PhotonNetwork.Instantiate(ballPrefab.name, spawnPos, Quaternion.identity);

        // Set the initial owner based on who's serving
        NetworkBall networkBall = currentBall.GetComponent<NetworkBall>();
        if (networkBall)
        {
            int ownerNumber = (servingPlayer == 1) ? 0 : 1;
            // Ensure we have enough players
            if (PhotonNetwork.PlayerList.Length > ownerNumber)
            {
                networkBall.SetInitialOwner(PhotonNetwork.PlayerList[ownerNumber].ActorNumber);
            }
            else
            {
                // Fallback to master client if player count doesn't match
                networkBall.SetInitialOwner(PhotonNetwork.MasterClient.ActorNumber);
            }
        }
    }

    public void ScorePoint(int scoringPlayer)
    {
        if (!PhotonNetwork.IsMasterClient || !gameInProgress) return;

        if (scoringPlayer == 1)
        {
            player1Score++;
            servingPlayer = 1;
        }
        else
        {
            player2Score++;
            servingPlayer = 2;
        }

        // Play score sound
        if (audioSource && scoreSound)
        {
            photonView.RPC("RPC_PlaySound", RpcTarget.All, 0); // 0 = score sound
        }

        // Update score on all clients
        photonView.RPC("RPC_UpdateScore", RpcTarget.All, player1Score, player2Score);

        // Check if game is over
        if (player1Score >= scoreToWin || player2Score >= scoreToWin)
        {
            EndGame();
        }
        else
        {
            // Start next round
            StartNextRound();
        }
    }

    private void EndGame()
    {
        gameInProgress = false;
        awaitingServe = false;

        // Determine winner
        string winnerText = player1Score > player2Score ? "Player 1 Wins!" : "Player 2 Wins!";
        photonView.RPC("RPC_UpdateGameStateText", RpcTarget.All, winnerText);

        // Play win sound
        if (audioSource && gameWinSound)
        {
            photonView.RPC("RPC_PlaySound", RpcTarget.All, 1); // 1 = win sound
        }

        // Clean up ball
        if (currentBall != null)
        {
            PhotonNetwork.Destroy(currentBall);
            currentBall = null;
        }

        // Offer rematch after delay
        StartCoroutine(OfferRematch());
    }

    private IEnumerator OfferRematch()
    {
        yield return new WaitForSeconds(5f);
        photonView.RPC("RPC_UpdateGameStateText", RpcTarget.All, "Starting new game...");
        yield return new WaitForSeconds(2f);

        StartGame();
    }

    private void UpdateScoreUI()
    {
        if (player1ScoreText) player1ScoreText.text = player1Score.ToString();
        if (player2ScoreText) player2ScoreText.text = player2Score.ToString();
    }

    private void UpdateGameStateText(string text)
    {
        if (gameStateText) gameStateText.text = text;
    }

    [PunRPC]
    private void RPC_UpdateScore(int p1Score, int p2Score)
    {
        player1Score = p1Score;
        player2Score = p2Score;
        UpdateScoreUI();
    }

    [PunRPC]
    private void RPC_UpdateGameStateText(string text)
    {
        UpdateGameStateText(text);
    }

    [PunRPC]
    private void RPC_SyncGameState(int p1Score, int p2Score, int serving, bool inProgress)
    {
        player1Score = p1Score;
        player2Score = p2Score;
        servingPlayer = serving;
        gameInProgress = inProgress;

        UpdateScoreUI();
    }

    [PunRPC]
    private void RPC_PlaySound(int soundType)
    {
        if (!audioSource) return;

        switch (soundType)
        {
            case 0: // Score sound
                if (scoreSound) audioSource.PlayOneShot(scoreSound);
                break;
            case 1: // Win sound
                if (gameWinSound) audioSource.PlayOneShot(gameWinSound);
                break;
            case 2: // Start sound
                if (gameStartSound) audioSource.PlayOneShot(gameStartSound);
                break;
        }
    }
}