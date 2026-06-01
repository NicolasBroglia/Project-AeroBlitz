using System.Text;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

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

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Vaciamos todos los textos apenas arranca la escena
        if (timerText != null) timerText.text = "";
        if (scoresText != null) scoresText.text = "";
        if (playersCountText != null) playersCountText.text = "";
        if (coundownText != null) coundownText.text = "";

        // Apagamos los textos de notificaciones por defecto
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
        timerText.text = Mathf.CeilToInt(timeRemaining).ToString();
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
        notificationText.text = message;
    }

    public void HideNotification()
    {
        if (notificationText.gameObject.activeSelf) notificationText.gameObject.SetActive(false);
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
}