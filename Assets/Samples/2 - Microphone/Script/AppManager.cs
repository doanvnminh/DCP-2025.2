using UnityEngine;
using TMPro;
public class AppManager : MonoBehaviour
{
    public static AppManager Instance;

    [Header("Connected Systems")]
    public ObjectSpawner spawner;
    public AvatarController avatarController;
    public WikiAPI wikiApi;

    [Header("Testing UI")]
    public TMP_InputField textInputField;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (textInputField != null)
        {
            textInputField.onSubmit.AddListener(OnTextInputSubmit);
        }
    }

    public void OnTextInputSubmit(string typedText)
    {
        if (!string.IsNullOrWhiteSpace(typedText))
        {
            Debug.Log($"[TEXT INPUT] Sent: {typedText}");
            ProcessVoiceCommand(typedText); // Send it to the exact same logic the voice uses!

            textInputField.text = ""; 
            textInputField.ActivateInputField(); 
        }
    }

    public void ProcessVoiceCommand(string rawText, string aiMaterial = "")
    {
        string command = rawText.ToLower().Trim('.', ',', '!', '?', ' ');

        Debug.Log($"[AppManager] Understood: {command} | AI Material: {aiMaterial.ToUpper()}");

        // Detect the INTENT of the sentence
        bool wantsToSpawn = IsFuzzyMatch(command, "spawn") || IsFuzzyMatch(command, "create") || IsFuzzyMatch(command, "make");
        bool wantsToSearch = IsFuzzyMatch(command, "search") || IsFuzzyMatch(command, "what is") || IsFuzzyMatch(command, "who is");

        if (wantsToSpawn)
        {
            // Clean up the sentence to isolate the noun
            // e.g., "spawn a chair" becomes "chair"
            string targetShape = command
                .Replace("spawn", "")
                .Replace("create", "")
                .Replace("make", "")
                .Replace(" a ", " ")
                .Replace(" an ", " ")
                .Trim();

            if (!string.IsNullOrEmpty(targetShape))
            {
                // Send the isolated noun to the gatekeeper
                spawner.SpawnShape(targetShape);
            }
            else
            {
                Debug.Log("I heard 'spawn', but I don't know WHAT shape to spawn!");
            }
        }
        else if (wantsToSearch)
        {
            Debug.Log("Routing to Wikipedia API... (Coming next!)");
            string searchTerm = command
                .Replace("search wikipedia for", "")
                .Replace("search for", "")
                .Replace("search", "")
                .Replace("what is", "")
                .Replace("who is", "")
                .Trim();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                wikiApi.SearchWikipedia(searchTerm, (summaryText) =>
                {
                    Debug.Log($"[WIKIPEDIA SUMMARY] {summaryText}");
                    avatarController.ProcessAndSpeak(summaryText);
                });
            }
            else
            {
                Debug.Log("I heard 'search', but I don't know WHAT to search for!");
            }
        }
        else
        {
            // ── Noun-only input (no instruction keyword) ─────────────────────────
            // e.g. "cat", "Eiffel Tower", "black hole"
            // → spawn the 3D model AND have the avatar explain it via Wikipedia.
            string noun = command.Trim();
            bool looksLikeNoun = !string.IsNullOrEmpty(noun)
                && noun.Split(' ').Length <= 4   // short phrase, not a full sentence
                && !noun.Contains("?");

            if (looksLikeNoun)
            {
                Debug.Log($"[AppManager] Noun-only input: '{noun}' → spawning model + Wikipedia summary");
                spawner.SpawnShape(noun);
                avatarController.AnswerQuestion(noun);
            }
            else
            {
                Debug.Log("Command not recognized by the AppManager.");
            }
        }
    }

    // --- Keep your existing IsFuzzyMatch and LevenshteinDistance functions down here! ---
    private bool IsFuzzyMatch(string whisperStr, string targetStr, int maxStep = -1)
    {
        if (whisperStr.Contains(targetStr)) return true;

        string[] spokenWords = whisperStr.Split(' ', ',', '.', '!', '?');

        int allowedTypos = maxStep;
        if (allowedTypos == -1)
        {
            if (targetStr.Length <= 4) allowedTypos = 1;
            else if (targetStr.Length <= 7) allowedTypos = 2;
            else allowedTypos = 3;
        }

        foreach (string word in spokenWords)
        {
            if (Mathf.Abs(word.Length - targetStr.Length) > allowedTypos) continue;

            int distance = LevenshteinDistance(word, targetStr);

            if (distance <= allowedTypos)
            {
                Debug.Log($"[FUZZY MATCH] Corrected typo '{word}' into '{targetStr}'");
                return true;
            }
        }
        return false;
    }

    private int LevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source)) return string.IsNullOrEmpty(target) ? 0 : target.Length;
        if (string.IsNullOrEmpty(target)) return source.Length;
        int[,] matrix = new int[source.Length + 1, target.Length + 1];
        for (int i = 0; i <= source.Length; i++) matrix[i, 0] = i;
        for (int j = 0; j <= target.Length; j++) matrix[0, j] = j;
        for (int i = 1; i <= source.Length; i++)
        {
            for (int j = 1; j <= target.Length; j++)
            {
                int cost = (source[i - 1] == target[j - 1]) ? 0 : 1;
                matrix[i, j] = Mathf.Min(
                    Mathf.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }
        return matrix[source.Length, target.Length];
    }
}