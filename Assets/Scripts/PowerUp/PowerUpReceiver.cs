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

    [Header("Decoy Settings")]
    public GameObject decoyPrefab;

    // Para evitar que los efectos se pisen si agarra dos iguales seguidos
    private Coroutine activeSpeedRoutine;
    private Coroutine activeDragRoutine;
    private Coroutine activeFreezeRoutine;
    private Coroutine activeJuggernautRoutine;

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

            case PowerUpType.Juggernaut:
                if (activeJuggernautRoutine != null) StopCoroutine(activeJuggernautRoutine);
                activeJuggernautRoutine = StartCoroutine(JuggernautRoutine(data.duration));
                break;

            case PowerUpType.DecoyClone:
                SpawnDecoy();
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
    private IEnumerator JuggernautRoutine(float duration)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) yield break;

        // Guardamos la masa original (por si tenés distintos pesos para cazadores y snitch)
        float originalMass = rb.mass;

        // Lo convertimos en una pared de ladrillos (multiplicador x10)
        rb.mass = originalMass * 10f;

        // Opcional: Podés cambiarle el color al jugador o prenderle un aura acá

        yield return new WaitForSeconds(duration);

        // Volvemos a la normalidad
        rb.mass = originalMass;
    }

    private void SpawnDecoy()
    {
        if (decoyPrefab == null) return;

        Rigidbody myRb = GetComponent<Rigidbody>();

        // 1. Instanciamos el clon exactamente en nuestra posición y rotación
        GameObject clone = Instantiate(decoyPrefab, transform.position, transform.rotation);

        // 2. FÍSICAS DEL CLON
        Rigidbody cloneRb = clone.GetComponent<Rigidbody>();
        if (cloneRb != null && myRb != null)
        {
            // Le sacamos la fricción/freno para que se deslice sin parar
            cloneRb.linearDamping = 0f;
            cloneRb.angularDamping = 0f;

            // Definimos la velocidad a la que queremos que corra el clon
            float decoySpeed = snitchController.GetSpeed() * 0.25f;

            // Si la Snitch se está moviendo con físicas reales, copiamos su dirección
            if (myRb.linearVelocity.magnitude > 0.1f)
            {
                cloneRb.linearVelocity = myRb.linearVelocity.normalized * decoySpeed;
            }
            else
            {
                // Si la velocidad física da 0 (por usar MovePosition), lo disparamos 
                // en la dirección hacia la que está mirando el modelo 3D
                cloneRb.linearVelocity = transform.forward * decoySpeed;
            }
        }
        
        MeshRenderer myRenderer = snitchController.mRenderer;
        MeshRenderer[] cloneRenderers = clone.GetComponentsInChildren<MeshRenderer>();
        if (myRenderer != null && cloneRenderers != null && cloneRenderers.Length > 0)
        {
            // Le pasamos el color exacto que el GameManager le pintó a este jugador
            foreach (var renderer in cloneRenderers)
            {
                renderer.material.color = myRenderer.material.color;
            }
        }

        // 3. Autodestrucción simple
        Destroy(clone, 7f);
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