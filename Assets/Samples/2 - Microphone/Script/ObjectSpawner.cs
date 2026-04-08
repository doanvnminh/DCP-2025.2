using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    [Header("3D GameObjects")]
    public GameObject cubePrefab;
    public GameObject spherePrefab;
    public GameObject cylinderPrefab;

    [Header("Spawn Location")]
    public Transform spawnPoint; 

    public void SpawnShape(string shapeName)
    {
        GameObject objectToSpawn = null;

        // Map the spoken word to the correct 3D model
        switch (shapeName)
        {
            case "cube":
                objectToSpawn = cubePrefab;
                break;
            case "sphere":
                objectToSpawn = spherePrefab;
                break;
            case "cylinder":
                objectToSpawn = cylinderPrefab;
                break;
        }

        // If we found a valid match, instantiate it!
        if (objectToSpawn != null)
        {
            Instantiate(objectToSpawn, spawnPoint.position, Quaternion.identity);
            Debug.Log("Success! Spawned a " + shapeName);
        }
    }
}
