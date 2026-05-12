using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using GLTFast;
using System.Text.RegularExpressions;

public class ObjectSpawner : MonoBehaviour
{
    [Header("Standard 3D Prefabs")]
    public GameObject cubePrefab;
    public GameObject spherePrefab;
    public GameObject cylinderPrefab;

    [Header("Spawn Location")]
    public Transform spawnPoint;

    [Header("Poly Pizza API Setup")]
    public string polyPizzaApiKey = "";
    // Note: If api.poly.pizza is down, this will still fail, but we will catch the error gracefully.
    private string searchEndpoint = "https://api.poly.pizza/v1.1/search/";


    public void SpawnShape(string shapeName)
    {
        if (shapeName == "cube" || shapeName == "box")
        {
            Instantiate(cubePrefab, spawnPoint.position, Quaternion.identity);
            Debug.Log("Spawned local prefab: Cube");
        }
        else if (shapeName == "sphere" || shapeName == "ball")
        {
            Instantiate(spherePrefab, spawnPoint.position, Quaternion.identity);
            Debug.Log("Spawned local prefab: Sphere");
        }
        else if (shapeName == "cylinder")
        {
            Instantiate(cylinderPrefab, spawnPoint.position, Quaternion.identity);
            Debug.Log("Spawned local prefab: Cylinder");
        }
        else
        {
            Debug.Log($"Local prefab not found. Asking Poly Pizza API for a: {shapeName}");
            FetchAndSpawnFromAPI(shapeName);
        }
    }

    private async void FetchAndSpawnFromAPI(string searchTerm)
    {
        if (string.IsNullOrEmpty(polyPizzaApiKey))
        {
            Debug.LogError("Poly Pizza API Key is missing! Please paste it in the Inspector.");
            SpawnFallback(searchTerm);
            return;
        }

        string downloadUrl = await GetModelUrlFromAPI(searchTerm);

        if (string.IsNullOrEmpty(downloadUrl))
        {
            Debug.LogError($"Poly Pizza could not find a 3D model for: {searchTerm}");
            SpawnFallback(searchTerm);
            return;
        }

        await DownloadAndBuildRawMesh(downloadUrl, searchTerm);
    }

    // Inspired by Pizzabox, but kept simple using standard Tasks
    private async Task<string> GetModelUrlFromAPI(string keyword)
    {
        if (keyword.Length <= 2) return null;

        string url = searchEndpoint + keyword;

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("X-Auth-Token", polyPizzaApiKey);

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[API ERROR] The Poly Pizza API server might be down or blocked: {request.error}");
                return null;
            }

            string rawJson = request.downloadHandler.text;

            // Still using Regex because it's safer than Unity's strict JsonUtility
            Match match = Regex.Match(rawJson, @"""Download""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value.Replace("\\/", "/");
            }

            return null;
        }
    }

    private async Task DownloadAndBuildRawMesh(string url, string shapeName)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                byte[] modelBytes = request.downloadHandler.data;
                var gltf = new GltfImport();
                bool success = await gltf.LoadGltfBinary(modelBytes);

                if (success)
                {
                    GameObject newObject = new GameObject("API_Spawned_" + shapeName);
                    newObject.transform.position = spawnPoint.position;

                    // This automatically creates child objects with MeshFilters and MeshRenderers applied.
                    bool instanced = await gltf.InstantiateMainSceneAsync(newObject.transform);

                    if (instanced)
                    {
                        // Because GLTFast might create multiple child meshes, we measure the combined size
                        Bounds combinedBounds = new Bounds(newObject.transform.position, Vector3.zero);
                        Renderer[] renderers = newObject.GetComponentsInChildren<Renderer>();

                        foreach (Renderer r in renderers)
                        {
                            combinedBounds.Encapsulate(r.bounds);
                        }

                        float maxSide = Mathf.Max(combinedBounds.size.x, combinedBounds.size.y, combinedBounds.size.z);
                        float targetSizeInMeters = 1.5f;

                        if (maxSide > 0)
                        {
                            float scaleFactor = targetSizeInMeters / maxSide;
                            newObject.transform.localScale = Vector3.one * scaleFactor;
                        }

                        // We add a MeshCollider to every single piece of the colorful model
                        foreach (Renderer r in renderers)
                        {
                            MeshCollider collider = r.gameObject.AddComponent<MeshCollider>();
                            collider.convex = true;
                        }

                        // Add the Rigidbody to the main parent so the whole thing falls together
                        Rigidbody rb = newObject.AddComponent<Rigidbody>();
                        rb.mass = targetSizeInMeters * 2f;

                        Debug.Log($"Success! Downloaded with original colors, Scaled, and spawned: {shapeName}");
                    }
                    else
                    {
                        Debug.LogError("GLTFast failed to instantiate the visual scene.");
                        SpawnFallback(shapeName);
                    }
                }
                else
                {
                    Debug.LogError("Failed to load GLTF binary data.");
                    SpawnFallback(shapeName);
                }
            }
            else
            {
                Debug.LogError($"[DOWNLOAD ERROR] Failed to download model file: {request.error}");
                SpawnFallback(shapeName);
            }
        }
    }
    // A helper function to always spawn something even if the API fails
    private void SpawnFallback(string shapeName)
    {
        Debug.LogWarning($"Spawning a fallback cube instead of {shapeName}.");
        GameObject fallbackBox = Instantiate(cubePrefab, spawnPoint.position, Quaternion.identity);
        fallbackBox.name = "Fallback_" + shapeName;
    }
}