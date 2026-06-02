using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerUpManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private int maxActivePowerUps = 3;

    [Header("References")]
    [SerializeField] private GameObject powerUpPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private PowerUpSO[] availablePowerUps;

    //Object Pool
    private List<GameObject> powerUpPool = new List<GameObject>();
    private Coroutine spawnRoutine;

    private void Awake()
    {
        // POOLING: Instanciamos los power-ups necesarios al inicio y los dejamos apagados
        for (int i = 0; i < maxActivePowerUps; i++)
        {
            GameObject obj = Instantiate(powerUpPrefab);
            obj.SetActive(false);
            powerUpPool.Add(obj);
        }
    }

    private void OnEnable()
    {
        GameManager.OnGameStateChanged += PowerUpInit;
    }
    private void OnDisable()
    {
        GameManager.OnGameStateChanged -= PowerUpInit;
    }

    private void PowerUpInit(GameState gameState)
    {
        if (gameState == GameState.ActiveMatch)
        {
            if (spawnPoints.Length == 0 || availablePowerUps.Length == 0 || powerUpPrefab == null)
            {
                Debug.LogWarning("PowerUpManager: Missing references.");
                return;
            }

            if (spawnRoutine != null) StopCoroutine(spawnRoutine);
            spawnRoutine = StartCoroutine(SpawnRoutine());
            Debug.Log("PowerUpManager initialized");
        }
        else
        {
            // Seguridad: Si el juego termina o vuelve al lobby, apagamos todo y frenamos el spawner
            if (spawnRoutine != null) StopCoroutine(spawnRoutine);
            foreach (var obj in powerUpPool)
            {
                if (obj.activeSelf) obj.SetActive(false);
            }
        }
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            // Contamos cuántos de nuestros power-ups están actualmente prendidos en la arena
            int activeCount = 0;
            foreach (var obj in powerUpPool)
            {
                if (obj.activeSelf) activeCount++;
            }

            // Si hay menos del máximo permitido, buscamos uno del pool para reciclar
            if (activeCount < maxActivePowerUps)
            {
                SpawnPowerUp();
                Debug.Log("PowerUp spawned from pool");
            }
        }
    }

    private void SpawnPowerUp()
    {
        // 1. Buscamos el primer objeto del pool que esté inactivo
        GameObject powerUpToSpawn = null;
        foreach (var obj in powerUpPool)
        {
            if (!obj.activeSelf)
            {
                powerUpToSpawn = obj;
                break; // Encontramos uno, cortamos la búsqueda
            }
        }

        if (powerUpToSpawn == null) return;

        // 2. Buscamos un punto libre en el mapa
        Transform randomSpawnPoint = GetFreeSpawnPoint();

        if (randomSpawnPoint != null)
        {
            PowerUpSO randomData = availablePowerUps[Random.Range(0, availablePowerUps.Length)];

            // 3. Teletransportamos el objeto reciclado a la nueva posición
            powerUpToSpawn.transform.position = randomSpawnPoint.position;
            powerUpToSpawn.transform.rotation = powerUpPrefab.transform.rotation;

            // 4. Le inyectamos los datos nuevos
            PowerUpCollectable collectable = powerUpToSpawn.GetComponent<PowerUpCollectable>();
            if (collectable != null)
            {
                collectable.powerUpData = randomData;
            }

            ApplyVisualFeedback(powerUpToSpawn, randomData.type);

            // 5. Lo "Encendemos" (Esto dispara automáticamente el OnEnable del Collectable)
            powerUpToSpawn.SetActive(true);
        }
    }

    private Transform GetFreeSpawnPoint()
    {
        List<Transform> freePoints = new List<Transform>();

        foreach (Transform sp in spawnPoints)
        {
            bool isOccupied = false;
            foreach (GameObject obj in powerUpPool)
            {
                // Solo chequeamos colisiones con los power-ups que están activos
                if (obj.activeSelf && Vector3.Distance(sp.position, obj.transform.position) < 1f)
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

        if (freePoints.Count > 0)
        {
            return freePoints[Random.Range(0, freePoints.Count)];
        }

        return null;
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