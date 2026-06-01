using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PowerUpReceiver : MonoBehaviour
{
    private Rigidbody rb;
    private HunterController hunterController;
    private SnitchController snitchController;

    // Guardamos las velocidades base para poder restaurarlas
    private float baseMoveSpeed;
    private float baseMaxSpeed;
    private float baseDrag;

    // Para evitar que los efectos se pisen si agarra dos iguales seguidos
    private Coroutine activeSpeedRoutine;
    private Coroutine activeDragRoutine;
    private Coroutine activeFreezeRoutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        hunterController = GetComponent<HunterController>();
        snitchController = GetComponent<SnitchController>();

        baseDrag = rb.linearDamping;
    }

    private void Start()
    {
        // Guardamos las velocidades iniciales dependiendo de qui�n sea este jugador
        if (hunterController != null)
        {
            baseMoveSpeed = hunterController.GetSpeed();
            baseMaxSpeed = hunterController.GetMaxSpeed();
        }
        else if (snitchController != null)
        {
            baseMoveSpeed = snitchController.GetSpeed();
            baseMaxSpeed = snitchController.GetMaxSpeed();
        }
    }

    public void ApplyPowerUp(PowerUpSO data)
    {
        switch (data.type)
        {
            case PowerUpType.SpeedBoost:
                if (activeSpeedRoutine != null) StopCoroutine(activeSpeedRoutine);
                activeSpeedRoutine = StartCoroutine(SpeedBoostRoutine(data.duration, data.speedMultiplier));
                break;

            case PowerUpType.ZeroDrag:
                if (activeDragRoutine != null) StopCoroutine(activeDragRoutine);
                activeDragRoutine = StartCoroutine(ZeroDragRoutine(data.duration));
                break;

            case PowerUpType.FreezeHunters:
                // El freeze no se lo aplica a s� mismo, se lo manda a los dem�s
                ExecuteFreezeHunters(data.duration);
                break;
        }
    }

    // --- L�GICA DE LOS EFECTOS ---

    private IEnumerator SpeedBoostRoutine(float duration, float multiplier)
    {
        SetSpeeds(baseMoveSpeed * multiplier, baseMaxSpeed * multiplier);

        yield return new WaitForSeconds(duration);

        SetSpeeds(baseMoveSpeed, baseMaxSpeed); // Restauramos
    }

    private IEnumerator ZeroDragRoutine(float duration)
    {
        rb.linearDamping = 0f; // Patina como sobre hielo

        yield return new WaitForSeconds(duration);

        rb.linearDamping = baseDrag; // Restauramos la fricci�n original
    }

    private void ExecuteFreezeHunters(float duration)
    {
        // Buscamos a todos los cazadores activos en la escena
        HunterController[] allHunters = FindObjectsByType<HunterController>();

        foreach (HunterController hunter in allHunters)
        {
            // Solo congelamos si no somos nosotros mismos (por si el power up lo agarra un cazador y quer�s que congele al rival)
            if (hunter.gameObject != this.gameObject)
            {
                PowerUpReceiver enemyReceiver = hunter.GetComponent<PowerUpReceiver>();
                if (enemyReceiver != null)
                {
                    enemyReceiver.ReceiveExternalFreeze(duration);
                }
            }
        }
    }

    // M�todo que es llamado por otro jugador cuando activa el "Freeze Hunters"
    public void ReceiveExternalFreeze(float duration)
    {
        if (activeFreezeRoutine != null) StopCoroutine(activeFreezeRoutine);
        activeFreezeRoutine = StartCoroutine(FreezeRoutine(duration));
    }

    private IEnumerator FreezeRoutine(float duration)
    {
        if (hunterController != null)
        {
            hunterController.ChangeState(HunterController.HunterState.Stunned);

            yield return new WaitForSeconds(duration);

            // Volvemos a la persecuci�n
            hunterController.ChangeState(HunterController.HunterState.Chasing);
        }
    }

    // --- HELPER PARA MODIFICAR VELOCIDADES ---
    private void SetSpeeds(float newMoveSpeed, float newMaxSpeed)
    {
        if (hunterController != null)
        {
            hunterController.SetSpeed(newMoveSpeed);
            hunterController.SetMaxSpeed(newMaxSpeed);
        }
        else if (snitchController != null)
        {
            snitchController.SetSpeed(newMoveSpeed);
            snitchController.SetMaxSpeed(newMaxSpeed);
        }
    }
}