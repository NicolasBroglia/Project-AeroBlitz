using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SnitchController : MonoBehaviour
{
    // Definimos los estados posibles
    public enum SnitchState
    {
        Idle,       // Esperando el inicio (countdown)
        Fleeing,    // Estado activo de juego
        Captured    // Cuando un cazador la atrapa
    }

    [Header("State")]
    public SnitchState currentState = SnitchState.Idle;

    [Header("References")]
    [SerializeField] private InputSystemManager inputManager;
    private Rigidbody rb;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 40f;
    [SerializeField] private float maxSpeed = 15f;
    [SerializeField] private float maxHeight = 4f;
    [SerializeField] private float minHeight = 0.5f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Optimizaciones clave para una bola con movimiento fluido:
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.useGravity = false;

        inputManager = GetComponent<InputSystemManager>();
    }

    private void Update()
    {
        ManageStateMachine();
    }

    private void FixedUpdate()
    {
        // Las f�sicas solo se aplican si est� en estado de huida
        if (currentState == SnitchState.Fleeing)
        {
            HandleMovement();
        }
    }

    private void ManageStateMachine()
    {
        switch (currentState)
        {
            case SnitchState.Idle:
                // Para el prototipo: Si detecta input, pasa autom�ticamente a Fleeing.
                // M�s adelante, esto lo controlar� tu Game Manager despu�s del 3, 2, 1...
                if (inputManager.GetMovement().sqrMagnitude > 0.01f)
                {
                    ChangeState(SnitchState.Fleeing);
                }
                break;

            case SnitchState.Fleeing:
                // La l�gica de movimiento ocurre en FixedUpdate
                break;

            case SnitchState.Captured:
                // Frenamos la bola en seco para el impacto final
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                break;
        }
    }

    private void HandleMovement()
    {
        Vector2 planeInput = inputManager.GetMovement();
        float elevationInput = inputManager.GetElevation();

        // Construimos el vector 3D: 
        Vector3 moveDirection = new Vector3(planeInput.x, elevationInput, planeInput.y).normalized;

        rb.AddForce(moveDirection * moveSpeed, ForceMode.Acceleration);

        // Clampeamos la velocidad máxima global (en las 3 dimensiones)
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

        //Clamp de la altura
        Vector3 clampedPosition = transform.position;
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, minHeight, maxHeight);

        if (transform.position.y != clampedPosition.y)
        {
            rb.position = clampedPosition;

            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
    }

    public void ChangeState(SnitchState newState)
    {
        currentState = newState;

        // Ac� a futuro pod�s hookear eventos: 
        // Ej: si newState == Captured -> disparar part�culas o sonido.
    }
}