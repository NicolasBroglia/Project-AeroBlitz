using UnityEngine;
using NDream.AirConsole;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine.InputSystem;

// ==========================================
// --- DATA CONTAINER ---
// ==========================================
public class PlayerInputData
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

    [Header("Offline Testing")]
    [SerializeField] private bool offlineKeyboard = false;

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

        if (!deviceInputs.ContainsKey(fromDeviceID))
        {
            deviceInputs[fromDeviceID] = new PlayerInputData();
        }

        try
        {
            // Convert.ToSingle is the safest way to pull raw numbers out of Newtonsoft JSON
            if (data["x"] != null && data["x"].Type != JTokenType.Null)
                deviceInputs[fromDeviceID].MoveX = System.Convert.ToSingle(data["x"]);

            if (data["y"] != null && data["y"].Type != JTokenType.Null)
                deviceInputs[fromDeviceID].MoveY = System.Convert.ToSingle(data["y"]);

            if (data["z"] != null && data["z"].Type != JTokenType.Null)
                deviceInputs[fromDeviceID].MoveZ = System.Convert.ToSingle(data["z"]);

            if (data["dash"] != null && data["dash"].Type != JTokenType.Null)
                deviceInputs[fromDeviceID].IsDashing = (bool)data["dash"];

            if (data["mash"] != null && data["mash"].Type != JTokenType.Null)
                deviceInputs[fromDeviceID].IsMashing = (bool)data["mash"];
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[JSON ERROR] Device {fromDeviceID} failed parsing: {e.Message}");
        }
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
        if (offlineKeyboard && Keyboard.current != null)
        {
            var kb = Keyboard.current;

            if (deviceID == 0) // Snitch (WASD + Q/E)
            {
                float x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                float z = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
                float y = (kb.gKey.isPressed ? 1 : 0) - (kb.hKey.isPressed ? 1 : 0);

                // Only override if the keyboard is actually being pressed
                if (x != 0) mergedData.MoveX = x;
                if (z != 0) mergedData.MoveZ = z;
                if (y != 0) mergedData.MoveY = y;
            }
            else if (deviceID == 1) // Hunter (Arrows + Ctrl/Shift)
            {
                float x = (kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0);
                float z = (kb.upArrowKey.isPressed ? 1 : 0) - (kb.downArrowKey.isPressed ? 1 : 0);
                float y = (kb.numpad5Key.isPressed ? 1 : 0) - (kb.numpad6Key.isPressed ? 1 : 0);

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

        // 1. Check Keyboard (if enabled)
        if (offlineKeyboard && Keyboard.current != null)
        {
            if (deviceID == 0 && Keyboard.current.spaceKey.wasPressedThisFrame) triggered = true;
            if (deviceID == 1 && Keyboard.current.numpad0Key.wasPressedThisFrame) triggered = true;
        }

        // 2. Check AirConsole (Consume it instantly so it doesn't double-fire)
        if (deviceInputs.ContainsKey(deviceID) && deviceInputs[deviceID].IsDashing)
        {
            triggered = true;
            deviceInputs[deviceID].IsDashing = false;
        }

        return triggered;
    }

    public bool ConsumeMash(int deviceID)
    {
        bool triggered = false;

        // 1. Check Keyboard (if enabled)
        if (offlineKeyboard && Keyboard.current != null)
        {
            if (deviceID == 0 && Keyboard.current.spaceKey.wasPressedThisFrame) triggered = true;
            if (deviceID == 1 && Keyboard.current.enterKey.wasPressedThisFrame) triggered = true;
        }

        // 2. Check AirConsole (Failsafe: Dash or Mash counts)
        if (deviceInputs.ContainsKey(deviceID))
        {
            if (deviceInputs[deviceID].IsMashing || deviceInputs[deviceID].IsDashing)
            {
                triggered = true;
                deviceInputs[deviceID].IsMashing = false;
                deviceInputs[deviceID].IsDashing = false;
            }
        }

        return triggered;
    }

    #endregion
}