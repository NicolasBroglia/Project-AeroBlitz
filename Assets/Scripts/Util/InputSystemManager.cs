using UnityEngine;

public class InputSystemManager : MonoBehaviour
{
    #region Settings

    [Header("AirConsole Link")]
    public int deviceID = -1;

    #endregion

    #region Continuous Input (Axes)

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

    #endregion

    #region Action Triggers (Buttons)

    public bool GetDash()
    {
        if (InputManager.Instance != null && deviceID != -1)
        {
            return InputManager.Instance.ConsumeDash(deviceID);
        }
        return false;
    }

    public bool GetMash()
    {
        if (InputManager.Instance != null && deviceID != -1)
        {
            return InputManager.Instance.ConsumeMash(deviceID);
        }
        return false;
    }

    #endregion
}