using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

public abstract class LLMAPI
{
    protected string apiKey;
    protected string apiURL;

    protected HttpClient httpClient = new HttpClient();

    public LLMAPI(string apiKey, string apiURL)
    {
        this.apiKey = apiKey;
        this.apiURL = apiURL;
    }

    public abstract Task<string> SendPrompt(string message);
}
