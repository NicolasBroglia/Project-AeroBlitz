using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("References (TextMeshPro)")]
    [SerializeField] private TextMeshProUGUI playersCountText;
    [SerializeField] private TextMeshProUGUI scoresText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI coundownText;

    [Header("Minigame & Notifications")]
    [Tooltip("Texto central para avisos: ¡Atrapada!, ¡Mash!, etc.")]
    [SerializeField] private TextMeshProUGUI notificationText;
    [Tooltip("Texto para el tiempo restante del minijuego de mashear")]
    [SerializeField] private TextMeshProUGUI clashTimerText;

    [Header("Lobby Settings")]
    [SerializeField] private GameObject lobbyPanel; 
    
    [Header("Lobby Mode Graphics")]
    [SerializeField] private GameObject pcKeybindsPanel;      // Drag the "Panel" with keybinds here
    [SerializeField] private GameObject pcStartButton;        // Drag "HostStartButton" here
    [SerializeField] private TextMeshProUGUI airConsoleWaitingText; // Drag "Waiting_To_Phones_To_Connect" here

    [Header("Timer optimization")]
    private int lastDisplayedTime = -1;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (timerText != null) timerText.text = "";
        if (scoresText != null) scoresText.text = "";
        if (playersCountText != null) playersCountText.text = "";
        if (coundownText != null) coundownText.text = "";

        if (notificationText != null)
        {
            notificationText.text = "";
            notificationText.gameObject.SetActive(false);
        }
        if (clashTimerText != null)
        {
            clashTimerText.text = "";
            clashTimerText.gameObject.SetActive(false);
        }
    }

    public void UpdateTimerDisplay(float timeRemaining)
    {
        int currentSeconds = Mathf.CeilToInt(timeRemaining);

        if (currentSeconds != lastDisplayedTime)
        {
            lastDisplayedTime = currentSeconds;
            timerText.text = currentSeconds.ToString();
        }
    }

    public void UpdateScoresDisplay(int currentSnitchId, List<int> connectedDevices)
    {
        if (currentSnitchId != -1)
            playersCountText.text = $"Snitch: Player {currentSnitchId}";
        else
            playersCountText.text = "Waiting for players...";

        StringBuilder sb = new StringBuilder();
        if (connectedDevices != null && connectedDevices.Count > 0)
        {
            foreach (int id in connectedDevices)
            {
                float score = GameManager.Instance.GetPlayerScore(id);
                sb.AppendLine($"Player {id}: {score:F1}s");
            }
        }

        scoresText.text = sb.ToString();
    }

    public void ShowCountdownText(string text)
    {
        if (!coundownText.gameObject.activeSelf) coundownText.gameObject.SetActive(true);
        coundownText.text = text;
    }

    public void HideCountdownText()
    {
        if (coundownText.gameObject.activeSelf) coundownText.gameObject.SetActive(false);
    }

    public void ShowNotification(string message)
    {
        if (!notificationText.gameObject.activeSelf) notificationText.gameObject.SetActive(true);
        
        Color c = notificationText.color;
        notificationText.color = new Color(c.r, c.g, c.b, 1f);
        notificationText.text = message;
    }

    public void HideNotification(float seconds, float fadeDuration = 0f)
    {
        StartCoroutine(ClearNotificationAfter(seconds, fadeDuration));
    }

    public void ShowClashTimer(float timeRemaining)
    {
        if (!clashTimerText.gameObject.activeSelf) clashTimerText.gameObject.SetActive(true);
        clashTimerText.text = $"TIME: {timeRemaining:F1}s";
    }

    public void HideClashTimer()
    {
        if (clashTimerText.gameObject.activeSelf) clashTimerText.gameObject.SetActive(false);
    }

    private IEnumerator ClearNotificationAfter(float seconds, float fadeDuration = 0f)
    {
        yield return new WaitForSeconds(seconds);

        if (notificationText.gameObject.activeSelf)
        {
            if (fadeDuration > 0f)
            {
                float currentTime = 0f;
                Color originalColor = notificationText.color;

                while (currentTime < fadeDuration)
                {
                    currentTime += Time.deltaTime;
                    float alpha = Mathf.Lerp(1f, 0f, currentTime / fadeDuration);
                    notificationText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                    yield return null;
                }
            }
            notificationText.gameObject.SetActive(false);
        }
    }

    // --- NEW DYNAMIC LOBBY LOGIC ---
    public void ShowLobby(GameManager.PlayMode mode)
    {
        if (lobbyPanel != null) lobbyPanel.SetActive(true);

        if (mode == GameManager.PlayMode.PCLocal)
        {
            // Show PC visuals, hide AirConsole text
            if (pcKeybindsPanel != null) pcKeybindsPanel.SetActive(true);
            if (pcStartButton != null) pcStartButton.SetActive(true);
            
            if (airConsoleWaitingText != null) 
            {
                airConsoleWaitingText.gameObject.SetActive(false);
            }
        }
        else if (mode == GameManager.PlayMode.AirConsole)
        {
            // Hide PC visuals, show AirConsole text
            if (pcKeybindsPanel != null) pcKeybindsPanel.SetActive(false);
            if (pcStartButton != null) pcStartButton.SetActive(false);
            
            if (airConsoleWaitingText != null) 
            {
                airConsoleWaitingText.gameObject.SetActive(true);
                airConsoleWaitingText.text = "Host: Press START on your phone";
            }
        }
    }

    public void HideLobby()
    {
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
    }
}