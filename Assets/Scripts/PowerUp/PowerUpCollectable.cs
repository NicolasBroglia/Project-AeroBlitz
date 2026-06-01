using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PowerUpCollectable : MonoBehaviour
{
    public PowerUpSO powerUpData;

    [Tooltip("Tiempo en segundos antes de que el power-up se destruya automáticamente")]
    public float lifeTime = 10f;

    [Header("Visual Effects")]
    [Tooltip("Velocidad de giro en grados por segundo")]
    public float rotationSpeed = 90f;
    [Tooltip("Distancia máxima que sube y baja desde su centro")]
    public float floatAmplitude = 0.25f;
    [Tooltip("Qué tan rápido hace el ciclo de subir y bajar")]
    public float floatFrequency = 1f;

    // Guardamos la posición donde apareció para que el flote sea relativo a ese punto
    private Vector3 startPos;

    private void Start()
    {
        startPos = transform.position;

        // Autodestrucción a los 10 segundos
        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        VisualRotation();
    }

    private void VisualRotation()
    {
        // 1. Rotación continua
        // Usamos Space.World para que gire siempre sobre el eje vertical del mapa
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.World);

        // 2. Flote suave (Bobbing)
        // Usamos la función Seno basada en el tiempo para obtener un valor suave entre -1 y 1
        float newY = startPos.y + Mathf.Sin(Time.time * Mathf.PI * floatFrequency) * floatAmplitude;

        // Aplicamos la nueva posición manteniendo la X y la Z intactas
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
            Destroy(gameObject);
        }
    }

}