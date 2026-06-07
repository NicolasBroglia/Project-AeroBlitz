using UnityEngine;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance;

    #region Variables

    [Header("Prefabs & Spawns")]
    [SerializeField] private GameObject hunterPrefab;
    [SerializeField] private GameObject snitchPrefab;
    [Tooltip("The center of the map. The Snitch ALWAYS spawns here.")]
    [SerializeField] private Transform snitchSpawnPoint;
    [Tooltip("All the surrounding spots. Hunters are randomly placed here.")]
    [SerializeField] private Transform[] hunterSpawnPoints;

    [Header("Vertical Lane Rules")]
    public int maxLanes = 5;
    public float minHeight = 0.5f;
    public float maxHeight = 4f;

    #endregion

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    #region Public Spawning API

    public List<GameObject> SpawnAllPlayers(List<int> connectedDevices)
    {
        List<GameObject> spawnedPlayers = new List<GameObject>();
        if (connectedDevices.Count == 0) return spawnedPlayers;

        List<Transform> camTargets = new List<Transform>();
        List<Transform> availableHunterSpawns = new List<Transform>(hunterSpawnPoints);

        for (int i = 0; i < connectedDevices.Count; i++)
        {
            if (i == 0) // Snitch Spawning
            {
                float snappedY = GetClosestLaneHeight(snitchSpawnPoint.position.y);
                Vector3 exactSpawnPos = new Vector3(snitchSpawnPoint.position.x, snappedY, snitchSpawnPoint.position.z);

                GameObject snitch = Instantiate(snitchPrefab, exactSpawnPos, snitchSpawnPoint.rotation);
                snitch.GetComponent<InputSystemManager>().deviceID = connectedDevices[i];

                if (snitch.TryGetComponent<SnitchController>(out var snitchController))
                {
                    snitchController.InitializeAtSpawn(exactSpawnPos, snitchSpawnPoint.rotation);
                }

                spawnedPlayers.Add(snitch);
                camTargets.Add(snitch.transform);
            }
            else // Hunter Spawning
            {
                Transform chosenSpawn = snitchSpawnPoint;

                if (availableHunterSpawns.Count > 0)
                {
                    int randomIndex = Random.Range(0, availableHunterSpawns.Count);
                    chosenSpawn = availableHunterSpawns[randomIndex];
                    availableHunterSpawns.RemoveAt(randomIndex);
                }

                float snappedY = GetClosestLaneHeight(chosenSpawn.position.y);
                Vector3 exactSpawnPos = new Vector3(chosenSpawn.position.x, snappedY, chosenSpawn.position.z);

                GameObject hunter = Instantiate(hunterPrefab, exactSpawnPos, chosenSpawn.rotation);
                hunter.GetComponent<InputSystemManager>().deviceID = connectedDevices[i];

                if (hunter.TryGetComponent<HunterController>(out var hunterController))
                {
                    hunterController.InitializeAtSpawn(exactSpawnPos, chosenSpawn.rotation);
                }

                spawnedPlayers.Add(hunter);
                camTargets.Add(hunter.transform);
            }
        }

        IsometricCameraRig cam = FindAnyObjectByType<IsometricCameraRig>();
        if (cam != null)
        {
            cam.targets = camTargets;
            cam.SnapToTargets();
        }

        return spawnedPlayers;
    }

    public GameObject SpawnSinglePlayerAsHunter(int newDeviceID)
    {
        Transform chosenSpawn = snitchSpawnPoint;
        if (hunterSpawnPoints.Length > 0)
        {
            chosenSpawn = hunterSpawnPoints[Random.Range(0, hunterSpawnPoints.Length)];
        }

        float snappedY = GetClosestLaneHeight(chosenSpawn.position.y);
        Vector3 exactSpawnPos = new Vector3(chosenSpawn.position.x, snappedY, chosenSpawn.position.z);

        GameObject hunter = Instantiate(hunterPrefab, exactSpawnPos, chosenSpawn.rotation);
        hunter.GetComponent<InputSystemManager>().deviceID = newDeviceID;

        if (hunter.TryGetComponent<HunterController>(out var controller))
        {
            controller.InitializeAtSpawn(exactSpawnPos, chosenSpawn.rotation);
            controller.ChangeState(HunterController.HunterState.Chasing);
        }

        IsometricCameraRig cam = FindAnyObjectByType<IsometricCameraRig>();
        if (cam != null)
        {
            if (!cam.targets.Contains(hunter.transform))
            {
                cam.targets.Add(hunter.transform);
            }
        }

        return hunter;
    }

    #endregion

    #region Lane Math Methods

    public float GetClosestLaneHeight(float rawY)
    {
        float heightPercentage = Mathf.InverseLerp(minHeight, maxHeight, rawY);
        int closestLane = Mathf.RoundToInt(heightPercentage * (maxLanes - 1));
        closestLane = Mathf.Clamp(closestLane, 0, maxLanes - 1);

        return Mathf.Lerp(minHeight, maxHeight, (float)closestLane / (maxLanes - 1));
    }

    public float GetLaneHeight(int laneIndex)
    {
        return Mathf.Lerp(minHeight, maxHeight, (float)laneIndex / (maxLanes - 1));
    }

    #endregion
}