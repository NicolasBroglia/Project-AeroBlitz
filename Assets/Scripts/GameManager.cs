using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using NDream.AirConsole;
using Random = UnityEngine.Random;

public enum GameState
{
    Lobby,
    Countdown,
    ActiveMatch,
    Clash,
    PostTag,
    MatchOver
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    #region Variables & Properties

    public GameState CurrentState { get; private set; }
    private const int MAX_PLAYERS = 5;
    public static event Action<GameState> OnGameStateChanged;

    [Header("Match Settings")]
    [SerializeField] private float matchDuration = 120f;
    [SerializeField] private float currentMatchTimer;
    [SerializeField] private Dictionary<int, float> snitchTimeScores = new Dictionary<int, float>();

    [Header("Prefabs & Spawns")]
    [SerializeField] private GameObject hunterPrefab;
    [SerializeField] private GameObject snitchPrefab;
    [Tooltip("The center of the map. The Snitch ALWAYS spawns here.")]
    [SerializeField] private Transform snitchSpawnPoint;
    [Tooltip("All the surrounding spots. Hunters are randomly placed here.")]
    [SerializeField] private Transform[] hunterSpawnPoints;

    [Header("Clash Settings")]
    [SerializeField] private float clashDuration = 2.5f;
    private float clashTimer;
    private int hunterClashScore = 0;
    private int snitchClashScore = 0;
    // Temporarily holds the players during the minigame
    [SerializeField] private HunterController clashingHunter;
    [SerializeField] private SnitchController clashingSnitch;

    [Header("Player Tracking & Colors")]
    [SerializeField] private List<int> connectedDevices = new List<int>();
    [SerializeField] private List<GameObject> activePlayers = new List<GameObject>();
    [SerializeField]
    private Color[] availableColors = new Color[]
    {
        Color.red, Color.cyan, Color.yellow, Color.magenta, new Color(1f, 0.5f, 0f) /* Orange */ 
    };
    private Dictionary<int, Color> deviceColors = new Dictionary<int, Color>();
    private int nextColorIndex = 0;

    [Header("Testing & Debug")]
    [SerializeField] private bool debugSinglePlayer = false;
    [SerializeField] private bool offlineMode = false;
    [SerializeField][Range(2, 5)] private int offlinePlayerCount = 3;
    [Tooltip("Uncheck to hide the OnGUI dashboard so it doesn't block the real UI")]
    [SerializeField] private bool showDebugGUI = true;

    #endregion

    #region Unity Lifecycle

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
            // Instantly fake 'N' connected phones
            for (int i = 0; i < offlinePlayerCount; i++)
            {
                connectedDevices.Add(i);

                deviceColors[i] = availableColors[nextColorIndex % availableColors.Length];
                nextColorIndex++;
            }

