using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UniVRM10;

public class AvatarController : MonoBehaviour
{
    [Header("Component Setup")]
    public AudioSource avatarAudioSource;

    [Header("VRM Expressions")]
    [Tooltip("Drag the JohnCennaReal GameObject (the one with Vrm10Instance) here.")]
    public Vrm10Instance vrmInstance;

    [Header("Local Python Server")]
    public string localServerURL = "https://sloppily-fondling-judicial.ngrok-free.dev/generate";

    [Header("Test Controls")]
    public string wikipediaTopic = "Cat";
    public bool testSpeakNow = false;

    // --- THE FIFO AUDIO BUFFER ---
    private Queue<AudioClip> audioQueue = new Queue<AudioClip>();
    private bool isDownloading = false;
    private bool isPlaying = false;
    private bool isSpeaking = false;
    private Transform _lookTarget; // eyes follow this transform

    // --- JSON CLASSES ---
    [System.Serializable] public class WikipediaResponse { public string extract; }
    [System.Serializable] public class LocalTTSRequest { public string text; }

    void Start()
    {
        // Create a hidden transform the VRM eyes will follow for look-around
        _lookTarget = new GameObject("VRM_LookTarget").transform;
        if (vrmInstance != null)
            vrmInstance.LookAtTarget = _lookTarget;

        StartCoroutine(IdleAnimationLoop());
    }

    void Update()
    {
        if (testSpeakNow) 
        {
            testSpeakNow = false;
            StartCoroutine(FetchWikipediaSummary(wikipediaTopic));
        }
    }

    // ── VRM Expression helpers ────────────────────────────────────────────────
    private void SetSpeakingExpression()
    {
        if (vrmInstance == null || vrmInstance.Runtime == null) return;
        var expr = vrmInstance.Runtime.Expression;
        // Use relaxed instead of happy — relaxed opens the mouth without squinting/closing the eyes
        expr.SetWeight(ExpressionKey.CreateFromPreset(ExpressionPreset.relaxed),  1.0f);
        expr.SetWeight(ExpressionKey.CreateFromPreset(ExpressionPreset.happy),    0.0f);
        expr.SetWeight(ExpressionKey.CreateFromPreset(ExpressionPreset.neutral),  0.0f);
    }

    private void SetIdleExpression()
    {
        if (vrmInstance == null || vrmInstance.Runtime == null) return;
        var expr = vrmInstance.Runtime.Expression;
        expr.SetWeight(ExpressionKey.CreateFromPreset(ExpressionPreset.relaxed),  0.0f);
        expr.SetWeight(ExpressionKey.CreateFromPreset(ExpressionPreset.happy),    0.0f);
        expr.SetWeight(ExpressionKey.CreateFromPreset(ExpressionPreset.neutral),  1.0f);
    }

    // Idle loop: blink (expressions) + look-around (LookAt target, bone-driven)
    // VRoid models use bone-based LookAt — expression weights for lookLeft/Right/Up/Down do nothing.

