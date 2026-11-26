
using UnityEngine;
using System.Collections.Generic;

public class RandomDollSpawner : MonoBehaviour
{
    [Header("Doll Objects")]
    [SerializeField] private GameObject[] dolls;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    private void Start()
    {
        SpawnDolls();
    }

    private void SpawnDolls()
    {
        if (dolls.Length == 0 || spawnPoints.Length == 0)
        {
            Debug.LogWarning("Dolls atau Spawn Points belum diatur!");
            return;
        }

        if (dolls.Length > spawnPoints.Length)
        {
            Debug.LogWarning("Jumlah boneka melebihi jumlah spawn point!");
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

        // Tempatkan setiap boneka
        for (int i = 0; i < dolls.Length; i++)
        {
            if (dolls[i] == null)
                continue;

            dolls[i].transform.SetPositionAndRotation(
                availablePoints[i].position,
                availablePoints[i].rotation
            );

            dolls[i].SetActive(true);
        }
    }
}
