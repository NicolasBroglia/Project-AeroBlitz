using UnityEngine;

public enum PowerUpType
{
    SpeedBoost,
    FreezeHunters,
    ZeroDrag,
    Juggernaut,
    DecoyClone,
    InvertControls
}

[CreateAssetMenu(fileName = "NewPowerUp", menuName = "PowerUps/PowerUp Data")]
public class PowerUpSO : ScriptableObject
{
    public string powerUpName;
    public PowerUpType type;
    public float duration = 3f;

    [Tooltip("Solo se usa si el tipo es SpeedBoost")]
    public float speedMultiplier = 1.5f;
}