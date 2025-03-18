using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime; // Added this import for the Player class

/// <summary>
/// Handles the networked physics and ownership transfers for the ping pong ball.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class NetworkBall : MonoBehaviourPun, IPunOwnershipCallbacks
{
    [Header("Ball Settings")]
    [SerializeField] private float initialForce = 5f;
    [SerializeField] private float maxVelocity = 20f;
    [SerializeField] private float ownershipTransferCooldown = 0.5f;
    [SerializeField] private float serveHeight = 0.3f; // Height above paddle for serve

    [Header("Effects")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip wallHitSound;
    [SerializeField] private AudioClip tableHitSound;
    [SerializeField] private ParticleSystem hitParticles;
    [SerializeField] private TrailRenderer trailRenderer;

    // Component references
    private Rigidbody rb;
    private AudioSource audioSource;
    private MeshRenderer meshRenderer;

    // State tracking
    private bool canTransferOwnership = true;
    private float lastHitTime = 0f;
    private Vector3 latestReceivedPosition;
    private Vector3 latestReceivedVelocity;
    private bool hasBeenServed = false;
    private Color originalTrailColor;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();

        // Make sure we're using interpolation for smoother movement
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Store original trail color if there is a trail renderer
        if (trailRenderer)
        {
            originalTrailColor = trailRenderer.startColor;
            trailRenderer.emitting = false; // Start with trail off until served
        }

        // Register for ownership transfer callbacks
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Start()
    {
        // Initial setup
        if (photonView.IsMine)
        {
            // Don't apply force yet - wait for serve command
            rb.isKinematic = true;

            // Position for serve
            PositionForServe();
        }
        else
        {
            // For non-owners, just make sure physics is set up properly
            rb.isKinematic = false;
        }
    }

    private void PositionForServe()
    {
        // Find the correct paddle for serving based on owner
        int playerNumber = photonView.Owner.ActorNumber == PhotonNetwork.MasterClient.ActorNumber ? 1 : 2;
        PlayerPaddle[] paddles = FindObjectsOfType<PlayerPaddle>();

        foreach (PlayerPaddle paddle in paddles)
        {
            if (paddle.PlayerNumber == playerNumber && photonView.Owner == paddle.PhotonOwner)
            {
                // Position slightly above the paddle
                Vector3 servePosition = paddle.transform.position;
                servePosition.y += serveHeight;
                transform.position = servePosition;
                break;
            }
        }
    }

    public void SetInitialOwner(int playerActorNumber)
    {
        if (photonView.Owner.ActorNumber == playerActorNumber)
        {
            // We're already the owner
            return;
        }

        photonView.TransferOwnership(playerActorNumber);
    }

    public void Serve()
    {
        if (!photonView.IsMine) return;

        // Get serving direction based on player number
        // Player 1 serves in +Z, Player 2 serves in -Z
        int playerNumber = photonView.Owner.ActorNumber == PhotonNetwork.MasterClient.ActorNumber ? 1 : 2;

        // Apply randomized initial force
        Vector3 direction = new Vector3(
            Random.Range(-0.3f, 0.3f),
            Random.Range(0.1f, 0.3f),
            playerNumber == 1 ? 1 : -1
        ).normalized;

        rb.isKinematic = false;
        rb.velocity = Vector3.zero; // Reset velocity before applying force
        rb.AddForce(direction * initialForce, ForceMode.Impulse);

        // Turn on the trail renderer
        if (trailRenderer)
        {
            trailRenderer.emitting = true;
        }

        // Mark as served
        hasBeenServed = true;

        // Notify all clients the ball has been served
        photonView.RPC("RPC_OnBallServed", RpcTarget.Others);
    }

    [PunRPC]
    private void RPC_OnBallServed()
    {
        // Enable trail renderer for non-owners too
        if (trailRenderer)
        {
            trailRenderer.emitting = true;
        }

        hasBeenServed = true;
        rb.isKinematic = false;
    }

    private void FixedUpdate()
    {
        if (!hasBeenServed) return;

        if (photonView.IsMine)
        {
            // Handle owner physics
            HandleOwnerPhysics();
        }
        else
        {
            // Handle remote physics - smoothly move to received position
            HandleRemotePhysics();
        }
    }

    private void HandleOwnerPhysics()
    {
        // Clamp velocity to max speed
        if (rb.velocity.magnitude > maxVelocity)
        {
            rb.velocity = rb.velocity.normalized * maxVelocity;
        }

        // Keep ball from bouncing too high off the table
        if (rb.velocity.y > maxVelocity * 0.3f)
        {
            Vector3 newVelocity = rb.velocity;
            newVelocity.y = maxVelocity * 0.3f;
            rb.velocity = newVelocity;
        }

        // Check if ball is out of bounds - if so, find who scored
        if (transform.position.y < -5f || Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.z) > 10f)
        {
            // Ball is out of bounds, determine who scored
            int scoringPlayer = transform.position.z > 0 ? 2 : 1; // If Z > 0, player 2 scored, else player 1
            GameManager.Instance.ScorePoint(scoringPlayer);
            return;
        }

        // Send position and velocity to other clients regularly
        if (Time.frameCount % 3 == 0) // Every 3 frames to reduce network traffic but maintain responsiveness
        {
            photonView.RPC("RPC_SyncBallPhysics", RpcTarget.Others,
                transform.position, rb.velocity);
        }
    }

    private void HandleRemotePhysics()
    {
        // Smoothly move to the latest received position to handle network latency
        if (latestReceivedPosition != Vector3.zero)
        {
            // Calculate a position slightly ahead based on velocity
            Vector3 projectedPosition = latestReceivedPosition +
                                        (latestReceivedVelocity * Time.fixedDeltaTime);

            // Smoothly move to the projected position
            transform.position = Vector3.Lerp(transform.position,
                projectedPosition, Time.fixedDeltaTime * 10);

            // Update the rigidbody velocity to match the received velocity
            rb.velocity = latestReceivedVelocity;
        }
    }

    [PunRPC]
    private void RPC_SyncBallPhysics(Vector3 position, Vector3 velocity)
    {
        // Store received values
        latestReceivedPosition = position;
        latestReceivedVelocity = velocity;

        // Force immediately update to new position if the difference is too large
        if (Vector3.Distance(transform.position, position) > 2.0f)
        {
            transform.position = position;
            rb.velocity = velocity;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!hasBeenServed) return;

        // Play sound effects and particles based on what was hit
        if (audioSource)
        {
            if (collision.gameObject.CompareTag("Paddle"))
            {
                audioSource.PlayOneShot(hitSound);

                // Transfer ownership when hit by a paddle
                HandlePaddleHit(collision);
            }
            else if (collision.gameObject.CompareTag("Table"))
            {
                audioSource.PlayOneShot(tableHitSound);
            }
            else if (collision.gameObject.CompareTag("Net") || collision.gameObject.CompareTag("Wall"))
            {
                audioSource.PlayOneShot(wallHitSound);
            }
        }

        // Spawn hit particles at collision point (if we're the owner)
        if (photonView.IsMine && hitParticles)
        {
            // Get the contact point
            ContactPoint contact = collision.contacts[0];

            // Create particles via RPC so everyone sees them
            photonView.RPC("RPC_SpawnHitEffect", RpcTarget.All,
                contact.point,
                contact.normal);
        }
    }

    [PunRPC]
    private void RPC_SpawnHitEffect(Vector3 position, Vector3 normal)
    {
        if (hitParticles)
        {
            // Play particle effect at contact point
            hitParticles.transform.position = position;
            hitParticles.transform.rotation = Quaternion.LookRotation(normal);
            hitParticles.Play();
        }
    }

    private void HandlePaddleHit(Collision collision)
    {
        // Only transfer ownership if we have ownership and enough time has passed
        if (!canTransferOwnership || !photonView.IsMine) return;

        // Get the player ID from the paddle
        PlayerPaddle paddle = collision.gameObject.GetComponent<PlayerPaddle>();
        if (paddle != null)
        {
            // Calculate reflect direction based on where the ball hit the paddle
            Vector3 hitPoint = collision.contacts[0].point;
            Vector3 paddleCenter = collision.collider.bounds.center;
            Vector3 direction = (hitPoint - paddleCenter).normalized;

            // Add some force based on paddle movement
            Rigidbody paddleRb = collision.gameObject.GetComponent<Rigidbody>();
            if (paddleRb)
            {
                // Apply force based on paddle velocity
                rb.velocity += paddleRb.velocity * 0.5f;

                // Ensure we're sending the ball forward in the right direction
                if ((paddle.PlayerNumber == 1 && rb.velocity.z < 0) ||
                    (paddle.PlayerNumber == 2 && rb.velocity.z > 0))
                {
                    Vector3 correctedVelocity = rb.velocity;
                    correctedVelocity.z *= -1;
                    rb.velocity = correctedVelocity;
                }
            }

            // Transfer ownership to the player who hit the ball
            if (paddle.PhotonOwner != photonView.Owner)
            {
                photonView.TransferOwnership(paddle.PhotonOwner);
                StartCoroutine(OwnershipTransferCooldown());
            }
        }
    }

    private IEnumerator OwnershipTransferCooldown()
    {
        canTransferOwnership = false;
        yield return new WaitForSeconds(ownershipTransferCooldown);
        canTransferOwnership = true;
    }

    // Called when ownership changes
    public void OnOwnershipRequest(PhotonView targetView, Player requestingPlayer)
    {
        // Auto-accept ownership transfers since we initiate them ourselves
        if (targetView.ViewID == this.photonView.ViewID)
        {
            canTransferOwnership = false;
        }
    }

    public void OnOwnershipTransfered(PhotonView targetView, Player previousOwner)
    {
        if (targetView.ViewID == this.photonView.ViewID)
        {
            SampleController.Log($"Ball ownership transferred from {previousOwner.NickName} to {targetView.Owner.NickName}");

            // Reset transfer cooldown
            StartCoroutine(OwnershipTransferCooldown());

            // Enhance the trail effect to show transfer
            if (trailRenderer)
            {
                Color newColor = photonView.IsMine ? Color.green : Color.red;
                StartCoroutine(FlashTrail(newColor));
            }
        }
    }

    private IEnumerator FlashTrail(Color targetColor)
    {
        if (!trailRenderer) yield break;

        // Store original color
        Color startColor = trailRenderer.startColor;

        // Flash to target color
        trailRenderer.startColor = targetColor;

        // Wait a moment
        yield return new WaitForSeconds(0.3f);

        // Return to original color
        trailRenderer.startColor = originalTrailColor;
    }

    public void OnOwnershipTransferFailed(PhotonView targetView, Player senderOfFailedRequest)
    {
        if (targetView.ViewID == this.photonView.ViewID)
        {
            SampleController.Log($"Ball ownership transfer FAILED from {senderOfFailedRequest.NickName}");
            canTransferOwnership = true;
        }
    }

    // Helper method for debugging
    private void OnGUI()
    {
        if (!Debug.isDebugBuild) return;

        // Display ownership info at ball position
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        string ownerText = photonView.IsMine ? "Mine" : photonView.Owner.NickName;
        GUI.Label(new Rect(screenPos.x, Screen.height - screenPos.y, 100, 20), ownerText);
    }
}