using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Simple component to add to the main menu that adds a Ping Pong game launch button.
/// </summary>
public class PingPongLauncher : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject pingPongButton; // Button prefab to add to menu
    [SerializeField] private Transform buttonParent; // Parent transform to add button to
    [SerializeField] private GameObject pingPongGamePrefab; // Prefab containing all ping pong game elements

    [Header("Button Settings")]
    [SerializeField] private string buttonText = "Play Ping Pong";
    [SerializeField] private Sprite pingPongIcon;

    private GameObject gameInstance;
    private Button launchButton;

    private void Start()
    {
        // Find button parent if not set
        if (!buttonParent)
        {
            // Try to find the menu panel or commands section
            buttonParent = GameObject.Find("Commands")?.transform;

            if (!buttonParent)
            {
                // Look for any panel with buttons as a fallback
                Canvas canvas = FindObjectOfType<Canvas>();
                if (canvas)
                {
                    foreach (Transform child in canvas.transform)
                    {
                        if (child.GetComponentInChildren<Button>() != null)
                        {
                            buttonParent = child;
                            break;
                        }
                    }
                }
            }
        }

        // Create button if we have a parent
        if (buttonParent)
        {
            CreatePingPongButton();
        }
        else
        {
            Debug.LogError("PingPongLauncher: Could not find a parent transform for the button.");
        }
    }

    private void CreatePingPongButton()
    {
        GameObject buttonObj;

        // Use prefab if available, otherwise create a new button
        if (pingPongButton)
        {
            buttonObj = Instantiate(pingPongButton, buttonParent);
        }
        else
        {
            // Create a basic button
            buttonObj = new GameObject("PingPongButton");
            buttonObj.transform.SetParent(buttonParent, false);

            // Add required components
            RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 50);

            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.6f, 1f);

            launchButton = buttonObj.AddComponent<Button>();

            // Create text child
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRectTransform = textObj.AddComponent<RectTransform>();
            textRectTransform.anchorMin = Vector2.zero;
            textRectTransform.anchorMax = Vector2.one;
            textRectTransform.offsetMin = Vector2.zero;
            textRectTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = buttonText;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 24;
        }

        // Get the button component
        launchButton = buttonObj.GetComponent<Button>();
        if (!launchButton)
        {
            launchButton = buttonObj.GetComponentInChildren<Button>();
        }

        // Set button text if it exists
        TextMeshProUGUI buttonTextComponent = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonTextComponent)
        {
            buttonTextComponent.text = buttonText;
        }

        // Set button icon if it exists
        if (pingPongIcon)
        {
            Image iconImage = buttonObj.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImage)
            {
                iconImage.sprite = pingPongIcon;
            }
        }

        // Add click handler
        if (launchButton)
        {
            launchButton.onClick.AddListener(LaunchPingPongGame);
        }
    }

    public void LaunchPingPongGame()
    {
        // Log that we're launching the game
        SampleController.Log("Launching Ping Pong Game...");

        // Instantiate the ping pong game prefab if it exists
        if (pingPongGamePrefab)
        {
            if (gameInstance == null)
            {
                gameInstance = Instantiate(pingPongGamePrefab);
                StartCoroutine(SetupGameInstance());
            }
            else
            {
                // Game is already instantiated, make sure it's active
                gameInstance.SetActive(true);
            }
        }
        else
        {
            SampleController.LogError("PingPongLauncher: No ping pong game prefab assigned!");
        }
    }

    private IEnumerator SetupGameInstance()
    {
        // Give a moment for the game to initialize
        yield return new WaitForSeconds(0.5f);

        // Find the game initializer
        GameInitializer initializer = gameInstance.GetComponent<GameInitializer>();
        if (initializer == null)
        {
            initializer = gameInstance.GetComponentInChildren<GameInitializer>();
        }

        if (initializer)
        {
            // Connect to Photon
            initializer.Connect();
        }
        else
        {
            SampleController.LogError("PingPongLauncher: Could not find GameInitializer in the instantiated prefab.");
        }
    }
}