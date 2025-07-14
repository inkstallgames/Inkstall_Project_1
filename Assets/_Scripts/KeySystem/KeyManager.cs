using UnityEngine;
using TMPro;
using System.Collections;

public class KeyManager : MonoBehaviour
{
    public static KeyManager Instance;

    [Header("Key Settings")]
    [SerializeField] private int startingKeys = 0;
    private int currentKeys;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI keyCountText;
    [SerializeField] private TextMeshProUGUI doorPromptText;
    [SerializeField] private float promptDuration = 2.0f;

    // Keep track of the door the player is currently near
    private LockedDoor currentNearbyDoor;
    private Coroutine promptCoroutine;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize keys
        currentKeys = startingKeys;
        UpdateKeyCountUI();

        // Hide door prompt initially
        if (doorPromptText != null)
        {
            doorPromptText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Check for key press when near a locked door
        if (currentNearbyDoor != null && Input.GetKeyDown(KeyCode.F))
        {
            UnlockDoor(currentNearbyDoor);
        }
    }

    // Update the key count UI
    private void UpdateKeyCountUI()
    {
        if (keyCountText != null)
        {
            keyCountText.text = $"Keys: {currentKeys}";
        }
    }

    // Called when player enters the trigger zone of a locked door
    public void PlayerNearDoor(LockedDoor door)
    {
        currentNearbyDoor = door;
        ShowDoorPrompt(door);
    }

    // Called when player exits the trigger zone of a locked door
    public void PlayerLeftDoor(LockedDoor door)
    {
        if (currentNearbyDoor == door)
        {
            currentNearbyDoor = null;
            HideDoorPrompt();
        }
    }

    // Show the door prompt UI
    private void ShowDoorPrompt(LockedDoor door)
    {
        if (doorPromptText != null)
        {
            // Cancel any existing coroutine
            if (promptCoroutine != null)
            {
                StopCoroutine(promptCoroutine);
            }

            // Show appropriate message based on whether we have keys
            if (currentKeys > 0)
            {
                doorPromptText.text = "Door is locked. Press F to use a key.";
            }
            else
            {
                doorPromptText.text = "Not enough keys.";
            }

            doorPromptText.gameObject.SetActive(true);
        }
    }

    // Hide the door prompt UI
    private void HideDoorPrompt()
    {
        if (doorPromptText != null)
        {
            // Cancel any existing coroutine
            if (promptCoroutine != null)
            {
                StopCoroutine(promptCoroutine);
            }

            doorPromptText.gameObject.SetActive(false);
        }
    }

    // Show a temporary message
    public void ShowTemporaryMessage(string message)
    {
        if (doorPromptText != null)
        {
            // Cancel any existing coroutine
            if (promptCoroutine != null)
            {
                StopCoroutine(promptCoroutine);
            }

            doorPromptText.text = message;
            doorPromptText.gameObject.SetActive(true);

            // Start coroutine to hide the message after a delay
            promptCoroutine = StartCoroutine(HidePromptAfterDelay());
        }
    }

    // Coroutine to hide the prompt after a delay
    private IEnumerator HidePromptAfterDelay()
    {
        yield return new WaitForSeconds(promptDuration);
        doorPromptText.gameObject.SetActive(false);
    }

    // Try to unlock the door
    private void UnlockDoor(LockedDoor door)
    {
        if (currentKeys > 0)
        {
            // Decrease key count
            currentKeys--;
            UpdateKeyCountUI();

            // Unlock the door
            door.Unlock();

            // Show success message
            ShowTemporaryMessage("Door unlocked!");

            // Clear the current nearby door reference
            currentNearbyDoor = null;
        }
        else
        {
            // Show no keys message
            ShowTemporaryMessage("No keys available!");
        }
    }

    // Add keys to the player's inventory
    public void AddKeys(int amount)
    {
        currentKeys += amount;
        UpdateKeyCountUI();
        ShowTemporaryMessage($"Gained {amount} key" + (amount > 1 ? "s" : "") + "!");
    }

    // Get the current number of keys
    public int GetKeyCount()
    {
        return currentKeys;
    }
}