    private IEnumerator IdleAnimationLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(3f, 6f));

            if (vrmInstance == null || vrmInstance.Runtime == null) continue;

            var expr = vrmInstance.Runtime.Expression;

            // Blink — only when idle; skip during speech so eyes stay open
            if (!isSpeaking)
            {
                var blinkKey = ExpressionKey.CreateFromPreset(ExpressionPreset.blink);
                expr.SetWeight(blinkKey, 1.0f);
                yield return new WaitForSeconds(0.12f);
                expr.SetWeight(blinkKey, 0.0f);
            }

            // Look-around only when idle — move the LookAt target (bone-driven eyes)
            if (!isSpeaking && _lookTarget != null)
            {
                yield return new WaitForSeconds(0.3f);

                // Build offsets relative to the model's facing direction
                Transform root = vrmInstance.transform;
                Vector3 eyeCenter = root.position + root.up * 1.6f + root.forward * 3f;

                // Four glance positions
                Vector3[] targets =
                {
                    eyeCenter + root.right  *  1.5f,          // right
                    eyeCenter - root.right  *  1.5f,          // left
                    eyeCenter + root.up     *  0.8f,          // up
                    eyeCenter - root.up     *  0.5f,          // down
                };

                Vector3 chosen = targets[Random.Range(0, targets.Length)];
                float holdTime = Random.Range(0.8f, 1.6f);

                _lookTarget.position = chosen;

                yield return new WaitForSeconds(holdTime);

                // Return to centre-forward
                _lookTarget.position = eyeCenter;
            }
        }
    }

    // --- PHASE 1: GET TEXT FROM WIKIPEDIA ---
    private IEnumerator FetchWikipediaSummary(string topic)
    {
        Debug.Log("1. Asking Wikipedia about: " + topic);
        string wikiURL = "https://en.wikipedia.org/api/rest_v1/page/summary/" + topic;

        using (UnityWebRequest webRequest = UnityWebRequest.Get(wikiURL))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Wikipedia Error: " + webRequest.error);
            }
            else
            {
                WikipediaResponse response = JsonUtility.FromJson<WikipediaResponse>(webRequest.downloadHandler.text);
                if (string.IsNullOrEmpty(response.extract)) yield break;

                Debug.Log("2. Wikipedia replied! Breaking into sentences...");
                ProcessAndSpeak(response.extract);
            }
        }
    }

    // --- PHASE 2: THE PIPELINE MANAGER ---
    public void ProcessAndSpeak(string fullText)
    {
        // 1. Send the massive paragraph through our new Smart Chunker!
        // We set the target length to 80 characters. 
        List<string> balancedSentences = SmartChunkText(fullText, 80);

        // 2. Start the "Producer" (Downloads audio in the background)
        if (!isDownloading) StartCoroutine(DownloadAudioQueue(balancedSentences.ToArray()));

        // 3. Start the "Consumer" (Plays audio as soon as it hits the queue)
        if (!isPlaying) StartCoroutine(PlayAudioQueue());
    }

    public void AnswerQuestion(string topic)
    {
        // Stop any current talking/downloading if the user interrupts with a new question
        StopAllCoroutines();
        audioQueue.Clear();
        isDownloading = false;
        isPlaying = false;

        // Start the new search
        StartCoroutine(FetchWikipediaSummary(topic));
    }

    // --- THE SMART CHUNKING ALGORITHM ---
    private List<string> SmartChunkText(string rawText, int targetCharLength)
    {
        List<string> finalChunks = new List<string>();
        string currentBucket = "";

        // Splits after [.!?] OR after a comma, but ONLY if the comma is followed by a space and a letter [a-zA-Z].
        string[] fragments = System.Text.RegularExpressions.Regex.Split(rawText, @"(?<=[.!?\n])|(?<=,)(?=\s+[a-zA-Z])");

        foreach (string fragment in fragments)
        {
            string cleanFragment = fragment.Trim();
            if (string.IsNullOrEmpty(cleanFragment)) continue;

            // 2. Add the fragment to the bucket
            if (currentBucket.Length == 0)
                currentBucket = cleanFragment;
            else
                currentBucket += " " + cleanFragment;

            // 3. Inspect the last character of our bucket
            char lastChar = currentBucket[currentBucket.Length - 1];
            bool isHardStop = (lastChar == '.' || lastChar == '!' || lastChar == '?');
            bool isSoftStop = (lastChar == ',');

            // 4. THE SMART FLUSH LOGIC
            // - ALWAYS flush if we hit the end of a full sentence (Hard Stop).
            // - ONLY flush at a comma (Soft Stop) IF we have crossed the character limit.
            if (isHardStop || (isSoftStop && currentBucket.Length >= targetCharLength))
            {
                // If we are flushing exactly on a comma, swap it to a period 
                // so Piper knows to drop its vocal pitch naturally.
                if (isSoftStop)
                {
                    currentBucket = currentBucket.Substring(0, currentBucket.Length - 1) + ".";
                }

                finalChunks.Add(currentBucket);
                currentBucket = ""; // Empty the bucket
            }
        }

        // 5. Catch any leftover text at the very end
        if (currentBucket.Trim().Length > 0)
        {
            char lastChar = currentBucket[currentBucket.Length - 1];
            if (lastChar != '.' && lastChar != '!' && lastChar != '?')
            {
                if (lastChar == ',') currentBucket = currentBucket.Substring(0, currentBucket.Length - 1);
                currentBucket += ".";
            }
            finalChunks.Add(currentBucket);
        }

        return finalChunks;
    }

    // --- PHASE 3: THE PRODUCER (FETCH FROM PYTHON) ---
    private IEnumerator DownloadAudioQueue(string[] sentences)
    {
        isDownloading = true;

        for (int i = 0; i < sentences.Length; i++)
        {
            // Clean up the sentence and skip if it's empty
            string sentence = sentences[i].Trim();
            if (string.IsNullOrEmpty(sentence)) continue;

            // Add back the punctuation for natural Piper pausing
            //sentence += ".";
            Debug.Log($"[PRODUCER] Requesting Sentence {i + 1}/{sentences.Length}: {sentence}");

            LocalTTSRequest requestData = new LocalTTSRequest { text = sentence };
            string jsonBody = JsonUtility.ToJson(requestData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest webRequest = new UnityWebRequest(localServerURL, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                //Send the secret handshake to bypass the HTML warning page!
                webRequest.SetRequestHeader("ngrok-skip-browser-warning", "true");
                webRequest.SetRequestHeader("Content-Type", "application/json");

                yield return webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Local Server Error: " + webRequest.error);
                }
                else
                {
                    byte[] rawBytes = webRequest.downloadHandler.data;
                    if (rawBytes != null && rawBytes.Length > 0)
                    {
                        // Decode raw PCM sound waves (Identical to your working code!)
                        int sampleCount = rawBytes.Length / 2;
                        float[] audioData = new float[sampleCount];

                        for (int s = 0; s < sampleCount; s++)
                        {
                            short sample16Bit = System.BitConverter.ToInt16(rawBytes, s * 2);
                            audioData[s] = sample16Bit / 32768f;
                        }

                        int fadeSamples = (int)(22050 * 0.03f); // 30 milliseconds of audio
                        if (fadeSamples * 2 < sampleCount) // Make sure the clip is long enough to fade
                        {
                            for (int s = 0; s < fadeSamples; s++)
                            {
                                float fadeMultiplier = (float)s / fadeSamples;
                                audioData[s] *= fadeMultiplier; // Fade in the start
                                audioData[sampleCount - 1 - s] *= fadeMultiplier; // Fade out the end
                            }
                        }

                        AudioClip clip = AudioClip.Create($"Sentence_{i}", sampleCount, 1, 22050, false);
                        clip.SetData(audioData, 0);

                        // Push the finished audio clip into the FIFO buffer
                        audioQueue.Enqueue(clip);
                        Debug.Log($"[PRODUCER] Sentence {i + 1} added to Queue!");
                    }
                }
            }
        }
        isDownloading = false;
        Debug.Log("[PRODUCER] Finished downloading all sentences.");
    }

    // --- PHASE 4: THE CONSUMER (PLAY FROM QUEUE) ---
    private IEnumerator PlayAudioQueue()
    {
        isPlaying = true;

        // THE FIX: THE PRE-BUFFER WAITING ROOM
        // Do not start playing until we have at least 2 chunks ready, 
        // OR until the download completely finishes (for very short sentences).
        while (isDownloading && audioQueue.Count < 2)
        {
            yield return null; // Wait here and let Python work...
        }

        // Now start the normal playback loop!
        while (isDownloading || audioQueue.Count > 0)
        {
            if (audioQueue.Count > 0)
            {
                AudioClip nextClip = audioQueue.Dequeue();

                avatarAudioSource.clip = nextClip;
                avatarAudioSource.Play();
                isSpeaking = true;
                SetSpeakingExpression();

                Debug.Log($"[CONSUMER] Playing audio clip. Length: {nextClip.length:F1} seconds. ({audioQueue.Count} left in queue)");

                while (avatarAudioSource.isPlaying)
                {
                    yield return null;
                }

                yield return new WaitForSeconds(0.3f);
            }
            else
            {
                yield return null;
            }
        }

        isSpeaking = false;
        SetIdleExpression();
        isPlaying = false;
        Debug.Log("[CONSUMER] Queue is empty and finished playing.");
    }
}