            Debug.Log($"Offline mode generated {offlinePlayerCount} players. Waiting in Lobby.");
        }

        // ALWAYS start in the Lobby, waiting for the Start Match button, 
        // regardless of whether we are online or offline
        ChangeState(GameState.Lobby);
    }

    void Update()
    {
        if (CurrentState == GameState.ActiveMatch)
        {
            currentMatchTimer -= Time.deltaTime;

            //UI updates match timer
            UIManager.Instance.UpdateTimerDisplay(currentMatchTimer);

            if (connectedDevices.Count > 0)
            {
                int currentSnitchID = connectedDevices[0];
                if (!snitchTimeScores.ContainsKey(currentSnitchID)) snitchTimeScores[currentSnitchID] = 0f;
                snitchTimeScores[currentSnitchID] += Time.deltaTime;

                //UI updates snitch's score
                UIManager.Instance.UpdateScoresDisplay(currentSnitchID, connectedDevices);
            }

            if (currentMatchTimer <= 0)
            {
                currentMatchTimer = 0;
                StartCoroutine(EndMatchRoutine());
            }
        }
        else if (CurrentState == GameState.Clash)
        {
            clashTimer -= Time.deltaTime;

            //UI updates clash timer on screen
            UIManager.Instance.ShowClashTimer(clashTimer);

            // Constantly poll for mash inputs from both players
            if (clashingHunter != null && clashingHunter.GetComponent<InputSystemManager>().GetMash())
            {
                hunterClashScore++;
            }
            if (clashingSnitch != null && clashingSnitch.GetComponent<InputSystemManager>().GetMash())
            {
                snitchClashScore++;
            }

            if (clashTimer <= 0)
            {
                // Time's up! Resolve the tie-breaker
                // Defender's Advantage: Hunter MUST strictly beat the Snitch's score to capture
                bool hunterWins = hunterClashScore > snitchClashScore;
                ResolveClash(hunterWins);
            }
        }
    }

    #endregion

    #region AirConsole Callbacks

    void OnDeviceConnect(int deviceId)
    {
        if (!connectedDevices.Contains(deviceId) && connectedDevices.Count >= MAX_PLAYERS)
        {
            Debug.LogWarning($"Game is full! Device {deviceId} connected as a spectator.");
            return;
        }

        if (!connectedDevices.Contains(deviceId))
        {
            connectedDevices.Add(deviceId);

            // Assign a permanent color to this device
            if (!deviceColors.ContainsKey(deviceId))
            {
                deviceColors[deviceId] = availableColors[nextColorIndex % availableColors.Length];
                nextColorIndex++;
            }

            int requiredPlayers = debugSinglePlayer ? 1 : 2;

            // If we are in an Active Match, drop them in dynamically
            if (CurrentState == GameState.ActiveMatch)
            {
                SpawnSinglePlayerAsHunter(deviceId);
            }
            else if (CurrentState == GameState.Countdown)
            {
                // If they join during the 3..2..1 countdown, just re-rack the spawn for everyone
                SpawnPlayers();
            }

            // Notice: If CurrentState == Lobby, we do NOTHING. They just sit in the list!
        }
        //UI updates players labels
        UIManager.Instance.UpdateScoresDisplay(GetCurrentSnitchID(), connectedDevices);
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
        //UI updates players labels
        UIManager.Instance.UpdateScoresDisplay(GetCurrentSnitchID(), connectedDevices);

    }

    #endregion

    #region State & Match Flow

    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState);

        if (AirConsole.instance != null)
        {
            try
            {
                // --- Send specific targets if a clash is happening ---
                if (newState == GameState.Clash && clashingHunter != null && clashingSnitch != null)
                {
                    int hId = clashingHunter.GetComponent<InputSystemManager>().deviceID;
                    int sId = clashingSnitch.GetComponent<InputSystemManager>().deviceID;

                    AirConsole.instance.Broadcast(new
                    {
                        gameState = newState.ToString(),
                        clashingIDs = new int[] { hId, sId } // Inject the IDs here
                    });
                }
                else
                {
                    // Normal broadcast for Lobby, Countdown, etc.
                    AirConsole.instance.Broadcast(new { gameState = newState.ToString() });
                }
            }
            catch (System.Exception)
            {
                // Safely ignored
            }
        }
    }

    public void RequestStartMatch()
    {
        if (CurrentState != GameState.Lobby) return;

        int requiredPlayers = debugSinglePlayer ? 1 : 2;

        if (connectedDevices.Count >= requiredPlayers)
        {
            Debug.Log("Lobby ready! Starting match...");
            StartMatchInit();
        }
        else
        {
            Debug.LogWarning($"Not enough players! Need at least {requiredPlayers}.");
        }
    }

    private void StartMatchInit()
    {
        currentMatchTimer = matchDuration;
        snitchTimeScores.Clear();
        StartCoroutine(StartCountdownRoutine());
    }

    private IEnumerator StartCountdownRoutine()
    {
        //UI clears the screen before countdown
        UIManager.Instance.HideNotification();

        ChangeState(GameState.Countdown);
        SpawnPlayers();

        //UI displays the countdown on screen
        UIManager.Instance.ShowCountdownText("3");
        yield return new WaitForSeconds(1f);

        UIManager.Instance.ShowCountdownText("2");
        yield return new WaitForSeconds(1f);

        UIManager.Instance.ShowCountdownText("1");
        yield return new WaitForSeconds(1f);

        UIManager.Instance.ShowCountdownText("GO!");

        ChangeState(GameState.ActiveMatch);
        WakeUpPlayers();

        //UI Waits 1s and hides the countdown text
        yield return new WaitForSeconds(1f);
        UIManager.Instance.HideCountdownText();
    }

    private IEnumerator EndMatchRoutine()
    {
        ChangeState(GameState.MatchOver);

        foreach (var player in activePlayers) { if (player != null) Destroy(player); }
        activePlayers.Clear();

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

        //UI notifies the winner
        UIManager.Instance.ShowNotification($"<color=green>WINNER: Device {winnerID} with {highestTime:F1} seconds!</color>"); 
        StartCoroutine(ClearNotificationAfter(5f));

        Debug.Log($"============== MATCH OVER ==============");
        Debug.Log($"WINNER: Device {winnerID} with {highestTime:F1} seconds!");
        Debug.Log($"========================================");

        yield return new WaitForSeconds(5f);
        ChangeState(GameState.Lobby);

        if (connectedDevices.Count >= (debugSinglePlayer ? 1 : 2))
        {
            StartMatchInit();
        }
    }

    #endregion

    #region Spawning & Player Management

    private void SpawnPlayers()
    {
        foreach (var player in activePlayers) { if (player != null) Destroy(player); }
        activePlayers.Clear();

        if (connectedDevices.Count == 0) return;

        List<Transform> camTargets = new List<Transform>();
        List<Transform> availableHunterSpawns = new List<Transform>(hunterSpawnPoints);

        for (int i = 0; i < connectedDevices.Count; i++)
        {
            if (i == 0)
            {
                // Player 0 (Snitch) ALWAYS goes to the dedicated middle spot
                GameObject snitch = Instantiate(snitchPrefab, snitchSpawnPoint.position, Quaternion.identity);
                snitch.GetComponent<InputSystemManager>().deviceID = connectedDevices[i];
                activePlayers.Add(snitch);
                ApplyColorToPlayer(snitch, connectedDevices[i]);
                camTargets.Add(snitch.transform);
            }
            else
            {
                // Safety fallback just in case you forget to assign hunter spawn points
                Transform chosenSpawn = snitchSpawnPoint;

                // Draw a random, unique spawn point for this Hunter
                if (availableHunterSpawns.Count > 0)
                {
                    int randomIndex = Random.Range(0, availableHunterSpawns.Count);
                    chosenSpawn = availableHunterSpawns[randomIndex];
                    availableHunterSpawns.RemoveAt(randomIndex);
                }

                // Calculate rotation to face the Snitch (Center)
                Vector3 directionToCenter = snitchSpawnPoint.position - chosenSpawn.position;
                directionToCenter.y = 0f;

                Quaternion spawnRotation = directionToCenter != Vector3.zero
                    ? Quaternion.LookRotation(directionToCenter)
                    : Quaternion.identity;

                GameObject hunter = Instantiate(hunterPrefab, chosenSpawn.position, spawnRotation);
                hunter.GetComponent<InputSystemManager>().deviceID = connectedDevices[i];
                ApplyColorToPlayer(hunter, connectedDevices[i]);
                activePlayers.Add(hunter);
                camTargets.Add(hunter.transform);
            }
        }

        // Update the dynamic camera with all spawned players
        DynamicCamera cam = FindAnyObjectByType<DynamicCamera>();
        if (cam != null)
        {
            cam.targets = camTargets;
            cam.SnapToTargets();
        }
    }

    private void SpawnSinglePlayerAsHunter(int newDeviceID)
    {
        Transform chosenSpawn = snitchSpawnPoint;
        if (hunterSpawnPoints.Length > 0)
        {
            chosenSpawn = hunterSpawnPoints[Random.Range(0, hunterSpawnPoints.Length)];
        }

        Vector3 directionToCenter = snitchSpawnPoint.position - chosenSpawn.position;
        directionToCenter.y = 0f;
        Quaternion spawnRotation = directionToCenter != Vector3.zero
            ? Quaternion.LookRotation(directionToCenter)
            : Quaternion.identity;

        GameObject hunter = Instantiate(hunterPrefab, chosenSpawn.position, spawnRotation);
        hunter.GetComponent<InputSystemManager>().deviceID = newDeviceID;
        ApplyColorToPlayer(hunter, newDeviceID);

        activePlayers.Add(hunter);

        // Instantly wake them up (Skip the idle/countdown phase)
        if (hunter.TryGetComponent<HunterController>(out var controller))
        {
            controller.ChangeState(HunterController.HunterState.Chasing);
        }

        DynamicCamera cam = FindAnyObjectByType<DynamicCamera>();
        if (cam != null)
        {
            cam.targets.Add(hunter.transform);
        }

        Debug.Log($"Device {newDeviceID} dropped in mid-match as a Hunter!");
    }

    private void WakeUpPlayers()
    {
        foreach (var player in activePlayers)
        {
            if (player != null)
            {
                if (player.TryGetComponent<SnitchController>(out var snitch))
                {
                    snitch.ChangeState(SnitchController.SnitchState.Fleeing);
                }

                if (player.TryGetComponent<HunterController>(out var hunter))
                {
                    hunter.ChangeState(HunterController.HunterState.Chasing);
                }
            }
        }
    }

    private void ApplyColorToPlayer(GameObject player, int deviceID)
    {
        if (deviceColors.TryGetValue(deviceID, out Color assignedColor))
        {
            Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in renderers)
            {
                rend.material.color = assignedColor;
            }
        }
    }

    #endregion

    #region Clash & Tag Minigame

    public void StartClash(HunterController hunter, SnitchController snitch)
    {
        if (CurrentState != GameState.ActiveMatch) return;

        // 1. SET THESE FIRST so the ChangeState broadcast can read them
        clashingHunter = hunter;
        clashingSnitch = snitch;

        // 2. NOW change the state and broadcast
        ChangeState(GameState.Clash);

        clashingHunter.ChangeState(HunterController.HunterState.Clashing);
        clashingSnitch.ChangeState(SnitchController.SnitchState.Clashing);

        Debug.Log("--- CLASH STARTED! MASH! ---");

        clashTimer = clashDuration;
        hunterClashScore = 0;
        snitchClashScore = 0;

        //UI notifies clash started
        UIManager.Instance.ShowNotification("<color=red>¡MASH BUTTONS!</color>");
    }

    public void ResolveClash(bool hunterWins)
    {
        if (CurrentState != GameState.Clash) return;

        //UI hides clash timer
        UIManager.Instance.HideClashTimer();

        Debug.Log($"--- CLASH RESOLVED! Hunter Wins: {hunterWins} ---");

        if (hunterWins)
        {
            int winningHunterID = clashingHunter.GetComponent<InputSystemManager>().deviceID;

            clashingHunter.ChangeState(HunterController.HunterState.CapturedSnitch);
            clashingSnitch.ChangeState(SnitchController.SnitchState.Captured);

            OnPlayerTagged(winningHunterID);
        }
        else
        {
            // Snitch Escapes
            ChangeState(GameState.ActiveMatch);
            clashingSnitch.ChangeState(SnitchController.SnitchState.Fleeing);
            clashingHunter.ApplyKnockbackFrom(clashingSnitch.transform.position);

            //UI notifies snitch scaped
            UIManager.Instance.ShowNotification("<color=yellow>¡THE SNITCH GOT AWAY!</color>");
            //Hide after 2s
            StartCoroutine(ClearNotificationAfter(2f));
        }

        clashingHunter = null;
        clashingSnitch = null;
    }

    public void OnPlayerTagged(int winningHunterID)
    {
        ChangeState(GameState.PostTag);
        int oldSnitchID = connectedDevices[0];

        Debug.Log($"TAG! Device {oldSnitchID} caught by Device {winningHunterID}!");
        Debug.Log($"Device {oldSnitchID} held the Snitch for {snitchTimeScores[oldSnitchID]:F1} seconds total so far.");

        //UI notifies who caught the snitch
        UIManager.Instance.ShowNotification($"¡PLAYER {winningHunterID} CAUGHT THE SNITCH!");
        UIManager.Instance.UpdateScoresDisplay(GetCurrentSnitchID(), connectedDevices);

        StartCoroutine(ResetRoundRoutine(winningHunterID));

        //UI updates the snitch player
        UIManager.Instance.UpdateScoresDisplay(GetCurrentSnitchID(), connectedDevices);
    }

    private IEnumerator ResetRoundRoutine(int winningHunterID)
    {
        yield return new WaitForSeconds(2f);

        // Swap the winning hunter into the Snitch role
        if (connectedDevices.Count >= 2)
        {
            int winnerIndex = connectedDevices.IndexOf(winningHunterID);

            if (winnerIndex != -1)
            {
                int oldSnitch = connectedDevices[0];
                connectedDevices[0] = winningHunterID;
                connectedDevices[winnerIndex] = oldSnitch;
            }
        }

        StartCoroutine(StartCountdownRoutine());
    }

    #endregion

    #region Public API (Getters)

    public float GetTimeRemaining() { return currentMatchTimer; }
    public int GetConnectedPlayerCount() { return connectedDevices.Count; }
    public float GetPlayerScore(int deviceID) { return snitchTimeScores.ContainsKey(deviceID) ? snitchTimeScores[deviceID] : 0f; }
    public int GetCurrentSnitchID() { return (CurrentState == GameState.ActiveMatch && connectedDevices.Count > 0) ? connectedDevices[0] : -1; }
    public List<int> GetConnectedDevices() { return connectedDevices; }
    #endregion

    #region UI
    private IEnumerator ClearNotificationAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        UIManager.Instance.HideNotification();
    }

    #endregion

    #region Debug & Editor GUI

    [ContextMenu("DEBUG: Force Hunter Tag Win")]
    public void DebugForceTag()
    {
        if (CurrentState == GameState.ActiveMatch && connectedDevices.Count > 1)
        {
            Debug.Log("<color=yellow>DEBUG: Forcing a Tag!</color>");
            OnPlayerTagged(connectedDevices[1]);
        }
    }

    [ContextMenu("DEBUG: Simulate New Phone Connecting")]
    public void DebugConnectPhone()
    {
        OnDeviceConnect(Random.Range(100, 999));
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!Application.isPlaying || !showDebugGUI) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, 400), GUI.skin.box);
        GUILayout.Label($"<b>MATCH STATE:</b> {CurrentState}");
        GUILayout.Label($"<b>TIMER:</b> {currentMatchTimer:F1}s");
        GUILayout.Label($"<b>CONNECTED DEVICES:</b> {connectedDevices.Count}");

        GUILayout.Space(10);
        GUILayout.Label("<b>--- PLAYER ROLES & SCORES ---</b>");

        if (connectedDevices.Count > 0)
        {
            int snitchID = connectedDevices[0];
            Color sColor = deviceColors.TryGetValue(snitchID, out var sc) ? sc : Color.white;
            GUILayout.Label($"<color=#{ColorUtility.ToHtmlStringRGB(sColor)}>▶ DEVICE {snitchID} [SNITCH]</color> - Score: {GetPlayerScore(snitchID):F1}s");

            for (int i = 1; i < connectedDevices.Count; i++)
            {
                int hunterID = connectedDevices[i];
                Color hColor = deviceColors.TryGetValue(hunterID, out var hc) ? hc : Color.white;
                GUILayout.Label($"<color=#{ColorUtility.ToHtmlStringRGB(hColor)}>▷ DEVICE {hunterID} [HUNTER]</color> - Score: {GetPlayerScore(hunterID):F1}s");
            }
        }

        if (CurrentState == GameState.Lobby)
        {
            GUILayout.Space(10);
            if (GUILayout.Button("START MATCH (DEBUG)", GUILayout.Height(40)))
            {
                RequestStartMatch();
            }
        }

        if (CurrentState == GameState.Clash)
        {
            GUILayout.Space(15);
            GUILayout.Label("<color=yellow><b>--- TUG OF WAR ---</b></color>");
            GUILayout.Label($"TIME LEFT: {clashTimer:F1}s");

            string hColor = hunterClashScore > snitchClashScore ? "yellow" : "white";
            string sColor = snitchClashScore >= hunterClashScore ? "yellow" : "white";

            GUILayout.Label($"<color={hColor}>HUNTER MASHES:</color> {hunterClashScore}");
            GUILayout.Label($"<color={sColor}>SNITCH MASHES:</color> {snitchClashScore}");
        }

        GUILayout.EndArea();
    }
#endif

    #endregion
}