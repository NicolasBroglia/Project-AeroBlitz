using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using NDream.AirConsole;

public enum GameState
{
    Lobby,          // Waiting for at least 2 phones
    Countdown,      // 3, 2, 1...
    ActiveMatch,    // Gameplay is live
    PostTag         // A tag happened, updating scores, resetting
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameState CurrentState { get; private set; }

    [Header("Match Data")]
    public int scoreHunter = 0;
    public int scoreSpark = 0;

    // Track connected AirConsole device IDs
    private List<int> connectedDevices = new List<int>();

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

    // --- AIRCONSOLE CONNECTION LOGIC ---
    void OnDeviceConnect(int deviceId)
    {
        if (!connectedDevices.Contains(deviceId))
        {
            connectedDevices.Add(deviceId);
            Debug.Log($"Device {deviceId} connected. Total: {connectedDevices.Count}");

            // If we are in the lobby and have at least 2 players, start the game!
            if (CurrentState == GameState.Lobby && connectedDevices.Count >= 2)
            {
                StartCoroutine(StartCountdownRoutine());
            }
        }
    }

    void OnDeviceDisconnect(int deviceId)
    {
        connectedDevices.Remove(deviceId);
        if (connectedDevices.Count < 2 && CurrentState != GameState.Lobby)
        {
            Debug.LogWarning("Not enough players! Returning to Lobby.");
            ChangeState(GameState.Lobby);
        }
    }

    // --- STATE MACHINE LOGIC ---
    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        Debug.Log($"Game State changed to: {newState}");

        switch (newState)
        {
            case GameState.Lobby:
                // Show Lobby UI, hide players
                break;
            case GameState.ActiveMatch:
                // Enable player movement
                break;
        }
    }

    private IEnumerator StartCountdownRoutine()
    {
        ChangeState(GameState.Countdown);
        Debug.Log("3...");
        yield return new WaitForSeconds(1f);
        Debug.Log("2...");
        yield return new WaitForSeconds(1f);
        Debug.Log("1...");
        yield return new WaitForSeconds(1f);
        Debug.Log("GO!");
        ChangeState(GameState.ActiveMatch);
    }

    // --- GAMEPLAY HOOKS (Jere will call these) ---
    public void OnPlayerTagged()
    {
        if (CurrentState != GameState.ActiveMatch) return;

        ChangeState(GameState.PostTag);
        scoreHunter++; // Or whoever won that round
        Debug.Log("TAG! Hunter Score: " + scoreHunter);

        StartCoroutine(ResetRoundRoutine());
    }

    private IEnumerator ResetRoundRoutine()
    {
        yield return new WaitForSeconds(2f); // Pause to show explosion/juice
        // TODO: Swap roles between the two device IDs
        // TODO: Reset player positions
        StartCoroutine(StartCountdownRoutine());
    }
}