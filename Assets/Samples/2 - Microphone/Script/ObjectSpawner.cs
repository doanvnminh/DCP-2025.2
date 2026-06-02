using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using GLTFast;
using System.Text.RegularExpressions;

[System.Serializable]
public struct MaterialSound
{
    public string material; // "wood", "metal", "stone", "plastic", "fabric", "flesh", "fur"
    public AudioClip clip;
}

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

    [Header("Colab Server")]
    [Tooltip("Paste the ngrok base URL printed by the Colab cell, e.g. https://xxxx.ngrok-free.app")]
    public string colabServerBaseURL = "";

    [Header("Scene Scale Limits")]
    [Tooltip("Real-world sizes are preserved as ratios but clamped so nothing breaks the scene.")]
    public float maxSpawnSizeMeters = 4.0f;
    public float minSpawnSizeMeters = 0.15f;

    [Header("Impact Sounds — assign one AudioClip per material")]
    [Tooltip("The Colab server classifies each spawned object's material. Assign matching audio clips here.")]
    public MaterialSound[] materialSounds;


    public void SpawnShape(string shapeName)
    {
        if (shapeName == "cube" || shapeName == "box")
        {
            Instantiate(cubePrefab, GetFreeSpawnPosition(0.6f), Quaternion.identity);
            Debug.Log("Spawned local prefab: Cube");
        }
        else if (shapeName == "sphere" || shapeName == "ball")
        {
            Instantiate(spherePrefab, GetFreeSpawnPosition(0.6f), Quaternion.identity);
            Debug.Log("Spawned local prefab: Sphere");
        }
        else if (shapeName == "cylinder")
        {
            Instantiate(cylinderPrefab, GetFreeSpawnPosition(0.6f), Quaternion.identity);
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

        // Fire all three requests at the same time — no need to wait for one before the others.
        Task<string> urlTask      = GetModelUrlFromAPI(searchTerm);
        Task<float>  sizeTask     = GetRealWorldSizeAsync(searchTerm);
        Task<string> materialTask = GetObjectMaterialAsync(searchTerm);
        await Task.WhenAll(urlTask, sizeTask, materialTask);

        string downloadUrl   = urlTask.Result;
        float  realWorldSize = sizeTask.Result;
        string material      = materialTask.Result;

        if (string.IsNullOrEmpty(downloadUrl))
        {
            Debug.LogError($"Poly Pizza could not find a 3D model for: {searchTerm}");
            SpawnFallback(searchTerm);
            return;
        }

        float targetSize = Mathf.Clamp(realWorldSize, minSpawnSizeMeters, maxSpawnSizeMeters);
        Debug.Log($"[SIZE] '{searchTerm}' → {realWorldSize:F2}m real-world, clamped to {targetSize:F2}m in scene");

        AudioClip impactClip = GetClipForMaterial(material);
        Vector3 spawnPos = GetFreeSpawnPosition(targetSize * 0.5f);
        await DownloadAndBuildRawMesh(downloadUrl, searchTerm, targetSize, spawnPos, impactClip);
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

    private async Task<float> GetRealWorldSizeAsync(string objectName)
    {
        if (string.IsNullOrEmpty(colabServerBaseURL))
        {
            Debug.LogError("[SIZE] colabServerBaseURL is empty! Paste your ngrok URL in the Inspector.");
            return 1.0f;
        }

        string endpoint = colabServerBaseURL.TrimEnd('/') + "/object_size";
        byte[] body = System.Text.Encoding.UTF8.GetBytes("{\"name\":\"" + objectName + "\"}");
        Debug.Log($"[SIZE] Sending to {endpoint}  body: {{name: '{objectName}'}}");

        using (UnityWebRequest req = new UnityWebRequest(endpoint, "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            string rawResponse = req.downloadHandler.text;
            long   httpCode    = req.responseCode;

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[SIZE] Server replied HTTP {httpCode}: {rawResponse}");

                Match m = Regex.Match(rawResponse, @"""size""\s*:\s*([\d.]+)");
                if (m.Success && float.TryParse(m.Groups[1].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float size))
                {
                    return size;
                }

                Debug.LogError($"[SIZE] Got HTTP {httpCode} but could not parse 'size' from: {rawResponse}");
            }
            else
            {
                Debug.LogError($"[SIZE] Request failed. HTTP {httpCode} | Network error: {req.error} | Body: {rawResponse}");
            }

            return 1.0f; // only reached if server is truly unreachable
        }
    }

    private async Task<string> GetObjectMaterialAsync(string objectName)
    {
        if (string.IsNullOrEmpty(colabServerBaseURL))
            return "wood"; // silent fallback

        string endpoint = colabServerBaseURL.TrimEnd('/') + "/object_material";
        byte[] body = System.Text.Encoding.UTF8.GetBytes("{\"name\":\"" + objectName + "\"}");

        using (UnityWebRequest req = new UnityWebRequest(endpoint, "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string json = req.downloadHandler.text;
                Match m = Regex.Match(json, @"\""material\""\s*:\s*\""([a-z]+)\""");
                if (m.Success)
                {
                    string mat = m.Groups[1].Value;
                    Debug.Log($"[MATERIAL] '{objectName}' → {mat}");
                    return mat;
                }
            }
            else
            {
                Debug.LogWarning($"[MATERIAL] Request failed for '{objectName}': {req.error}");
            }

            return "wood"; // fallback
        }
    }

    private AudioClip GetClipForMaterial(string material)
    {
        if (materialSounds == null) return null;
        foreach (var ms in materialSounds)
            if (string.Equals(ms.material, material, System.StringComparison.OrdinalIgnoreCase))
                return ms.clip;
        return null; // silent if no clip assigned for this material
    }

    // Finds a clear spot around the spawn point so objects don't pile on top of each other.
    // radius should be half the object's expected size.
    private Vector3 GetFreeSpawnPosition(float radius = 0.5f)
    {
        Vector3 basePos = spawnPoint.position;
        // Always spawn a bit above so the object falls into place.
        basePos.y += radius * 0.5f;

        // Try up to 12 candidate positions, spiralling outward.
        for (int i = 0; i < 12; i++)
        {
            Vector3 candidate;
            if (i == 0)
            {
                candidate = basePos;
            }
            else
            {
                float angle  = i * 137.5f * Mathf.Deg2Rad; // golden-angle spiral — uniform spread
                float spread = radius * 1.5f + (i / 12f) * 4f;
                candidate = basePos + new Vector3(Mathf.Cos(angle) * spread, 0f, Mathf.Sin(angle) * spread);
            }

            if (!Physics.CheckSphere(candidate, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                return candidate;
        }

        // All spots occupied — fall back to a random offset so objects at least separate over time.
        Vector2 rnd = UnityEngine.Random.insideUnitCircle.normalized * (radius * 2f + UnityEngine.Random.Range(0f, 3f));
        return basePos + new Vector3(rnd.x, 0f, rnd.y);
    }

    private async Task DownloadAndBuildRawMesh(string url, string shapeName, float targetSizeInMeters = 1.5f, Vector3? spawnPosition = null, AudioClip impactClip = null)
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
                    newObject.transform.position = spawnPosition ?? spawnPoint.position;

                    // This automatically creates child objects with MeshFilters and MeshRenderers applied.
                    bool instanced = await gltf.InstantiateMainSceneAsync(newObject.transform);

                    if (instanced)
                    {
                        // Measure the combined world-space bounds of every renderer child.
                        // GLTFast may import models at wildly different internal scales
                        // (some in mm, some in m), so we always normalise first.
                        Renderer[] renderers = newObject.GetComponentsInChildren<Renderer>();
                        Bounds combinedBounds = new Bounds(newObject.transform.position, Vector3.zero);
                        foreach (Renderer r in renderers)
                            combinedBounds.Encapsulate(r.bounds);

                        float rawMax = Mathf.Max(combinedBounds.size.x, combinedBounds.size.y, combinedBounds.size.z);

                        // ── Step 1: normalise model to exactly 1 m on its largest axis ────────
                        // ── Step 2: scale by the real-world size returned by the AI ─────────
                        // Combined: finalScale = targetSizeInMeters / rawMax
                        Debug.Log($"[SCALE] '{shapeName}' — raw model max-dim: {rawMax:F4}m  |  AI target: {targetSizeInMeters:F3}m");

                        if (rawMax > 0.0001f)
                        {
                            float scaleFactor = targetSizeInMeters / rawMax;
                            newObject.transform.localScale = Vector3.one * scaleFactor;
                            Debug.Log($"[SCALE] '{shapeName}' — applied scale: {scaleFactor:F4}  →  final size ≈ {targetSizeInMeters:F2}m");
                        }
                        else
                        {
                            Debug.LogWarning($"[SCALE] '{shapeName}' — bounds too small ({rawMax:F6}), scale skipped");
                        }

                        // We add a MeshCollider to every single piece of the colorful model
                        foreach (Renderer r in renderers)
                        {
                            MeshCollider collider = r.gameObject.AddComponent<MeshCollider>();
                            collider.convex = true;
                        }

                        // Add the Rigidbody to the main parent so the whole thing falls together.
                        // Mass uses cube of size (volume proxy) so relative weight feels correct:
                        // cat ~0.4kg, car ~34kg, truck ~430kg — heavier objects won't get blasted
                        // by lighter ones landing nearby.
                        Rigidbody rb = newObject.AddComponent<Rigidbody>();
                        rb.mass = Mathf.Max(0.1f, Mathf.Pow(targetSizeInMeters, 3f) * 10f);

                        // Wire up the impact system: camera shake + blast on landing.
                        SpawnedObjectImpact1 impact = newObject.AddComponent<SpawnedObjectImpact1>();
                        impact.sizeInMeters = targetSizeInMeters;
                        impact.impactClip   = impactClip;
                        
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
        GameObject fallbackBox = Instantiate(cubePrefab, GetFreeSpawnPosition(0.6f), Quaternion.identity);
        fallbackBox.name = "Fallback_" + shapeName;
    }
}