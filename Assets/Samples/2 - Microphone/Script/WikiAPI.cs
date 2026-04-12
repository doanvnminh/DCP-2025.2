using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class WikiAPI : MonoBehaviour
{
    //  The Search Engine (Finds the exact page title)
    private const string SEARCH_URL = "https://en.wikipedia.org/w/rest.php/v1/search/page?q=";

    //  The Summary Database (Grabs the paragraph)
    private const string SUMMARY_URL = "https://en.wikipedia.org/api/rest_v1/page/summary/";

    public void SearchWikipedia(string searchTerm, Action<string> onComplete)
    {
        StartCoroutine(GetWikiData(searchTerm, onComplete));
    }

    private IEnumerator GetWikiData(string searchTerm, Action<string> onComplete)
    {
        string query = UnityWebRequest.EscapeURL(searchTerm);
        string searchRequestUrl = SEARCH_URL + query + "&limit=1";

        Debug.Log($"[WikiAPI] 1. Searching for best match for: '{searchTerm}'...");
        string exactPageKey = "";


        //Find the exact page key for search term
        using (UnityWebRequest searchReq = UnityWebRequest.Get(searchRequestUrl))
        {
            yield return searchReq.SendWebRequest();

            if (searchReq.result == UnityWebRequest.Result.Success)
            {
                WikiSearchResponse searchResult = JsonUtility.FromJson<WikiSearchResponse>(searchReq.downloadHandler.text);

                // If we found at least one result, grab its exact URL key!
                if (searchResult.pages != null && searchResult.pages.Length > 0)
                {
                    exactPageKey = searchResult.pages[0].key;
                }
            }
        }

        // If Wikipedia's search engine found absolutely nothing, stop here.
        if (string.IsNullOrEmpty(exactPageKey))
        {
            onComplete?.Invoke($"Sorry, I couldn't find any articles about {searchTerm}.");
            yield break; // Exit the coroutine
        }


        //Get the page link of wiki
        string fullWikipediaUrl = "https://en.wikipedia.org/wiki/" + exactPageKey;
        Debug.Log($"[WikiAPI] Clickable Link: {fullWikipediaUrl}");


        //Gete summary 
        Debug.Log($"[WikiAPI] 2. Found exact page: '{exactPageKey}'. Downloading summary...");
        string summaryRequestUrl = SUMMARY_URL + exactPageKey;

        using (UnityWebRequest summaryReq = UnityWebRequest.Get(summaryRequestUrl))
        {
            yield return summaryReq.SendWebRequest();

            if (summaryReq.result != UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke("Sorry, I found the page but couldn't load the text.");
            }
            else
            {
                WikiSummaryResponse result = JsonUtility.FromJson<WikiSummaryResponse>(summaryReq.downloadHandler.text);

                if (result.type == "disambiguation")
                {
                    onComplete?.Invoke($"There are many different pages for '{searchTerm}'. Could you be more specific?");
                }
                else
                {
                    onComplete?.Invoke(result.extract);
                }
            }
        }
    }
}



//  (The Search)
[System.Serializable]
public class WikiSearchResponse
{
    public WikiSearchPage[] pages; 
}

[System.Serializable]
public class WikiSearchPage
{
    public string key; 
}

[System.Serializable]
public class WikiSummaryResponse
{
    public string type;
    public string extract;
}