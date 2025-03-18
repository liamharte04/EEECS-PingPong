using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Detects when the ball crosses into scoring areas and awards points accordingly.
/// </summary>
public class ScoreZone : MonoBehaviourPun
{
    [SerializeField] private int scoringPlayer = 1; // The player who gets a point when the ball enters this zone
    [SerializeField] private string ballTag = "Ball";

    [Header("Visual Feedback")]
    [SerializeField] private AudioClip scoreSound;
    [SerializeField] private ParticleSystem scoreEffect;

    private AudioSource audioSource;

    public void SetScoringPlayer(int player)
    {
        scoringPlayer = player;
    }

    private void Awake()
    {
        // Set up audio source if needed
        audioSource = GetComponent<AudioSource>();
        if (!audioSource && scoreSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if this is the ball
        if (other.CompareTag(ballTag))
        {
            // Only the master client handles scoring
            if (PhotonNetwork.IsMasterClient)
            {
                // Award point to the appropriate player
                GameManager.Instance.ScorePoint(scoringPlayer);

                // Trigger effects on all clients
                photonView.RPC("RPC_OnScore", RpcTarget.All);
            }
        }
    }

    [PunRPC]
    private void RPC_OnScore()
    {
        // Play sound effect
        if (audioSource && scoreSound)
        {
            audioSource.PlayOneShot(scoreSound);
        }

        // Play particle effect
        if (scoreEffect)
        {
            scoreEffect.Play();
        }
    }
}