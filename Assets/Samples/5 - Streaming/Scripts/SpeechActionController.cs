using UnityEngine;

public class SpeechActionController : MonoBehaviour
{
    public GameObject modelPrefab;

    public void HandleSpeech(string text)
    {
        text = text.ToLower();

        if (text.Contains("car") || text.Contains("object"))
        {
            GameObject obj= Instantiate(modelPrefab, new Vector3(-4, 0, 4), Quaternion.identity);
            obj.AddComponent<Spin>();
        }
    }
}