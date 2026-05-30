using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using NDream.AirConsole;

public enum GameState
{
    Lobby,
    Countdown,
    ActiveMatch,
    PostTag,
    MatchOver       // New state to stop everything when the timer hits 0
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameState CurrentState { get; private set; }

    [Header("Match Settings")]
    public float matchDuration = 120f; // 2 minutes total
    private float currentMatchTimer;

    // Tracks the exact amount of time each Device ID spends as the Snitch
    public Dictionary<int, float> snitchTimeScores = new Dictionary<int, float>();

    [Header("Prefabs & Spawns")]
    public GameObject hunterPrefab;
    public GameObject snitchPrefab;
    public Transform[] spawnPoints;


    [Header("Testing")]
    public bool debugSinglePlayer = false;
    public bool offlineMode = false; // Bypasses the browser completely



    private List<int> connectedDevices = new List<int>();
    private List<GameObject> activePlayers = new List<GameObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void OnEnable()
    {
        AirConsole.instance.onConnect += OnDeviceConnect;
        AirConsole.instance.onDisconnect += OnDeviceDisconnect;
    }

    void OnDisable()
    {
        if (AirConsole.instance != null)
        {
            AirConsole.instance.onConnect -= OnDeviceConnect;
            AirConsole.instance.onDisconnect -= OnDeviceDisconnect;
        }
    }

    void Start()
    {
        if (offlineMode)
        {
            // Instantly fake two connected phones and skip the Lobby
            connectedDevices.Add(0);
            connectedDevices.Add(1);
            StartMatchInit();
        }
        else
        {
            ChangeState(GameState.Lobby);
        }
    }

    // --- AIRCONSOLE CONNECTION LOGIC ---
    void OnDeviceConnect(int deviceId)
    {
        if (!connectedDevices.Contains(deviceId))
        {
            connectedDevices.Add(deviceId);
            Debug.Log($"Device {deviceId} connected. Total: {connectedDevices.Count}");

            int requiredPlayers = debugSinglePlayer ? 1 : 2;
            if (CurrentState == GameState.Lobby && connectedDevices.Count >= requiredPlayers)
            {
                StartMatchInit();
            }
        }
    }

