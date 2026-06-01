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


    //nico
    [Header("Lobby Settings")]
    [SerializeField] private GameObject lobbyPanel; // We will create this in the Unity Editor
    //

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
        
        //Restore opacity
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
        // 1. Esperamos el tiempo que el texto debe estar visible
        yield return new WaitForSeconds(seconds);

        if (notificationText.gameObject.activeSelf)
        {
            // 2. Solo ejecutamos la animación si pidieron un fade mayor a 0 segundos
            if (fadeDuration > 0f)
            {
                float currentTime = 0f;
                Color originalColor = notificationText.color;

                while (currentTime < fadeDuration)
                {
                    currentTime += Time.deltaTime;

                    // Calculamos el nuevo Alpha. Al estar dentro del IF, nos aseguramos de que fadeDuration nunca sea 0
                    float alpha = Mathf.Lerp(1f, 0f, currentTime / fadeDuration);
                    notificationText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

                    yield return null;
                }
            }

            // 3. Apagamos el texto (haya habido fade o no)
            notificationText.gameObject.SetActive(false);
        }
    }

    //nico
    public void ShowLobby()
    {
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
    }

    public void HideLobby()
    {
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
    }
    //
}