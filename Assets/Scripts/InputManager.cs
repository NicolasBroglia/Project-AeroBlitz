using UnityEngine;
using NDream.AirConsole;
using Newtonsoft.Json.Linq;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    // Public getters for Programmer 2's character scripts
    public float MoveX { get; private set; }
    public float MoveY { get; private set; }
    public float MoveZ { get; private set; }
    public bool IsDashing { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void OnEnable()
    {
        AirConsole.instance.onMessage += OnMessage;
    }

    void OnDisable()
    {
        if (AirConsole.instance != null)
        {
            AirConsole.instance.onMessage -= OnMessage;
        }
    }

    // Called automatically whenever a phone sends a message
    void OnMessage(int fromDeviceID, JToken data)
    {
        // Parse the JSON data sent from the controller.html
        if (data["x"] != null) MoveX = (float)data["x"];
        if (data["z"] != null) MoveZ = (float)data["z"];
        if (data["y"] != null) MoveY = (float)data["y"];

        if (data["dash"] != null && (bool)data["dash"])
        {
            IsDashing = true;
        }
    }

    void Update()
    {
        // Reset the dash flag at the end of the frame 
        // to mimic Unity's standard Input.GetButtonDown behavior
        if (IsDashing)
        {
            IsDashing = false;
        }
    }
}