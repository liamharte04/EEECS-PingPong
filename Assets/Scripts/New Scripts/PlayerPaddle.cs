using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Handles paddle movement, input detection, and player interactions with the ball.
/// Supports both controller input and hand tracking.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerPaddle : MonoBehaviourPun, IPunObservable
{
    [Header("Paddle Settings")]
    [SerializeField] private int playerNumber = 1; // 1 for player 1, 2 for player 2
    [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.RTouch;
    [SerializeField] private float hapticStrength = 0.5f;
    [SerializeField] private float hapticDuration = 0.1f;

    [Header("Hand Tracking")]
    [SerializeField] private bool useHandTracking = false;
    [SerializeField] private OVRHand hand;
    [SerializeField] private OVRSkeleton handSkeleton;
    [SerializeField] private float pinchThreshold = 0.7f;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer paddleRenderer;
    [SerializeField] private Material ownedMaterial;
    [SerializeField] private Material otherMaterial;
    [SerializeField] private GameObject handVisualization;
    [SerializeField] private Material goodContactMaterial;
    [SerializeField] private Material badContactMaterial;
    [SerializeField] private ParticleSystem hitEffect;

    [Header("Audio")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip swingSound;

    // References
    private Rigidbody rb;
    private Transform controllerTransform;
    private Vector3 lastValidPosition;
    private Quaternion lastValidRotation;
    private AudioSource audioSource;
    private Transform palmTransform;

    // State tracking
    private bool isInitialized = false;
    private Bounds validMovementBounds;
    private bool isPinching = false;
    private Vector3 previousPosition;
    private float velocity;
    private float timeSincePrevious;
    private float lastHitTime;

    // Property for external scripts
    public int PlayerNumber => playerNumber;
    public Player PhotonOwner => photonView.Owner;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();

        // Set constraints to prevent unwanted physics
        rb.constraints = RigidbodyConstraints.FreezePositionY;

        // Set up paddle renderer material based on ownership
        if (paddleRenderer)
        {
            paddleRenderer.material = photonView.IsMine ? ownedMaterial : otherMaterial;
        }

        // Initialize hand visuals
        if (handVisualization)
        {
            handVisualization.SetActive(false);
        }
    }

    private void Start()
    {
        // Set up the reference to the OVR controller position
        if (photonView.IsMine)
        {
            // Initialize based on player number
            InitializePaddlePosition();

            // Set up movement bounds
            SetupMovementBounds();
        }

        isInitialized = true;
        previousPosition = transform.position;
    }

    private void InitializePaddlePosition()
    {
        // Find controller anchor based on which controller we're using
        OVRCameraRig cameraRig = FindObjectOfType<OVRCameraRig>();
        if (cameraRig != null)
        {
            if (controller == OVRInput.Controller.LTouch)
            {
                controllerTransform = cameraRig.leftControllerAnchor;
            }
            else
            {
                controllerTransform = cameraRig.rightControllerAnchor;
            }

            // Initialize hand reference if using hand tracking
            if (useHandTracking)
            {
                // Find appropriate OVRHand component if not set
                if (hand == null)
                {
                    if (controller == OVRInput.Controller.LTouch)
                    {
                        hand = GameObject.Find("LeftHandAnchor").GetComponentInChildren<OVRHand>();
                        handSkeleton = GameObject.Find("LeftHandAnchor").GetComponentInChildren<OVRSkeleton>();
                    }
                    else
                    {
                        hand = GameObject.Find("RightHandAnchor").GetComponentInChildren<OVRHand>();
                        handSkeleton = GameObject.Find("RightHandAnchor").GetComponentInChildren<OVRSkeleton>();
                    }
                }

                // Create a transform for the palm if we have a hand skeleton
                if (handSkeleton != null && handSkeleton.Bones.Count > 0)
                {
                    // Use middle finger metacarpal as approximate palm position
                    foreach (var bone in handSkeleton.Bones)
                    {
                        if (bone.Id == OVRSkeleton.BoneId.Hand_Middle1)
                        {
                            palmTransform = bone.Transform;
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            SampleController.LogError("Could not find OVRCameraRig in the scene!");
        }
    }

    private void SetupMovementBounds()
    {
        // Create movement bounds based on table position
        SharedAnchor tableAnchor = FindObjectOfType<SharedAnchor>(); // Assuming table has a SharedAnchor component

        if (tableAnchor != null)
        {
            // Get table bounds
            Renderer tableRenderer = tableAnchor.GetComponentInChildren<Renderer>();
            if (tableRenderer)
            {
                validMovementBounds = tableRenderer.bounds;

                // Expand slightly to allow for paddle to go a bit beyond the table
                validMovementBounds.Expand(new Vector3(0.5f, 0.5f, 0.5f));

                // Restrict to the player's half of the table
                if (playerNumber == 1)
                {
                    // Player 1 stays on negative Z side
                    validMovementBounds.min = new Vector3(
                        validMovementBounds.min.x,
                        validMovementBounds.min.y,
                        validMovementBounds.min.z
                    );
                    validMovementBounds.max = new Vector3(
                        validMovementBounds.max.x,
                        validMovementBounds.max.y,
                        0 // Center of table
                    );
                }
                else
                {
                    // Player 2 stays on positive Z side
                    validMovementBounds.min = new Vector3(
                        validMovementBounds.min.x,
                        validMovementBounds.min.y,
                        0 // Center of table
                    );
                    validMovementBounds.max = new Vector3(
                        validMovementBounds.max.x,
                        validMovementBounds.max.y,
                        validMovementBounds.max.z
                    );
                }
            }
        }

        // Store initial valid position
        lastValidPosition = transform.position;
        lastValidRotation = transform.rotation;
    }

    private void Update()
    {
        if (photonView.IsMine)
        {
            // Check if hand tracking status has changed
            if (hand != null)
            {
                bool handCurrentlyTracked = hand.IsTracked &&
                                          hand.HandConfidence == OVRHand.TrackingConfidence.High; // Fixed error: Changed HandConfidence to TrackingConfidence

                if (handCurrentlyTracked != useHandTracking)
                {
                    // Switch input methods
                    useHandTracking = handCurrentlyTracked;

                    // Provide feedback about the switch
                    string message = useHandTracking ?
                        "Switched to hand tracking" :
                        "Switched to controller input";

                    SampleController.Log(message);

                    // Update hand visualization
                    if (handVisualization)
                    {
                        handVisualization.SetActive(useHandTracking);
                    }
                }
            }

            // Update paddle position based on controller or hand
            UpdatePaddlePosition();

            // Detect gestures for hand tracking
            if (useHandTracking && hand != null && hand.IsTracked)
            {
                DetectHandGestures();
            }
            else
            {
                // Check controller input for ball serve
                if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller))
                {
                    OnTriggerOrPinch();
                }
            }

            // Calculate velocity for physics
            CalculateVelocity();
        }

        // Update hand visuals if using hand tracking
        if (useHandTracking && handVisualization)
        {
            UpdateHandVisuals();
        }
    }

    private void UpdatePaddlePosition()
    {
        if (useHandTracking && hand != null && hand.IsTracked)
        {
            // Use hand tracking for paddle position/rotation
            UpdatePaddleFromHandTracking();
        }
        else if (controllerTransform != null)
        {
            // Use controller for paddle position/rotation
            UpdatePaddleFromControllerPosition();
        }
    }

    private void UpdatePaddleFromHandTracking()
    {
        if (palmTransform == null) return;

        // Get palm position and orientation
        Vector3 targetPosition = palmTransform.position;
        Quaternion targetRotation = palmTransform.rotation;

        // Apply paddle-specific rotation offset
        targetRotation *= Quaternion.Euler(90, 0, 0); // Adjust as needed for paddle grip

        // Validate position is within bounds
        targetPosition = ValidatePosition(targetPosition);

        // Use physics for movement to enable proper collisions
        rb.MovePosition(targetPosition);
        rb.MoveRotation(targetRotation);
    }

    private void UpdatePaddleFromControllerPosition()
    {
        // Update paddle position based on controller
        Vector3 targetPosition = controllerTransform.position;
        Quaternion targetRotation = controllerTransform.rotation;

        // Validate position is within bounds
        targetPosition = ValidatePosition(targetPosition);

        // Use physics for movement to enable proper collisions
        rb.MovePosition(targetPosition);
        rb.MoveRotation(targetRotation);
    }

    private Vector3 ValidatePosition(Vector3 position)
    {
        if (validMovementBounds.size == Vector3.zero)
            return position;

        // Apply constraints based on player number
        if (playerNumber == 1)
        {
            // Clamp Z to ensure player 1 stays on their side
            if (position.z > 0) position.z = 0;
        }
        else
        {
            // Clamp Z to ensure player 2 stays on their side
            if (position.z < 0) position.z = 0;
        }

        // Clamp within bounds
        position.x = Mathf.Clamp(position.x,
            validMovementBounds.min.x, validMovementBounds.max.x);
        position.z = Mathf.Clamp(position.z,
            validMovementBounds.min.z, validMovementBounds.max.z);

        // Keep Y position fixed at the paddle's height
        position.y = transform.position.y;

        // Store valid position
        lastValidPosition = position;

        return position;
    }

    private void DetectHandGestures()
    {
        // Check for pinch gesture (for serving the ball or other interactions)
        float pinchStrength = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);

        if (pinchStrength > pinchThreshold && !isPinching)
        {
            isPinching = true;
            OnTriggerOrPinch();
        }
        else if (pinchStrength < pinchThreshold && isPinching)
        {
            isPinching = false;
        }
    }

    private void OnTriggerOrPinch()
    {
        // This method is called when the player triggers (controller) or pinches (hand)
        // Used for serving the ball
        if (GameManager.Instance.IsAwaitingServe() && photonView.IsMine)
        {
            GameManager.Instance.ServeBall();
        }
    }

    private void CalculateVelocity()
    {
        // Calculate paddle velocity for physics interactions
        timeSincePrevious += Time.deltaTime;

        if (timeSincePrevious > 0.02f) // Update every 20ms for smoother calculations
        {
            Vector3 currentPosition = transform.position;
            velocity = Vector3.Distance(currentPosition, previousPosition) / timeSincePrevious;
            previousPosition = currentPosition;
            timeSincePrevious = 0f;
        }
    }

    private void UpdateHandVisuals()
    {
        if (!hand.IsTracked)
        {
            handVisualization.SetActive(false);
            return;
        }

        handVisualization.SetActive(true);

        // Update visualization based on tracking quality
        Renderer visualRenderer = handVisualization.GetComponent<Renderer>();
        if (visualRenderer)
        {
            if (hand.HandConfidence == OVRHand.TrackingConfidence.High) // Fixed error: Changed HandConfidence to TrackingConfidence
            {
                visualRenderer.material = goodContactMaterial;
            }
            else
            {
                visualRenderer.material = badContactMaterial;
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Check if this is our paddle and if the collision is with the ball
        if (photonView.IsMine && collision.gameObject.CompareTag("Ball"))
        {
            // Provide haptic feedback
            if (!useHandTracking)
            {
                ProvidePaddleHapticFeedback();
            }

            // Play hit sound
            if (audioSource && hitSound)
            {
                audioSource.PlayOneShot(hitSound);
            }

            // Visual effect for hit
            if (Time.time - lastHitTime > 0.1f) // Prevent multiple effects for the same hit
            {
                lastHitTime = Time.time;
                photonView.RPC("RPC_PlayHitEffect", RpcTarget.All);
            }
        }
    }

    private void ProvidePaddleHapticFeedback()
    {
        // Apply haptic feedback to the controller
        OVRInput.SetControllerVibration(hapticStrength, hapticStrength, controller);

        // Stop vibration after duration
        CancelInvoke(nameof(StopHapticFeedback));
        Invoke(nameof(StopHapticFeedback), hapticDuration);
    }

    private void StopHapticFeedback()
    {
        OVRInput.SetControllerVibration(0, 0, controller);
    }

    [PunRPC]
    private void RPC_PlayHitEffect()
    {
        // Play visual and audio effects when paddle hits ball
        if (hitEffect)
        {
            hitEffect.Play();
        }

        // Flash the paddle on hit
        if (paddleRenderer)
        {
            StartCoroutine(FlashPaddle());
        }
    }

    private IEnumerator FlashPaddle()
    {
        // Flash the paddle on hit
        if (paddleRenderer)
        {
            Color originalColor = paddleRenderer.material.color;
            paddleRenderer.material.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            paddleRenderer.material.color = originalColor;
        }
    }

    public void SetHandTrackingMode(OVRHand dominantHand)
    {
        hand = dominantHand;
        useHandTracking = true;

        // Find hand skeleton
        handSkeleton = hand.GetComponent<OVRSkeleton>();

        // Create palm transform reference
        if (handSkeleton != null && handSkeleton.Bones.Count > 0)
        {
            // Try to get middle finger metacarpal as palm approximation
            foreach (var bone in handSkeleton.Bones)
            {
                if (bone.Id == OVRSkeleton.BoneId.Hand_Middle1)
                {
                    palmTransform = bone.Transform;
                    break;
                }
            }
        }

        if (handVisualization)
        {
            handVisualization.SetActive(true);
        }
    }

    public void SetControllerMode(OVRInput.Controller controllerType)
    {
        controller = controllerType;
        useHandTracking = false;

        // Find controller transform
        OVRCameraRig cameraRig = FindObjectOfType<OVRCameraRig>();
        if (cameraRig != null)
        {
            controllerTransform = controller == OVRInput.Controller.LTouch ?
                cameraRig.leftControllerAnchor : cameraRig.rightControllerAnchor;
        }

        if (handVisualization)
        {
            handVisualization.SetActive(false);
        }
    }

    // Implement IPunObservable for network syncing
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send data to other players
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(rb.velocity);
            stream.SendNext(velocity); // Send calculated velocity for physics
        }
        else
        {
            // Receive data from other players
            Vector3 receivedPosition = (Vector3)stream.ReceiveNext();
            Quaternion receivedRotation = (Quaternion)stream.ReceiveNext();
            Vector3 receivedVelocity = (Vector3)stream.ReceiveNext();
            float receivedSpeed = (float)stream.ReceiveNext();

            // Apply network lag compensation
            float lag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));
            receivedPosition += receivedVelocity * lag;

            // Update rigidbody
            rb.position = receivedPosition;
            rb.rotation = receivedRotation;
            rb.velocity = receivedVelocity;
            velocity = receivedSpeed;
        }
    }
}