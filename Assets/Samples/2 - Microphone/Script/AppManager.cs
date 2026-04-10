using UnityEngine;

public class AppManager : MonoBehaviour
{
    public static AppManager Instance;

    [Header("Connected Systems")]
    public ObjectSpawner spawner;
    public WikiAPI wikiApi;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void ProcessVoiceCommand(string rawText)
    {
        // Here we will process the raw text from the transcription and determine what to do with it.
        // For example, if the user says "Spawn a red cube", we want to tell the spawner to create a red cube in the scene.
        // If the user says "Search Wikipedia for cats", we want to tell the wikiApi to search for cats and return some information.
        string command = rawText.ToLower().Trim('.', ',', '!', '?', ' ');
        Debug.Log("AppManager understood: " + command);
        //Keyword spotted
        bool wantsToSpawn = IsFuzzyMatch(command,"spawn") || IsFuzzyMatch(command,"create") || IsFuzzyMatch(command, "make");
        bool wantsToSearch = IsFuzzyMatch(command, "search") || IsFuzzyMatch(command, "what is") || IsFuzzyMatch(command, "who is");


        

        if (wantsToSpawn)
        {
            if (IsFuzzyMatch(command, "cube") || IsFuzzyMatch(command, "box"))
            {
                spawner.SpawnShape("cube");
            }
            else if (IsFuzzyMatch(command, "sphere") || IsFuzzyMatch(command, "ball"))
            {
                spawner.SpawnShape("sphere");
            }
            else if (IsFuzzyMatch(command, "cylinder"))
            {
                spawner.SpawnShape("cylinder");
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
                    // This code runs a split second later when the internet request finishes!
                    Debug.Log($"[WIKIPEDIA SUMMARY] {summaryText}");
                });
            }
            else
            {
                Debug.Log("I heard 'search', but I don't know WHAT to search for!");
            }
        }
        else
        {
            // Fallback: If  didn't say "spawn", but DID say a shape name, 
            if (command.Contains("cube")) spawner.SpawnShape("cube");
            else Debug.Log("Command not recognized by the AppManager.");
        }
    }

    private bool IsFuzzyMatch(string whisperStr, string targetStr, int maxStep = -1)
    {
  
       if (whisperStr.Contains(targetStr)) return true;

        string[] spokenWords = whisperStr.Split(' ', ',', '.', '!', '?');

    
        int allowedTypos = maxStep;
        if (allowedTypos == -1) // If we didn't manually set a number
        {
            if (targetStr.Length <= 4) allowedTypos = 1;      // cube, box
            else if (targetStr.Length <= 7) allowedTypos = 2; //  sphere, create
            else allowedTypos = 3;                            //  cylinder
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

