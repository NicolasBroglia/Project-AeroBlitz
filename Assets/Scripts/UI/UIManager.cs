using System.Text;
using UnityEngine;
using TMPro;
using NDream.AirConsole;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("References (TextMeshPro)")]
    [SerializeField] private TextMeshProUGUI playersCountText;
    [SerializeField] private TextMeshProUGUI scoresText;
    [SerializeField] private TextMeshProUGUI timerText;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnEnable()
    {
        if (AirConsole.instance != null)
        {
            AirConsole.instance.onConnect += OnDeviceConnect;
            AirConsole.instance.onDisconnect += OnDeviceDisconnect;
            AirConsole.instance.onReady += OnAirConsoleReady;
        }
    }

    void OnDisable()
    {
        if (AirConsole.instance != null)
        {
            AirConsole.instance.onConnect -= OnDeviceConnect;
            AirConsole.instance.onDisconnect -= OnDeviceDisconnect;
            AirConsole.instance.onReady -= OnAirConsoleReady;
        }
    }

    void Start()
    {
        // Si el plugin ya está listo, refrescamos; si no, esperamos al evento onReady.
        if (AirConsole.instance != null && AirConsole.instance.IsAirConsoleUnityPluginReady())
        {
            RefreshAll();
        }
        else
        {
            // Muestra estado inicial mínimo (sin depender de AirConsole)
            RefreshTimer();
        }
    }

    void Update()
    {
        // Timer en tiempo real
        RefreshTimer();
    }

    private void OnDeviceConnect(int deviceId) => RefreshPlayersAndScores();
    private void OnDeviceDisconnect(int deviceId) => RefreshPlayersAndScores();

    private void OnAirConsoleReady(string code)
    {
        // Plugin listo: actualizamos y nos desuscribimos
        RefreshAll();
        if (AirConsole.instance != null)
        {
            AirConsole.instance.onReady -= OnAirConsoleReady;
        }
    }

    public void RefreshAll()
    {
        RefreshPlayersAndScores();
        RefreshTimer();
    }

    private void RefreshPlayersAndScores()
    {
        int connected = 0;
        System.Collections.Generic.List<int> controllers = null;

        if (AirConsole.instance != null)
        {
            try
            {
                // Comprobación defensiva; IsAirConsoleUnityPluginReady ya gestionado en Start/OnAirConsoleReady
                if (AirConsole.instance.IsAirConsoleUnityPluginReady())
                {
                    controllers = AirConsole.instance.GetControllerDeviceIds();
                    connected = controllers?.Count ?? 0;
                }
            }
            catch (AirConsole.NotReadyException)
            {
                // Si por alguna razón se lanza, evitamos que la UI se rompa y mantenemos valores por defecto.
                controllers = null;
                connected = 0;
            }
        }

        if (playersCountText != null) playersCountText.text = $"Jugadores: {connected}";

        if (scoresText != null)
        {
            var sb = new StringBuilder();
            if (controllers == null || controllers.Count == 0)
            {
                sb.AppendLine("Puntajes: —");
            }
            else
            {
                sb.AppendLine("Puntajes:");
                int currentSnitch = GameManager.Instance != null ? GameManager.Instance.GetCurrentSnitchID() : -1;

                foreach (int deviceId in controllers)
                {
                    float score = GameManager.Instance != null ? GameManager.Instance.GetPlayerScore(deviceId) : 0f;
                    string marker = deviceId == currentSnitch ? " (Snitch)" : "";
                    sb.AppendLine($"#{deviceId}: {score:0.00}s{marker}");
                }
            }

            scoresText.text = sb.ToString();
        }
    }

    private void RefreshTimer()
    {
        if (timerText == null || GameManager.Instance == null) return;
        float remaining = GameManager.Instance.GetTimeRemaining();
        timerText.text = FormatTime(remaining);
    }

    private static string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{mins:00}:{secs:00}";
    }
}
