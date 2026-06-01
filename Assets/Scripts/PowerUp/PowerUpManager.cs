using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerUpManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Tiempo en segundos entre cada intento de aparición")]
    [SerializeField] private float spawnInterval = 5f;
    [Tooltip("Cantidad máxima de power-ups que pueden coexistir en la arena")]
    [SerializeField] private int maxActivePowerUps = 3;

    [Header("References")]
    [Tooltip("El prefab genérico que tiene el script PowerUpCollectable")]
    [SerializeField] private GameObject powerUpPrefab;
    [Tooltip("Los lugares físicos en la arena donde pueden aparecer")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("La lista de los Scriptable Objects que creaste")]
    [SerializeField] private PowerUpSO[] availablePowerUps;

    private List<GameObject> activePowerUps = new List<GameObject>();

    private void OnEnable()
    {
        GameManager.OnGameStateChanged += PowerUpInit;
    }
    private void OnDisable()
    {
        GameManager.OnGameStateChanged -= PowerUpInit;
    }
    private void Start()
    {

    }

    private void PowerUpInit(GameState gameState)
    {
        if (gameState != GameState.ActiveMatch) return;

        if (spawnPoints.Length == 0 || availablePowerUps.Length == 0 || powerUpPrefab == null)
        {
            Debug.LogWarning("PowerUpManager: Missing references.");
        }

        StartCoroutine(SpawnRoutine());
        Debug.Log("PowerUpManager intialized");
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            // 1. Limpiamos la lista quitando los objetos que ya fueron agarrados (destruidos)
            activePowerUps.RemoveAll(item => item == null);

            // 2. Si todavía hay lugar en el mapa, intentamos crear uno nuevo
            if (activePowerUps.Count < maxActivePowerUps)
            {
                SpawnPowerUp();
                Debug.Log("PowerUp spawned");
            }
        }
    }

    private void SpawnPowerUp()
    {
        // Elegimos un punto de aparición que no esté ocupado
        Transform randomSpawnPoint = GetFreeSpawnPoint();

        if (randomSpawnPoint != null)
        {
            // Elegimos un efecto al azar de nuestra bolsa de Scriptable Objects
            PowerUpSO randomData = availablePowerUps[Random.Range(0, availablePowerUps.Length)];

            // Instanciamos la "caja física" en el mapa
            GameObject newPowerUp = Instantiate(powerUpPrefab, randomSpawnPoint.position, powerUpPrefab.transform.rotation);

            // Le inyectamos los datos del efecto al script de la caja
            PowerUpCollectable collectable = newPowerUp.GetComponent<PowerUpCollectable>();
            if (collectable != null)
            {
                collectable.powerUpData = randomData;
            }

            // Aplicamos un color distinto para poder testear fácilmente
            ApplyVisualFeedback(newPowerUp, randomData.type);

            // Registramos la caja en nuestra lista
            activePowerUps.Add(newPowerUp);
        }
    }

    private Transform GetFreeSpawnPoint()
    {
        List<Transform> freePoints = new List<Transform>();

        // Filtramos los puntos que tienen un power-up activo a menos de 1 metro
        foreach (Transform sp in spawnPoints)
        {
            bool isOccupied = false;
            foreach (GameObject active in activePowerUps)
            {
                if (active != null && Vector3.Distance(sp.position, active.transform.position) < 1f)
                {
                    isOccupied = true;
                    break;
                }
            }

            if (!isOccupied)
            {
                freePoints.Add(sp);
            }
        }

        // Si hay al menos un punto libre, devolvemos uno al azar
        if (freePoints.Count > 0)
        {
            return freePoints[Random.Range(0, freePoints.Count)];
        }

        return null; // Todo está ocupado
    }

    private void ApplyVisualFeedback(GameObject powerUpObj, PowerUpType type)
    {
        Renderer rend = powerUpObj.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            switch (type)
            {
                case PowerUpType.SpeedBoost:
                    rend.material.color = Color.green;
                    break;
                case PowerUpType.ZeroDrag:
                    rend.material.color = Color.cyan;
                    break;
                case PowerUpType.FreezeHunters:
                    rend.material.color = Color.blue;
                    break;
            }
        }
    }
}