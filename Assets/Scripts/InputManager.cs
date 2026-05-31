using UnityEngine;
using NDream.AirConsole;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine.InputSystem; // <-- Required for the New Input System

// We create a clean container for each phone's data
public class PlayerInputData
{
    public float MoveX;
    public float MoveY;
    public float MoveZ;
    public bool IsDashing;
}

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [Header("Offline Testing")]
    [SerializeField] private bool offlineKeyboard = false;

    // The Dictionary mapping Phone ID -> Input Data
    private Dictionary<int, PlayerInputData> deviceInputs = new Dictionary<int, PlayerInputData>();

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

    // Called automatically whenever ANY phone sends a message
    void OnMessage(int fromDeviceID, JToken data)
    {
        // If this is a new phone, create a data container for it
        if (!deviceInputs.ContainsKey(fromDeviceID))
        {
            deviceInputs[fromDeviceID] = new PlayerInputData();
        }

        // Parse and store the data FOR THIS SPECIFIC PHONE
        if (data["x"] != null) deviceInputs[fromDeviceID].MoveX = (float)data["x"];
        if (data["y"] != null) deviceInputs[fromDeviceID].MoveY = (float)data["y"];
        if (data["z"] != null) deviceInputs[fromDeviceID].MoveZ = (float)data["z"];

        if (data["dash"] != null && (bool)data["dash"])
        {
            deviceInputs[fromDeviceID].IsDashing = true;
        }
    }

    void OnDisconnect(int deviceID)
    {
        if (deviceInputs.ContainsKey(deviceID))
        {
            deviceInputs.Remove(deviceID);
        }
    }


    public PlayerInputData GetInputForDevice(int deviceID)
    {
        // --- OFFLINE INJECTION (NEW INPUT SYSTEM) ---
        if (offlineKeyboard)
        {
            var kb = Keyboard.current;

            // Safety check in case no keyboard is detected by Unity
            if (kb != null)
            {
                // Player 1 (Snitch) -> STRICTLY WASD, Q/E for Y-axis, Spacebar for Dash
                if (deviceID == 0)
                {
                    float wasdX = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                    float wasdZ = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
                    float wasdY = (kb.eKey.isPressed ? 1 : 0) - (kb.qKey.isPressed ? 1 : 0);

                    return new PlayerInputData
                    {
                        MoveX = wasdX,
                        MoveZ = wasdZ,
                        MoveY = wasdY,
                        IsDashing = kb.spaceKey.wasPressedThisFrame
                    };
                }

                // Player 2 (Hunter) -> Arrow Keys, RightShift/Ctrl for Y-axis, Enter for Dash
                if (deviceID == 1)
                {
                    float arrowX = (kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0);
                    float arrowZ = (kb.upArrowKey.isPressed ? 1 : 0) - (kb.downArrowKey.isPressed ? 1 : 0);
                    float arrowY = (kb.rightShiftKey.isPressed ? 1 : 0) - (kb.rightCtrlKey.isPressed ? 1 : 0);

                    return new PlayerInputData
                    {
                        MoveX = arrowX,
                        MoveZ = arrowZ,
                        MoveY = arrowY,
                        IsDashing = kb.enterKey.wasPressedThisFrame
                    };
                }
            }
        }

        // --- 2. NORMAL AIRCONSOLE BEHAVIOR ---
        if (deviceInputs.ContainsKey(deviceID))
        {
            return deviceInputs[deviceID];
        }
        return null;
    }

    // Add this new method:
    public bool ConsumeDash(int deviceID)
    {
        // 1. Offline Keyboard Check
        if (offlineKeyboard)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (deviceID == 0) return kb.spaceKey.wasPressedThisFrame;
                if (deviceID == 1) return kb.enterKey.wasPressedThisFrame;
            }
        }

        // 2. AirConsole Network Check
        if (deviceInputs.ContainsKey(deviceID))
        {
            bool dashTriggered = deviceInputs[deviceID].IsDashing;

            // "Consume" the input immediately so it doesn't fire continuously
            deviceInputs[deviceID].IsDashing = false;

            return dashTriggered;
        }
        return false;
    }
}