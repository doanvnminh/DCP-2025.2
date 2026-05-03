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

    [Header("Default Material (For Raw Meshes)")]
    public Material defaultMaterial;

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

                if (success && gltf.GetMeshes() != null && gltf.GetMeshes().Length > 0)
                {
                    // 1. Grab the raw geometry
                    Mesh rawGeometry = gltf.GetMeshes()[0];

                    // ==========================================
                    // MAGIC FIX 1: AUTO-CENTER THE PIVOT POINT
                    // ==========================================
                    // We look at the bounding box, find the exact visual center, 
                    // and offset every single vertex so the center is exactly at (0,0,0).
                    Vector3[] vertices = rawGeometry.vertices;
                    Vector3 meshCenter = rawGeometry.bounds.center;

                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] -= meshCenter;
                    }

                    rawGeometry.vertices = vertices;
                    rawGeometry.RecalculateBounds(); // Tell Unity we moved the geometry!


                    // ==========================================
                    // MAGIC FIX 2: AUTO-SCALE TO A STANDARD SIZE
                    // ==========================================
                    // We find the longest side of the model, and calculate exactly 
                    // how much we need to multiply it by to make it exactly 1.5 meters big.
                    float maxSide = Mathf.Max(rawGeometry.bounds.size.x, rawGeometry.bounds.size.y, rawGeometry.bounds.size.z);
                    float targetSizeInMeters = 1.5f; // Change this if you want bigger/smaller objects!
                    float scaleFactor = targetSizeInMeters / maxSide;


                    // 2. Create the GameObject at your exact spawn point
                    GameObject newObject = new GameObject("API_Spawned_" + shapeName);
                    newObject.transform.position = spawnPoint.position;

                    // Apply our calculated scale factor so it isn't tiny!
                    newObject.transform.localScale = Vector3.one * scaleFactor;

                    // 3. Attach the fixed Mesh
                    MeshFilter filter = newObject.AddComponent<MeshFilter>();
                    filter.mesh = rawGeometry;

                    MeshRenderer renderer = newObject.AddComponent<MeshRenderer>();
                    if (defaultMaterial != null) renderer.material = defaultMaterial;

                    // 4. Setup Physics
                    MeshCollider collider = newObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = rawGeometry;
                    collider.convex = true;

                    Rigidbody rb = newObject.AddComponent<Rigidbody>();

                    // We calculate mass based on our NEW scaled size, not the tiny original size
                    rb.mass = targetSizeInMeters * 2f;

                    Debug.Log($"Success! Downloaded, Centered, Scaled, and spawned: {shapeName}");
                }
                else
                {
                    Debug.LogError("Failed to extract raw mesh from the downloaded file.");
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