using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PowerUpCollectable : MonoBehaviour
{
    public PowerUpSO powerUpData;

    [Tooltip("Tiempo en segundos antes de que el power-up se apague automáticamente")]
    public float lifeTime = 10f;

    [Header("Visual Effects")]
    public float rotationSpeed = 90f;
    public float floatAmplitude = 0.25f;
    public float floatFrequency = 1f;

    private Vector3 startPos;
    private Coroutine disableRoutine;

    // Usamos OnEnable en lugar de Start para que se ejecute cada vez que el Pool lo activa
    private void OnEnable()
    {
        // Actualizamos la posición inicial a donde sea que el Manager lo haya teletransportado
        startPos = transform.position;

        // Reiniciamos la corrutina de apagado automático
        if (disableRoutine != null) StopCoroutine(disableRoutine);
        disableRoutine = StartCoroutine(DisableAfterTime());
    }

    private IEnumerator DisableAfterTime()
    {
        yield return new WaitForSeconds(lifeTime);

        gameObject.SetActive(false);
    }

    private void Update()
    {
        VisualRotation();
    }

    private void VisualRotation()
    {
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.World);
        float newY = startPos.y + Mathf.Sin(Time.time * Mathf.PI * floatFrequency) * floatAmplitude;
        transform.position = new Vector3(startPos.x, newY, startPos.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        PowerUpReceiver receiver = other.GetComponent<PowerUpReceiver>();

        if (receiver != null)
        {
            UIManager.Instance.ShowNotification($"<color=blue>¡ {powerUpData.powerUpName} !</color>");
            UIManager.Instance.HideNotification(0, 1f);

            receiver.ApplyPowerUp(powerUpData);

            // Apagamos el objeto y lo dejamos disponible para el Pool
            gameObject.SetActive(false);
        }
    }
}