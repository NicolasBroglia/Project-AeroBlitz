using UnityEngine;
using NDream.AirConsole;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine.InputSystem;

// ==========================================
// --- DATA CONTAINER ---
// ==========================================
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

    private Dictionary<int, PlayerInputData> deviceInputs = new Dictionary<int, PlayerInputData>();

    void Awake() { if (Instance == null) Instance = this; }

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

    void OnMessage(int fromDeviceID, JToken data)
    {
        if (data == null || data.Type == JTokenType.Null) return;

        PlayerInputData inputData = deviceInputs.ContainsKey(fromDeviceID)
            ? deviceInputs[fromDeviceID]
            : new PlayerInputData();

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

        deviceInputs[fromDeviceID] = inputData;
    }

    void OnDisconnect(int deviceID)
    {
        if (deviceInputs.ContainsKey(deviceID)) deviceInputs.Remove(deviceID);
    }

    public PlayerInputData GetInputForDevice(int deviceID)
    {
        PlayerInputData mergedData = new PlayerInputData();

        if (deviceInputs.ContainsKey(deviceID))
        {
            mergedData.MoveX = deviceInputs[deviceID].MoveX;
            mergedData.MoveY = deviceInputs[deviceID].MoveY;
            mergedData.MoveZ = deviceInputs[deviceID].MoveZ;
            mergedData.IsDashing = deviceInputs[deviceID].IsDashing;
            mergedData.IsMashing = deviceInputs[deviceID].IsMashing;
        }

        bool isPCMode = GameManager.Instance != null && GameManager.Instance.CurrentPlayMode == GameManager.PlayMode.PCLocal;

        if (isPCMode && Keyboard.current != null)
        {
            var kb = Keyboard.current;

            if (deviceID == 800) // RESOLVED: User ID 800 with Friend's G/H keys
            {
                float x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                float z = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
                float y = (kb.gKey.isPressed ? 1 : 0) - (kb.hKey.isPressed ? 1 : 0);

                if (x != 0) mergedData.MoveX = x;
                if (z != 0) mergedData.MoveZ = z;
                if (y != 0) mergedData.MoveY = y;
            }
            else if (deviceID == 801) // RESOLVED: User ID 801 with Friend's Numpad keys
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

    public bool ConsumeDash(int deviceID)
    {
        bool triggered = false;
        bool isPCMode = GameManager.Instance != null && GameManager.Instance.CurrentPlayMode == GameManager.PlayMode.PCLocal;

        if (isPCMode && Keyboard.current != null)
        {
            // RESOLVED: Kept IDs 800/801, applied Numpad0 for player 2
            if (deviceID == 800 && Keyboard.current.spaceKey.wasPressedThisFrame) triggered = true;
            if (deviceID == 801 && Keyboard.current.numpad0Key.wasPressedThisFrame) triggered = true;
        }

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
            // RESOLVED: Kept IDs 800/801, applied Numpad0 for player 2
            if (deviceID == 800 && Keyboard.current.spaceKey.wasPressedThisFrame) triggered = true;
            if (deviceID == 801 && Keyboard.current.numpad0Key.wasPressedThisFrame) triggered = true;
        }

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
}