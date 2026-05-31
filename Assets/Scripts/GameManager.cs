using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using NDream.AirConsole;

public enum GameState
{
    Lobby,
    Countdown,
    ActiveMatch,
    Clash,          // <--- NEW: Pauses timer and movement for the Minigame
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
    [SerializeField] private Transform[] spawnPoints;

    [Header("Testing")]
    [SerializeField] private bool debugSinglePlayer = false;
    [SerializeField] private bool offlineMode = false;

    [SerializeField] private List<int> connectedDevices = new List<int>();
    [SerializeField] private List<GameObject> activePlayers = new List<GameObject>();

    // Temporarily holds the players during the minigame
    [SerializeField] private HunterController clashingHunter;
    [SerializeField] private SnitchController clashingSnitch;

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
            connectedDevices.Add(0);
            connectedDevices.Add(1);
            StartMatchInit();
        }
        else ChangeState(GameState.Lobby);
    }

    void OnDeviceConnect(int deviceId)
    {
        if (!connectedDevices.Contains(deviceId))
        {
            connectedDevices.Add(deviceId);

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
            clashingHunter.ChangeState(HunterController.HunterState.CapturedSnitch);
            clashingSnitch.ChangeState(SnitchController.SnitchState.Captured);
            OnPlayerTagged();
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

        // Players spawn in their Idle states automatically
        SpawnPlayers();

        Debug.Log("3..."); yield return new WaitForSeconds(1f);
        Debug.Log("2..."); yield return new WaitForSeconds(1f);
        Debug.Log("1..."); yield return new WaitForSeconds(1f);
        Debug.Log("GO!");

        ChangeState(GameState.ActiveMatch);

        // --- BEST PRACTICE: MANAGER-DRIVEN START ---
        // The exact millisecond the match starts, tell the frozen players to move
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

    private void SpawnPlayers()
    {
        foreach (var player in activePlayers) { if (player != null) Destroy(player); }
        activePlayers.Clear();

        int requiredPlayers = debugSinglePlayer ? 1 : 2;
        if (connectedDevices.Count < requiredPlayers) return;

        GameObject snitch = Instantiate(snitchPrefab, spawnPoints[0].position, Quaternion.identity);
        InputSystemManager snitchInput = snitch.GetComponent<InputSystemManager>();
        snitchInput.deviceID = connectedDevices[0];
        activePlayers.Add(snitch);

        GameObject hunter = Instantiate(hunterPrefab, spawnPoints[1].position, Quaternion.identity);
        InputSystemManager hunterInput = hunter.GetComponent<InputSystemManager>();

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

    public void OnPlayerTagged()
    {
        ChangeState(GameState.PostTag);
        int snitchID = connectedDevices[0];
        Debug.Log($"TAG! Device {snitchID} held the Snitch for {snitchTimeScores[snitchID]:F1} seconds total so far.");
        StartCoroutine(ResetRoundRoutine());
    }

    private IEnumerator ResetRoundRoutine()
    {
        yield return new WaitForSeconds(2f);
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

    public float GetTimeRemaining() { return currentMatchTimer; }
    public int GetConnectedPlayerCount() { return connectedDevices.Count; }
    public float GetPlayerScore(int deviceID) { return snitchTimeScores.ContainsKey(deviceID) ? snitchTimeScores[deviceID] : 0f; }
    public int GetCurrentSnitchID() { return (CurrentState == GameState.ActiveMatch && connectedDevices.Count > 0) ? connectedDevices[0] : -1; }
}