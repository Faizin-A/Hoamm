
using UnityEngine;
using System.Collections.Generic;

public class RandomKeySpawner : MonoBehaviour
{
    [Header("Key Objects")]
    [SerializeField] private GameObject[] keys;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    private void Start()
    {
        SpawnKeys();
    }

    private void SpawnKeys()
    {
        if (keys.Length == 0 || spawnPoints.Length == 0)
        {
            Debug.LogWarning("Keys atau Spawn Points belum diatur!");
            return;
        }

        if (keys.Length > spawnPoints.Length)
        {
            Debug.LogWarning("Jumlah kunci melebihi jumlah spawn point!");
            return;
        }

        // Buat daftar posisi spawn
        List<Transform> availablePoints =
            new List<Transform>(spawnPoints);

        // Acak posisi spawn
        for (int i = 0; i < availablePoints.Count; i++)
        {
            int randomIndex = Random.Range(i, availablePoints.Count);

            Transform temp = availablePoints[i];
            availablePoints[i] = availablePoints[randomIndex];
            availablePoints[randomIndex] = temp;
        }

        // Tempatkan setiap kunci
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == null)
                continue;

            keys[i].transform.SetPositionAndRotation(
                availablePoints[i].position,
                availablePoints[i].rotation
            );

            keys[i].gameObject.SetActive(true);
        }
    }
}
