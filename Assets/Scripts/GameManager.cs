using UnityEngine;
using System;
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

    public enum PlayMode { None, PCLocal, AirConsole }
    public PlayMode CurrentPlayMode { get; private set; } = PlayMode.None;

    #region Variables & Properties

    public GameState CurrentState { get; private set; }
    private const int MAX_PLAYERS = 5;
    public static event Action<GameState> OnGameStateChanged;

    [Header("Mode & Networking")]
    [SerializeField] private GameObject airConsoleManagerObject;

    [Header("Match Settings")]
    [SerializeField] private float matchDuration = 120f;
    [SerializeField] private float currentMatchTimer;
    [SerializeField] private Dictionary<int, int> playerScores = new Dictionary<int, int>(); // RESOLVED: Using Friend's Int Score

    [Header("Clash Settings")]
    [SerializeField] private float clashDuration = 2.5f;
    private float clashTimer;
    private int hunterClashScore = 0;
    private int snitchClashScore = 0;
    private HunterController clashingHunter;
    private SnitchController clashingSnitch;
    private InputSystemManager clashingHunterInput;
    private InputSystemManager clashingSnitchInput;

    [Header("Player Tracking & Colors")]
    [SerializeField] private List<int> connectedDevices = new List<int>();
    [SerializeField] private List<GameObject> activePlayers = new List<GameObject>();
    [SerializeField]
    private Color[] availableColors = new Color[]
    {
        Color.red, Color.cyan, Color.yellow, Color.magenta, new Color(1f, 0.5f, 0f)
    };
    private Dictionary<int, Color> deviceColors = new Dictionary<int, Color>();
    private int nextColorIndex = 0;

    public int localPlayerCount = 0;
    private int currentBotCount = 0;

    [Header("Testing & Debug")]
    [SerializeField] private bool showDebugGUI = true;

    private float uiUpdateTimer = 0f;
    private const float UI_UPDATE_RATE = 0.1f;

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
        ChangeState(GameState.Lobby);
    }

    void Update()
    {
        if (CurrentState == GameState.ActiveMatch)
        {
            currentMatchTimer -= Time.deltaTime;
            UIManager.Instance.UpdateTimerDisplay(currentMatchTimer);

            // RESOLVED: We don't add time to score anymore, we just update the UI occasionally
            if (connectedDevices.Count > 0)
            {
                uiUpdateTimer += Time.deltaTime;
                if (uiUpdateTimer >= UI_UPDATE_RATE)
                {
                    UIManager.Instance.UpdateScoresDisplay(connectedDevices[0], connectedDevices);
                    uiUpdateTimer = 0f;
                }
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
            UIManager.Instance.ShowClashTimer(clashTimer);

            if (clashingHunterInput != null && clashingHunterInput.GetMash()) hunterClashScore++;
            if (clashingSnitchInput != null && clashingSnitchInput.GetMash()) snitchClashScore++;

            if (clashTimer <= 0)
            {
                bool hunterWins = hunterClashScore > snitchClashScore;
                ResolveClash(hunterWins);
            }
        }
    }

    #endregion

    #region Mode & Roster Management

    public void SetPlayMode(PlayMode mode)
    {
        CurrentPlayMode = mode;

        if (mode == PlayMode.AirConsole)
        {
            if (airConsoleManagerObject != null) airConsoleManagerObject.SetActive(true);
        }
        else if (mode == PlayMode.PCLocal)
        {
            if (airConsoleManagerObject != null) airConsoleManagerObject.SetActive(false);
        }

        connectedDevices.Clear();
        deviceColors.Clear();
        localPlayerCount = 0;
        currentBotCount = 0;
        nextColorIndex = 0;

        if (mode == PlayMode.PCLocal)
        {
            AddLocalPlayer();
            AddLocalPlayer();
        }
    }

    public void RequestStartMatch()
    {
        if (CurrentState != GameState.Lobby) return;

        int requiredPlayers = 2;

        if (connectedDevices.Count >= requiredPlayers) StartMatchInit();
        else Debug.LogWarning($"Cannot start match: Need {requiredPlayers} players, but only {connectedDevices.Count} are registered.");
    }

    public void AddLocalPlayer()
    {
        if (connectedDevices.Count >= MAX_PLAYERS) return;
        if (localPlayerCount >= 2) return;

        int newId = 800 + localPlayerCount;
        localPlayerCount++;

        connectedDevices.Add(newId);
        deviceColors[newId] = availableColors[nextColorIndex % availableColors.Length];
        nextColorIndex++;
    }

    public void AddBot()
    {
        if (connectedDevices.Count >= MAX_PLAYERS) return;

        currentBotCount++;
        int botID = -currentBotCount;

        connectedDevices.Add(botID);
        deviceColors[botID] = availableColors[nextColorIndex % availableColors.Length];
        nextColorIndex++;
    }

    public void RemoveBot()
    {
        if (currentBotCount <= 0) return;

        int botID = -currentBotCount;
        connectedDevices.Remove(botID);
        deviceColors.Remove(botID);
        currentBotCount--;
    }

    #endregion

    #region AirConsole Callbacks

    void OnDeviceConnect(int deviceId)
    {
        if (!connectedDevices.Contains(deviceId) && connectedDevices.Count >= MAX_PLAYERS) return;

        if (!connectedDevices.Contains(deviceId))
        {
            connectedDevices.Add(deviceId);

            if (!deviceColors.ContainsKey(deviceId))
            {
                deviceColors[deviceId] = availableColors[nextColorIndex % availableColors.Length];
                nextColorIndex++;
            }

            if (CurrentState == GameState.ActiveMatch)
            {
                GameObject newHunter = SpawnManager.Instance.SpawnSinglePlayerAsHunter(deviceId);
                activePlayers.Add(newHunter);
                ApplyColorToPlayer(newHunter, deviceId);
            }
            else if (CurrentState == GameState.Countdown)
            {
                RebuildAllPlayers();
            }
        }
        UIManager.Instance.UpdateScoresDisplay(GetCurrentSnitchID(), connectedDevices);
    }

    void OnDeviceDisconnect(int deviceId)
    {
        connectedDevices.Remove(deviceId);

        if (connectedDevices.Count < 2 && CurrentState != GameState.Lobby)
        {
            foreach (var player in activePlayers) { if (player != null) Destroy(player); }
            activePlayers.Clear();
            ChangeState(GameState.Lobby);
        }
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
                if (newState == GameState.Clash && clashingHunter != null && clashingSnitch != null)
                {
                    int hId = clashingHunter.GetComponent<InputSystemManager>().deviceID;
                    int sId = clashingSnitch.GetComponent<InputSystemManager>().deviceID;

                    AirConsole.instance.Broadcast(new { gameState = newState.ToString(), clashingIDs = new int[] { hId, sId } });
                }
                else
                {
                    AirConsole.instance.Broadcast(new { gameState = newState.ToString() });
                }
            }
            catch (System.Exception) { }
        }
    }

    private void StartMatchInit()
    {
        currentMatchTimer = matchDuration;
        playerScores.Clear(); // RESOLVED: Clears friend's new dictionary

        // Ensure everyone starts with 0
        foreach (int device in connectedDevices) playerScores[device] = 0;

        StartCoroutine(StartCountdownRoutine());
    }

    private IEnumerator StartCountdownRoutine()
    {
        UIManager.Instance.HideNotification(0);
        ChangeState(GameState.Countdown);
        UIManager.Instance.HideLobby();

        RebuildAllPlayers();

        UIManager.Instance.ShowCountdownText("3");
        yield return new WaitForSeconds(1f);
        UIManager.Instance.ShowCountdownText("2");
        yield return new WaitForSeconds(1f);
        UIManager.Instance.ShowCountdownText("1");
        yield return new WaitForSeconds(1f);
        UIManager.Instance.ShowCountdownText("GO!");

        ChangeState(GameState.ActiveMatch);
        WakeUpPlayers();

        yield return new WaitForSeconds(1f);
        UIManager.Instance.HideCountdownText();
    }

    private IEnumerator EndMatchRoutine()
    {
        ChangeState(GameState.MatchOver);

        foreach (var player in activePlayers) { if (player != null) Destroy(player); }
        activePlayers.Clear();

        int highestScore = 0;
        List<int> winners = new List<int>();

        foreach (var score in playerScores)
        {
            if (score.Value > highestScore)
            {
                highestScore = score.Value;
                winners.Clear();
                winners.Add(score.Key);
            }
            else if (score.Value == highestScore && score.Value > 0)
            {
                winners.Add(score.Key);
            }
        }

        // RESOLVED: Merging Friend's logic with User's naming convention!
        List<string> winnerNames = new List<string>();
        foreach (int id in winners)
        {
            winnerNames.Add(id < 0 ? "BOT" : (id >= 800 ? $"LOCAL P{id - 799}" : $"PHONE {id}"));
        }

        string endMessage = "";

        if (winners.Count == 0)
        {
            endMessage = "<color=yellow>DRAW: The Snitch survived!</color>";
        }
        else if (winners.Count == 1)
        {
            endMessage = $"<color=green>WINNER: {winnerNames[0]} with {highestScore} points!</color>";
        }
        else
        {
            string tiedDevices = string.Join(" & ", winnerNames);
            endMessage = $"<color=yellow>TIE: {tiedDevices} with {highestScore} points!</color>";
        }

        UIManager.Instance.ShowNotification(endMessage);
        UIManager.Instance.HideNotification(5);

        yield return new WaitForSeconds(5f);
        ChangeState(GameState.Lobby);
    }
    #endregion

    #region Player Management

    private void RebuildAllPlayers()
    {
        foreach (var player in activePlayers) { if (player != null) Destroy(player); }
        activePlayers.Clear();

        activePlayers = SpawnManager.Instance.SpawnAllPlayers(connectedDevices);

        foreach (GameObject player in activePlayers)
        {
            if (player.TryGetComponent<InputSystemManager>(out var input))
            {
                ApplyColorToPlayer(player, input.deviceID);
            }
        }
    }

    private void WakeUpPlayers()
    {
        foreach (var player in activePlayers)
        {
            if (player != null)
            {
                if (player.TryGetComponent<SnitchController>(out var snitch)) snitch.ChangeState(SnitchController.SnitchState.Fleeing);
                if (player.TryGetComponent<HunterController>(out var hunter)) hunter.ChangeState(HunterController.HunterState.Chasing);
            }
        }
    }

    private void ApplyColorToPlayer(GameObject player, int deviceID)
    {
        if (deviceColors.TryGetValue(deviceID, out Color assignedColor))
        {
            if (player.TryGetComponent<HunterController>(out var hunter)) hunter.SetBaseColor(assignedColor);
            if (player.TryGetComponent<SnitchController>(out var snitch)) snitch.SetBaseColor(assignedColor);
        }
    }

    #endregion

    #region Clash & Tag Minigame

    public void StartClash(HunterController hunter, SnitchController snitch)
    {
        if (CurrentState != GameState.ActiveMatch) return;

        clashingHunter = hunter;
        clashingSnitch = snitch;

        clashingHunterInput = clashingHunter.GetComponent<InputSystemManager>();
        clashingSnitchInput = clashingSnitch.GetComponent<InputSystemManager>();

        ChangeState(GameState.Clash);

        clashingHunter.ChangeState(HunterController.HunterState.Clashing);
        clashingSnitch.ChangeState(SnitchController.SnitchState.Clashing);

        clashTimer = clashDuration;
        hunterClashScore = 0;
        snitchClashScore = 0;

        UIManager.Instance.ShowNotification("<color=red>¡MASH BUTTONS!</color>");
    }

    public void ResolveClash(bool hunterWins)
    {
        if (CurrentState != GameState.Clash) return;

        UIManager.Instance.HideClashTimer();

        if (hunterWins)
        {
            int winningHunterID = clashingHunter.GetComponent<InputSystemManager>().deviceID;

            clashingHunter.ChangeState(HunterController.HunterState.CapturedSnitch);
            clashingSnitch.ChangeState(SnitchController.SnitchState.Captured);

            OnPlayerTagged(winningHunterID);
        }
        else
        {
            ChangeState(GameState.ActiveMatch);
            clashingSnitch.ChangeState(SnitchController.SnitchState.Fleeing);
            clashingHunter.ApplyKnockbackFrom(clashingSnitch.transform.position);

            UIManager.Instance.ShowNotification("<color=yellow>¡THE SNITCH GOT AWAY!</color>");
            UIManager.Instance.HideNotification(2);
        }

        clashingHunter = null;
        clashingSnitch = null;
    }

    public void OnPlayerTagged(int winningHunterID)
    {
        ChangeState(GameState.PostTag);

        // RESOLVED: Give the point AND display the formatted name
        if (!playerScores.ContainsKey(winningHunterID)) playerScores[winningHunterID] = 0;
        playerScores[winningHunterID]++;

        string winnerName = winningHunterID < 0 ? "BOT" : (winningHunterID >= 800 ? $"LOCAL P{winningHunterID - 799}" : $"PHONE {winningHunterID}");
        UIManager.Instance.ShowNotification($"¡{winnerName} CAUGHT THE SNITCH!");

        StartCoroutine(ResetRoundRoutine(winningHunterID));
        UIManager.Instance.UpdateScoresDisplay(GetCurrentSnitchID(), connectedDevices);
    }

    private IEnumerator ResetRoundRoutine(int winningHunterID)
    {
        yield return new WaitForSeconds(2f);

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
    public int GetPlayerScore(int deviceID) { return playerScores.ContainsKey(deviceID) ? playerScores[deviceID] : 0; }
    public int GetCurrentSnitchID() { return (CurrentState == GameState.ActiveMatch && connectedDevices.Count > 0) ? connectedDevices[0] : -1; }
    public List<int> GetConnectedDevices() { return connectedDevices; }

    #endregion

    #region Editor Debug GUI

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
            GUILayout.Label($"<color=#{ColorUtility.ToHtmlStringRGB(sColor)}>▶ DEVICE {snitchID} [SNITCH]</color> - Score: {GetPlayerScore(snitchID)}");

            for (int i = 1; i < connectedDevices.Count; i++)
            {
                int hunterID = connectedDevices[i];
                Color hColor = deviceColors.TryGetValue(hunterID, out var hc) ? hc : Color.white;
                GUILayout.Label($"<color=#{ColorUtility.ToHtmlStringRGB(hColor)}>▷ DEVICE {hunterID} [HUNTER]</color> - Score: {GetPlayerScore(hunterID)}");
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