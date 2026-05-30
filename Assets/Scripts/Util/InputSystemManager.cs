using UnityEngine;

public class InputSystemManager : MonoBehaviour
{
    [Header("AirConsole Link")]
    public int deviceID = -1;

    public Vector2 GetMovement()
    {
        if (InputManager.Instance != null && deviceID != -1)
        {
            var data = InputManager.Instance.GetInputForDevice(deviceID);
            if (data != null) return new Vector2(data.MoveX, data.MoveZ);
        }
        return Vector2.zero;
    }

    public float GetElevation()
    {
        if (InputManager.Instance != null && deviceID != -1)
        {
            var data = InputManager.Instance.GetInputForDevice(deviceID);
            if (data != null) return data.MoveY;
        }
        return 0f;
    }

    public bool GetDash()
    {
        if (InputManager.Instance != null && deviceID != -1)
        {
            var data = InputManager.Instance.GetInputForDevice(deviceID);
            if (data != null) return data.IsDashing;
        }
        return false;
    }
}