    void OnDeviceDisconnect(int deviceId)
    {
        connectedDevices.Remove(deviceId);
        if (connectedDevices.Count < (debugSinglePlayer ? 1 : 2) && CurrentState != GameState.Lobby)
        {
            Debug.LogWarning("Not enough players! Returning to Lobby.");
            foreach (var player in activePlayers) { if (player != null) Destroy(player); }
            activePlayers.Clear();
            ChangeState(GameState.Lobby);
        }
    }

    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        Debug.Log($"Game State changed to: {newState}");
    }

    // --- TIMER & SCORING LOOP ---
    void Update()
    {
        // We only tick the clock down while they are actively playing
        if (CurrentState == GameState.ActiveMatch)
        {
            currentMatchTimer -= Time.deltaTime;

            // Give time-points to the current Snitch
            if (connectedDevices.Count > 0)
            {
                int currentSnitchID = connectedDevices[0]; // Device 0 is always the Snitch

                if (!snitchTimeScores.ContainsKey(currentSnitchID))
                {
                    snitchTimeScores[currentSnitchID] = 0f;
                }

                snitchTimeScores[currentSnitchID] += Time.deltaTime;
            }

            // Check if the match is over
            if (currentMatchTimer <= 0)
            {
                currentMatchTimer = 0;
                StartCoroutine(EndMatchRoutine());
            }
        }
    }

    // --- MATCH FLOW ---
    private void StartMatchInit()
    {
        // Reset scores and timers for a fresh game
        currentMatchTimer = matchDuration;
        snitchTimeScores.Clear();
        StartCoroutine(StartCountdownRoutine());
    }

    private IEnumerator StartCountdownRoutine()
    {
        ChangeState(GameState.Countdown);
        SpawnPlayers();

        Debug.Log("3...");
        yield return new WaitForSeconds(1f);
        Debug.Log("2...");
        yield return new WaitForSeconds(1f);
        Debug.Log("1...");
        yield return new WaitForSeconds(1f);
        Debug.Log("GO!");

        ChangeState(GameState.ActiveMatch);
    }

    private void SpawnPlayers()
    {
        foreach (var player in activePlayers) { if (player != null) Destroy(player); }
        activePlayers.Clear();

        int requiredPlayers = debugSinglePlayer ? 1 : 2;
        if (connectedDevices.Count < requiredPlayers) return;

        // 1. SPAWN SNITCH
        GameObject snitch = Instantiate(snitchPrefab, spawnPoints[0].position, Quaternion.identity);
        InputSystemManager snitchInput = snitch.GetComponent<InputSystemManager>();

        // The Snitch is ALWAYS Device 0 (or overridden by Jere's offline keyboard in InputManager)
        snitchInput.deviceID = connectedDevices[0];
        activePlayers.Add(snitch);

        // 2. SPAWN HUNTER
        GameObject hunter = Instantiate(hunterPrefab, spawnPoints[1].position, Quaternion.identity);
        InputSystemManager hunterInput = hunter.GetComponent<InputSystemManager>();

        // Only assign Device 1 if we aren't in debug single player mode
        if (!debugSinglePlayer && connectedDevices.Count > 1)
        {
            hunterInput.deviceID = connectedDevices[1];
        }
        activePlayers.Add(hunter);

        DynamicCamera cam = FindAnyObjectByType<DynamicCamera>();
        if (cam != null)
        {
            cam.targets = new List<Transform> { snitch.transform, hunter.transform };
            cam.SnapToTargets();
        }
    }

    // --- GAMEPLAY HOOKS ---
    public void OnPlayerTagged()
    {
        if (CurrentState != GameState.ActiveMatch) return;

        ChangeState(GameState.PostTag);

        // Print the current scores to the console just so you can see them updating!
        int snitchID = connectedDevices[0];
        Debug.Log($"TAG! Device {snitchID} held the Snitch for {snitchTimeScores[snitchID]:F1} seconds total so far.");

        StartCoroutine(ResetRoundRoutine());
    }

    private IEnumerator ResetRoundRoutine()
    {
        yield return new WaitForSeconds(2f);

        // Swap roles for the next mini-round
        if (connectedDevices.Count >= 2)
        {
            int temp = connectedDevices[0];
            connectedDevices[0] = connectedDevices[1];
            connectedDevices[1] = temp;
        }

        StartCoroutine(StartCountdownRoutine());
    }

    private IEnumerator EndMatchRoutine()
    {
        ChangeState(GameState.MatchOver);

        // Destroy players so they can't move anymore
        foreach (var player in activePlayers) { if (player != null) Destroy(player); }
        activePlayers.Clear();

        // Calculate the winner
        int winnerID = -1;
        float highestTime = -1f;

        foreach (var score in snitchTimeScores)
        {
            if (score.Value > highestTime)
            {
                highestTime = score.Value;
                winnerID = score.Key;
            }
        }

        Debug.Log($"============== MATCH OVER ==============");
        Debug.Log($"WINNER: Device {winnerID} with {highestTime:F1} seconds!");
        Debug.Log($"========================================");

        // Pause for a few seconds to let players see the result, then return to Lobby
        yield return new WaitForSeconds(5f);
        ChangeState(GameState.Lobby);

        // If phones are still connected, instantly restart the game
        if (connectedDevices.Count >= (debugSinglePlayer ? 1 : 2))
        {
            StartMatchInit();
        }
    }

    // ==========================================
    // --- UI HELPER METHODS (READ-ONLY) ---
    // ==========================================

    // 1. Get the current match timer (Returns seconds remaining)
    public float GetTimeRemaining()
    {
        return currentMatchTimer;
    }

    // 2. Get how many phones are actively connected
    public int GetConnectedPlayerCount()
    {
        return connectedDevices.Count;
    }

    // 3. Get the exact score (in seconds) for a specific phone
    public float GetPlayerScore(int deviceID)
    {
        if (snitchTimeScores.ContainsKey(deviceID))
        {
            return snitchTimeScores[deviceID];
        }
        return 0f; // Return 0 if they haven't held the Snitch yet
    }

    // 4. Identify who is currently holding the Snitch (Great for UI highlighting!)
    public int GetCurrentSnitchID()
    {
        // Because of our array-swap trick, Device 0 in the list is ALWAYS the active Snitch
        if (CurrentState == GameState.ActiveMatch && connectedDevices.Count > 0)
        {
            return connectedDevices[0];
        }
        return -1; // Returns -1 if the game hasn't started or is paused
    }
}