using UnityEngine;
using NDream.AirConsole;
using Newtonsoft.Json.Linq;

// This script sits on an empty GameObject in your scene
public class NetworkCommandRouter : MonoBehaviour
{
    void OnEnable()
    {
        AirConsole.instance.onMessage += OnNetworkMessage;
    }

    void OnDisable()
    {
        if (AirConsole.instance != null)
        {
            AirConsole.instance.onMessage -= OnNetworkMessage;
        }
    }

    private void OnNetworkMessage(int fromDeviceID, JToken data)
    {
        // Ignore physics inputs (InputManager handles x, y, z, dash, mash)
        if (data["action"] == null) return;

        string actionType = (string)data["action"];

        switch (actionType)
        {
            case "startGame":
                HandleStartGameRequest(fromDeviceID);
                break;

                // Future-proofing: Easy to add new UI commands later
                // case "pauseGame":
                // case "selectCharacter":
                // case "returnToLobby":
        }
    }

    private void HandleStartGameRequest(int requestingDeviceID)
    {
        // Optional logic: You could check if requestingDeviceID is the "Host" (Device 0) 
        // before allowing the game to start.

        if (GameManager.Instance.CurrentState == GameState.Lobby)
        {
            Debug.Log($"Device {requestingDeviceID} requested match start.");
            GameManager.Instance.RequestStartMatch();
        }
    }
}