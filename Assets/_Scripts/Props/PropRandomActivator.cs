using UnityEngine;
using System.Collections.Generic;

public class PropRandomActivator : MonoBehaviour
{
    [Header("Chance this prop will appear (0 = never, 1 = always)")]
    [Range(0f, 1f)] public float activationChance = 0.5f;

    [Header("Spawn points where this prop can appear")]
    public List<Transform> spawnPoints;

    private void Awake()
    {
        gameObject.SetActive(false); // Ensure it's disabled at scene load
    }

    private void Start()
    {
        if (Random.value > activationChance) return;

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            return;
        }

        Transform chosen = spawnPoints[Random.Range(0, spawnPoints.Count)];
        transform.position = chosen.position;
        transform.rotation = chosen.rotation;
        gameObject.SetActive(true);
    }
}
