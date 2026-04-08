using UnityEngine;

public class AppManager : MonoBehaviour
{
    public static AppManager Instance;

    [Header("Connected Systems")]
    public ObjectSpawner spawner;
    // public WikiAPI wikiApi; // We will uncomment this when we build the next step!

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
        bool wantsToSpawn = command.Contains("spawn") || command.Contains("create") || command.Contains("make");
        bool wantsToSearch = command.Contains("search") || command.Contains("what is") || command.Contains("who is");

        if (wantsToSpawn)
        {
            if (command.Contains("cube") || command.Contains("box"))
            {
                spawner.SpawnShape("cube");
            }
            else if (command.Contains("sphere") || command.Contains("ball"))
            {
                spawner.SpawnShape("sphere");
            }
            else if (command.Contains("cylinder"))
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
        }
        else
        {
            // Fallback: If  didn't say "spawn", but DID say a shape name, 
            if (command.Contains("cube")) spawner.SpawnShape("cube");
            else Debug.Log("Command not recognized by the AppManager.");
        }
    }

}

