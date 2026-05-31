using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using NDream.AirConsole;

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

    public GameState CurrentState { get; private set; }

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

    [Header("Testing")]
    [SerializeField] private bool debugSinglePlayer = false;
    [SerializeField] private bool offlineMode = false;
    [SerializeField][Range(2, 5)] private int offlinePlayerCount = 3;

    private const int MAX_PLAYERS = 5;

    [Tooltip("Uncheck to hide the OnGUI dashboard so it doesn't block the real UI")]
    [SerializeField] private bool showDebugGUI = true;

    [SerializeField] private List<int> connectedDevices = new List<int>();
    [SerializeField] private List<GameObject> activePlayers = new List<GameObject>();

    // Temporarily holds the players during the minigame
    [SerializeField] private HunterController clashingHunter;
    [SerializeField] private SnitchController clashingSnitch;

    [Header("Player Colors")]
    [SerializeField]
    private Color[] availableColors = new Color[]
    {
        Color.red, Color.cyan, Color.yellow, Color.magenta, new Color(1f, 0.5f, 0f) /* Orange */ 
    };
    private Dictionary<int, Color> deviceColors = new Dictionary<int, Color>();
    private int nextColorIndex = 0;

    void Awake() { if (Instance == null) Instance = this; }

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
            // Instantly fake 'N' connected phones and skip the Lobby
            for (int i = 0; i < offlinePlayerCount; i++)
            {
                connectedDevices.Add(i);

                // --- ADD THIS: Assign colors to offline bots ---
                deviceColors[i] = availableColors[nextColorIndex % availableColors.Length];
                nextColorIndex++;
            }
            StartMatchInit();
        }
        else ChangeState(GameState.Lobby);
    }

    [ContextMenu("DEBUG: Force Hunter Tag Win")]
    [ContextMenu("DEBUG: Force Hunter Tag Win")]
    public void DebugForceTag()
    {
        if (CurrentState == GameState.ActiveMatch && connectedDevices.Count > 1)
        {
            Debug.Log("<color=yellow>DEBUG: Forcing a Tag!</color>");
            // Simulate the first hunter in the list making the catch for testing
            OnPlayerTagged(connectedDevices[1]);
        }
    }
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

            if (CurrentState == GameState.Lobby && connectedDevices.Count >= requiredPlayers)
            {
                StartMatchInit();
            }
            else if (CurrentState == GameState.ActiveMatch)
            {
                SpawnPlayers();
                WakeUpPlayers(); // <--- THIS FIXES THE FREEZE
            }
            else if (CurrentState == GameState.Countdown)
            {
                // If they join during the countdown, just spawn them. 
                // The coroutine will wake them up naturally in a few seconds.
                SpawnPlayers();
            }
        }
    }

    [ContextMenu("DEBUG: Simulate New Phone Connecting")]
    public void DebugConnectPhone()
    {
        // Generates a random fake device ID
        OnDeviceConnect(Random.Range(100, 999));
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
    }

    void Update()
    {
        // Notice the timer ONLY ticks during ActiveMatch. 
        // When the Clash starts, the clock naturally stops!
        if (CurrentState == GameState.ActiveMatch)
        {
            currentMatchTimer -= Time.deltaTime;

            if (connectedDevices.Count > 0)
            {
                int currentSnitchID = connectedDevices[0];
                if (!snitchTimeScores.ContainsKey(currentSnitchID)) snitchTimeScores[currentSnitchID] = 0f;
                snitchTimeScores[currentSnitchID] += Time.deltaTime;
            }

            if (currentMatchTimer <= 0)
            {
                currentMatchTimer = 0;
                StartCoroutine(EndMatchRoutine());
            }
        }
    }

    // ==========================================
    // --- THE MINIGAME CLASH LOGIC --- 
    // ==========================================

    public void StartClash(HunterController hunter, SnitchController snitch)
    {
        if (CurrentState != GameState.ActiveMatch) return;

        ChangeState(GameState.Clash);
        clashingHunter = hunter;
        clashingSnitch = snitch;

        clashingHunter.ChangeState(HunterController.HunterState.Clashing);
        clashingSnitch.ChangeState(SnitchController.SnitchState.Clashing);

        Debug.Log("--- CLASH STARTED! ---");
        // TODO: En el futuro, acá instanciás el UI del Minijuego (Tug of War o Timing QTE)

        // TEMPORARY STUB: We will just auto-resolve it after 1 second for now
        // Delete this Invoke when you build the real UI!
        Invoke(nameof(StubResolveHunterWins), 1.5f);
    }

    // Temporary helper just to test the flow
    private void StubResolveHunterWins() { ResolveClash(true); }

    // Your future UI will call this function and pass 'true' if Hunter wins, 'false' if Snitch wins
    public void ResolveClash(bool hunterWins)
    {
        if (CurrentState != GameState.Clash) return;

        Debug.Log($"--- CLASH RESOLVED! Hunter Wins: {hunterWins} ---");

        if (hunterWins)
        {
            // --- NEW: Grab the specific winner's ID ---
            int winningHunterID = clashingHunter.GetComponent<InputSystemManager>().deviceID;

            clashingHunter.ChangeState(HunterController.HunterState.CapturedSnitch);
            clashingSnitch.ChangeState(SnitchController.SnitchState.Captured);

            // Pass the winner's ID to the next step
            OnPlayerTagged(winningHunterID);
        }
        else
        {
            // Snitch Escapes! 
            ChangeState(GameState.ActiveMatch);
            clashingSnitch.ChangeState(SnitchController.SnitchState.Fleeing);

            // Stun and knockback the Hunter so the Snitch can get away
            clashingHunter.ApplyKnockbackFrom(clashingSnitch.transform.position);
        }

        // Clean up references
        clashingHunter = null;
        clashingSnitch = null;
    }

    // ==========================================
    // --- MATCH FLOW ---
    // ==========================================

    private void StartMatchInit()
    {
        currentMatchTimer = matchDuration;
        snitchTimeScores.Clear();
        StartCoroutine(StartCountdownRoutine());
    }

    private IEnumerator StartCountdownRoutine()
    {
        ChangeState(GameState.Countdown);
        SpawnPlayers();

        Debug.Log("3..."); yield return new WaitForSeconds(1f);
        Debug.Log("2..."); yield return new WaitForSeconds(1f);
        Debug.Log("1..."); yield return new WaitForSeconds(1f);
        Debug.Log("GO!");

        ChangeState(GameState.ActiveMatch);

        // --- Manager-Driven Start ---
        WakeUpPlayers();
    }

    private void SpawnPlayers()
    {
        foreach (var player in activePlayers) { if (player != null) Destroy(player); }
        activePlayers.Clear();

        if (connectedDevices.Count == 0) return;

        List<Transform> camTargets = new List<Transform>();

        // Create a temporary "deck" of available hunter spawns to draw from
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

                    // Remove it from the list so the next Hunter can't spawn here
                    availableHunterSpawns.RemoveAt(randomIndex);
                }

                // --- Calculate rotation to face the Snitch (Center) ---
                // Math: Target Position - Origin Position = Direction Vector
                Vector3 directionToCenter = snitchSpawnPoint.position - chosenSpawn.position;

                // Flatten the Y axis so the Hunter stays perfectly upright
                directionToCenter.y = 0f;

                // Convert that direction into a rotation (Fallback to identity if they are exactly overlapping)
                Quaternion spawnRotation = directionToCenter != Vector3.zero
                    ? Quaternion.LookRotation(directionToCenter)
                    : Quaternion.identity;
                // -----------------------------------------------------------

                // Instantiate using our new spawnRotation instead of Quaternion.identity
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
    private void ApplyColorToPlayer(GameObject player, int deviceID)
    {
        if (deviceColors.TryGetValue(deviceID, out Color assignedColor))
        {
            // GetComponentsInChildren handles cases where your 3D mesh is on a child object
            Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in renderers)
            {
                // This automatically creates a material instance, so it won't permanently alter your project assets
                rend.material.color = assignedColor;
            }
        }
    }

    // Add the int parameter here
    public void OnPlayerTagged(int winningHunterID)
    {
        ChangeState(GameState.PostTag);
        int oldSnitchID = connectedDevices[0];

        Debug.Log($"TAG! Device {oldSnitchID} caught by Device {winningHunterID}!");
        Debug.Log($"Device {oldSnitchID} held the Snitch for {snitchTimeScores[oldSnitchID]:F1} seconds total so far.");

        // Pass it to the Coroutine
        StartCoroutine(ResetRoundRoutine(winningHunterID));
    }

    // Add the int parameter here
    private IEnumerator ResetRoundRoutine(int winningHunterID)
    {
        yield return new WaitForSeconds(2f);

        // --- NEW REAL SWAP MECHANIC ---
        if (connectedDevices.Count >= 2)
        {
            // Find where the winning hunter currently sits in the array
            int winnerIndex = connectedDevices.IndexOf(winningHunterID);

            if (winnerIndex != -1)
            {
                // Swap the old snitch (Index 0) with the winning hunter
                int oldSnitch = connectedDevices[0];
                connectedDevices[0] = winningHunterID;
                connectedDevices[winnerIndex] = oldSnitch;
            }
        }

        StartCoroutine(StartCountdownRoutine());
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
    private void WakeUpPlayers()
    {
        foreach (var player in activePlayers)
        {
            if (player != null)
            {
                // If the object has a SnitchController, tell it to flee
                if (player.TryGetComponent<SnitchController>(out var snitch))
                {
                    snitch.ChangeState(SnitchController.SnitchState.Fleeing);
                }

                // If the object has a HunterController, tell it to chase
                if (player.TryGetComponent<HunterController>(out var hunter))
                {
                    hunter.ChangeState(HunterController.HunterState.Chasing);
                }
            }
        }
    }



#if UNITY_EDITOR
    private void OnGUI()
    {
        // If we aren't playing, OR if you unchecked the box, do nothing.
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
        GUILayout.EndArea();
    }
#endif

    public float GetTimeRemaining() { return currentMatchTimer; }
    public int GetConnectedPlayerCount() { return connectedDevices.Count; }
    public float GetPlayerScore(int deviceID) { return snitchTimeScores.ContainsKey(deviceID) ? snitchTimeScores[deviceID] : 0f; }
    public int GetCurrentSnitchID() { return (CurrentState == GameState.ActiveMatch && connectedDevices.Count > 0) ? connectedDevices[0] : -1; }
}