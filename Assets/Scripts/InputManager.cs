using UnityEngine;
using NDream.AirConsole;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine.InputSystem;

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

    void OnMessage(int fromDeviceID, JToken data)
    {
        if (!deviceInputs.ContainsKey(fromDeviceID))
        {
            deviceInputs[fromDeviceID] = new PlayerInputData();
        }

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
        if (offlineKeyboard)
        {
            var kb = Keyboard.current;

            if (kb != null)
            {
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

        if (deviceInputs.ContainsKey(deviceID))
        {
            return deviceInputs[deviceID];
        }
        return null;
    }

    void Update()
    {
        foreach (var input in deviceInputs.Values)
        {
            if (input.IsDashing) input.IsDashing = false;
        }
    }
}