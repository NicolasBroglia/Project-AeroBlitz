using UnityEngine;
using NDream.AirConsole;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine.InputSystem;

// ==========================================
// --- DATA CONTAINER ---
// ==========================================
// OPTIMIZED: Struct to prevent GC allocations
public struct PlayerInputData
{
    public float MoveX;
    public float MoveY;
    public float MoveZ;
    public bool IsDashing;
    public bool IsMashing;
}

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    #region Variables & Setup

    // The Dictionary mapping Phone ID -> Input Data
    private Dictionary<int, PlayerInputData> deviceInputs = new Dictionary<int, PlayerInputData>();

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void OnEnable()
    {
        AirConsole.instance.onMessage += OnMessage;
        AirConsole.instance.onDisconnect += OnDisconnect;
    }

    void OnDisable()
    {
        if (AirConsole.instance != null)
        {
            AirConsole.instance.onMessage -= OnMessage;
            AirConsole.instance.onDisconnect -= OnDisconnect;
        }
    }

    #endregion

    #region AirConsole Network Callbacks

    // Called automatically whenever ANY phone sends a message
    void OnMessage(int fromDeviceID, JToken data)
    {
        if (data == null || data.Type == JTokenType.Null) return;

        // Extract existing struct or create a new one
        PlayerInputData inputData = deviceInputs.ContainsKey(fromDeviceID)
            ? deviceInputs[fromDeviceID]
            : new PlayerInputData();

        // OPTIMIZED PARSING: Direct nullable casting. 
        float? xVal = (float?)data["x"];
        if (xVal.HasValue) inputData.MoveX = xVal.Value;

        float? yVal = (float?)data["y"];
        if (yVal.HasValue) inputData.MoveY = yVal.Value;

        float? zVal = (float?)data["z"];
        if (zVal.HasValue) inputData.MoveZ = zVal.Value;

        bool? dashVal = (bool?)data["dash"];
        if (dashVal.HasValue) inputData.IsDashing = dashVal.Value;

        bool? mashVal = (bool?)data["mash"];
        if (mashVal.HasValue) inputData.IsMashing = mashVal.Value;

        // Save the modified struct back into the dictionary
        deviceInputs[fromDeviceID] = inputData;
    }

    void OnDisconnect(int deviceID)
    {
        if (deviceInputs.ContainsKey(deviceID))
        {
            deviceInputs.Remove(deviceID);
        }
    }

    #endregion

    #region Input Polling (Continuous Axes)

    public PlayerInputData GetInputForDevice(int deviceID)
    {
        PlayerInputData mergedData = new PlayerInputData();

        // 1. Pull AirConsole Network Data First
        if (deviceInputs.ContainsKey(deviceID))
        {
            mergedData.MoveX = deviceInputs[deviceID].MoveX;
            mergedData.MoveY = deviceInputs[deviceID].MoveY;
            mergedData.MoveZ = deviceInputs[deviceID].MoveZ;
            mergedData.IsDashing = deviceInputs[deviceID].IsDashing;
            mergedData.IsMashing = deviceInputs[deviceID].IsMashing;
        }

        // 2. Layer Offline Keyboard Data on top (if enabled)
        bool isPCMode = GameManager.Instance != null && GameManager.Instance.CurrentPlayMode == GameManager.PlayMode.PCLocal;

        if (isPCMode && Keyboard.current != null)
        {
            var kb = Keyboard.current;

            // FIX: Listening for the correct Local IDs (800 and 801)
            if (deviceID == 800) // Snitch (WASD + Q/E)
            {
                float x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                float z = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
                float y = (kb.eKey.isPressed ? 1 : 0) - (kb.qKey.isPressed ? 1 : 0);

                if (x != 0) mergedData.MoveX = x;
                if (z != 0) mergedData.MoveZ = z;
                if (y != 0) mergedData.MoveY = y;
            }
            else if (deviceID == 801) // Hunter (Arrows + Ctrl/Shift)
            {
                float x = (kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0);
                float z = (kb.upArrowKey.isPressed ? 1 : 0) - (kb.downArrowKey.isPressed ? 1 : 0);
                float y = (kb.rightShiftKey.isPressed ? 1 : 0) - (kb.rightCtrlKey.isPressed ? 1 : 0);

                if (x != 0) mergedData.MoveX = x;
                if (z != 0) mergedData.MoveZ = z;
                if (y != 0) mergedData.MoveY = y;
            }
        }

        return mergedData;
    }

    #endregion

    #region Input Consumption (Action Triggers)

    public bool ConsumeDash(int deviceID)
    {
        bool triggered = false;

        bool isPCMode = GameManager.Instance != null && GameManager.Instance.CurrentPlayMode == GameManager.PlayMode.PCLocal;

        if (isPCMode && Keyboard.current != null)
        {
            // FIX: Checking for IDs 800 and 801
            if (deviceID == 800 && Keyboard.current.spaceKey.wasPressedThisFrame) triggered = true;
            if (deviceID == 801 && Keyboard.current.enterKey.wasPressedThisFrame) triggered = true;
        }

        // 2. Check AirConsole
        if (deviceInputs.ContainsKey(deviceID) && deviceInputs[deviceID].IsDashing)
        {
            triggered = true;
            PlayerInputData data = deviceInputs[deviceID];
            data.IsDashing = false;
            deviceInputs[deviceID] = data;
        }

        return triggered;
    }

    public bool ConsumeMash(int deviceID)
    {
        bool triggered = false;

        bool isPCMode = GameManager.Instance != null && GameManager.Instance.CurrentPlayMode == GameManager.PlayMode.PCLocal;

        if (isPCMode && Keyboard.current != null)
        {
            // FIX: Checking for IDs 800 and 801
            if (deviceID == 800 && Keyboard.current.spaceKey.wasPressedThisFrame) triggered = true;
            if (deviceID == 801 && Keyboard.current.enterKey.wasPressedThisFrame) triggered = true;
        }

        // 2. Check AirConsole (Failsafe: Dash or Mash counts)
        if (deviceInputs.ContainsKey(deviceID))
        {
            if (deviceInputs[deviceID].IsMashing || deviceInputs[deviceID].IsDashing)
            {
                triggered = true;
                PlayerInputData data = deviceInputs[deviceID];
                data.IsMashing = false;
                data.IsDashing = false;
                deviceInputs[deviceID] = data;
            }
        }

        return triggered;
    }

    #endregion
}