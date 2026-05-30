using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [Header("Input Actions")]
    [Tooltip("Referencia a la acción de movimiento (Action Type: Value, Control Type: Vector2)")]
    [SerializeField] private InputActionReference moveAction;

    [Tooltip("Referencia a la acción de elevación (Action Type: Value, Control Type: Axis)")]
    [SerializeField] private InputActionReference elevationAction;

    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (elevationAction != null) elevationAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (elevationAction != null) elevationAction.action.Disable();
    }

    /// <summary>
    /// Devuelve el vector de movimiento normalizado en el plano XZ.
    /// </summary>
    public Vector2 GetMovement()
    {
        if (moveAction != null)
        {
            return moveAction.action.ReadValue<Vector2>();
        }
        return Vector2.zero;
    }

    /// <summary>
    /// Devuelve el valor de elevación (-1 para bajar, 1 para subir).
    /// </summary>
    public float GetElevation()
    {
        if (elevationAction != null)
        {
            return elevationAction.action.ReadValue<float>();
        }
        return 0f;
    }
}