using UnityEngine;
using System.Collections;

/// <summary>
/// Handles the setup and alignment of the ping pong table.
/// Works with SharedAnchor to ensure the table is shared with all players.
/// </summary>
public class TableSetup : MonoBehaviour
{
    [Header("Table Components")]
    [SerializeField] private Transform tableTop;
    [SerializeField] private Transform net;
    [SerializeField] private Transform[] tableBoundaries;
    [SerializeField] private Transform[] scoreZones;

    [Header("Visual Settings")]
    [SerializeField] private Material tableMaterial;
    [SerializeField] private Material highlightMaterial;
    [SerializeField] private float tableHeight = 0.76f; // Standard ping pong table height (meters)

    private SharedAnchor sharedAnchor;

    private void Awake()
    {
        // Get or add SharedAnchor component
        sharedAnchor = GetComponent<SharedAnchor>();
        if (sharedAnchor == null)
        {
            sharedAnchor = gameObject.AddComponent<SharedAnchor>();
        }
    }

    private void Start()
    {
        // Adjust table to standard height
        Vector3 position = transform.position;
        position.y = tableHeight;
        transform.position = position;

        // Initialize table components
        StartCoroutine(InitializeTableWhenAnchorReady());
    }

    private IEnumerator InitializeTableWhenAnchorReady()
    {
        // Wait until the anchor is fully initialized
        yield return new WaitUntil(() => sharedAnchor.Uuid != System.Guid.Empty);

        SampleController.Log($"Table anchor initialized with UUID: {sharedAnchor.Uuid}");

        // Set up table physics colliders
        SetupTableColliders();

        // Set up score zones
        SetupScoreZones();

        // Register with game manager
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.SetTableAnchor(sharedAnchor);
            SampleController.Log("Table registered with GameManager");
        }

        // Share with other players if needed
        if (sharedAnchor.Source.IsMine)  // Changed from sharedAnchor.IsMine to sharedAnchor.Source.IsMine
        {
            // Mark this as an alignment anchor if it's ours
            AlignPlayer alignComponent = FindObjectOfType<AlignPlayer>();
            if (alignComponent != null)
            {
                alignComponent.SetAlignmentAnchor(sharedAnchor);
                SampleController.Log("Table set as alignment anchor");
            }
        }
    }

    private void SetupTableColliders()
    {
        // Make sure table colliders are set up properly
        if (tableTop != null)
        {
            BoxCollider tableCollider = tableTop.GetComponent<BoxCollider>();
            if (tableCollider == null)
            {
                tableCollider = tableTop.gameObject.AddComponent<BoxCollider>();
            }

            // Tag for collision detection
            tableTop.gameObject.tag = "Table";
        }

        // Set up net collider
        if (net != null)
        {
            BoxCollider netCollider = net.GetComponent<BoxCollider>();
            if (netCollider == null)
            {
                netCollider = net.gameObject.AddComponent<BoxCollider>();
            }

            // Tag for collision detection
            net.gameObject.tag = "Net";
        }

        // Set up boundary colliders
        if (tableBoundaries != null)
        {
            foreach (Transform boundary in tableBoundaries)
            {
                if (boundary == null) continue;

                BoxCollider boundaryCollider = boundary.GetComponent<BoxCollider>();
                if (boundaryCollider == null)
                {
                    boundaryCollider = boundary.gameObject.AddComponent<BoxCollider>();
                }

                // Tag for collision detection
                boundary.gameObject.tag = "Wall";
            }
        }
    }

    private void SetupScoreZones()
    {
        // Set up score zones for each player
        if (scoreZones != null && scoreZones.Length >= 2)
        {
            // Set up player 1 score zone (when ball goes to player 2's side and off the table)
            SetupScoreZone(scoreZones[0], 1);

            // Set up player 2 score zone (when ball goes to player 1's side and off the table)
            SetupScoreZone(scoreZones[1], 2);
        }
        else
        {
            // Create default score zones if not provided
            CreateDefaultScoreZones();
        }
    }

    private void SetupScoreZone(Transform zoneTransform, int playerNumber)
    {
        if (zoneTransform == null) return;

        // Add score zone component
        ScoreZone scoreZone = zoneTransform.GetComponent<ScoreZone>();
        if (scoreZone == null)
        {
            scoreZone = zoneTransform.gameObject.AddComponent<ScoreZone>();
        }

        // Set scoring player
        scoreZone.SetScoringPlayer(playerNumber);

        // Make sure it has a trigger collider
        BoxCollider zoneCollider = zoneTransform.GetComponent<BoxCollider>();
        if (zoneCollider == null)
        {
            zoneCollider = zoneTransform.gameObject.AddComponent<BoxCollider>();
        }
        zoneCollider.isTrigger = true;

        // Make it invisible but present
        Renderer renderer = zoneTransform.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
        }
    }

    private void CreateDefaultScoreZones()
    {
        // Create default score zones based on table dimensions
        if (tableTop == null) return;

        // Get table dimensions
        Renderer tableRenderer = tableTop.GetComponent<Renderer>();
        if (tableRenderer == null) return;

        Bounds tableBounds = tableRenderer.bounds;

        // Player 1 score zone (negative Z side)
        GameObject p1ScoreZone = new GameObject("Player1ScoreZone");
        p1ScoreZone.transform.SetParent(transform);

        // Position it just beyond player 1's end of the table
        p1ScoreZone.transform.position = new Vector3(
            tableTop.position.x,
            tableTop.position.y,
            tableBounds.min.z - 1f
        );

        // Size it wider than the table to catch all balls
        BoxCollider p1Collider = p1ScoreZone.AddComponent<BoxCollider>();
        p1Collider.size = new Vector3(
            tableBounds.size.x + 2f,
            5f,
            2f
        );
        p1Collider.isTrigger = true;

        // Add score zone component
        ScoreZone p1Score = p1ScoreZone.AddComponent<ScoreZone>();
        p1Score.SetScoringPlayer(1);

        // Player 2 score zone (positive Z side)
        GameObject p2ScoreZone = new GameObject("Player2ScoreZone");
        p2ScoreZone.transform.SetParent(transform);

        // Position it just beyond player 2's end of the table
        p2ScoreZone.transform.position = new Vector3(
            tableTop.position.x,
            tableTop.position.y,
            tableBounds.max.z + 1f
        );

        // Size it wider than the table to catch all balls
        BoxCollider p2Collider = p2ScoreZone.AddComponent<BoxCollider>();
        p2Collider.size = new Vector3(
            tableBounds.size.x + 2f,
            5f,
            2f
        );
        p2Collider.isTrigger = true;

        // Add score zone component
        ScoreZone p2Score = p2ScoreZone.AddComponent<ScoreZone>();
        p2Score.SetScoringPlayer(2);

        // Store the new score zones
        scoreZones = new Transform[] { p1ScoreZone.transform, p2ScoreZone.transform };
    }

    // Helper method to highlight table when it's an active alignment target
    public void SetHighlighted(bool isHighlighted)
    {
        if (tableTop == null || tableMaterial == null || highlightMaterial == null) return;

        Renderer renderer = tableTop.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = isHighlighted ? highlightMaterial : tableMaterial;
        }
    }
}