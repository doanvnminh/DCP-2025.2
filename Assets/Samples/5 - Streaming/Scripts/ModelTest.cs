using UnityEngine;

public class ModelTest : MonoBehaviour
{
    public GameObject prefab;

    void Start()
    {
        Instantiate(prefab, new Vector3(-4, 0, 4), Quaternion.identity);
    }
}