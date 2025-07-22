using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class GeminiAPI : LLMAPI
{
    [Header("Settings")]
    private static int maxAPITries = 5;

    public GeminiAPI(string apiKey) : base(apiKey, "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key=")
    {

    }

    public override async Task<string> SendPrompt(string message)
    {
        var requestBody = new
        {
            contents =
                new
                {
                    parts =
                        new
                        {
                            text = message
                        }
                }
        };
        Debug.Log("Request Body: " + JsonConvert.SerializeObject(requestBody));

        var jsonBody = JsonConvert.SerializeObject(requestBody);

        for (int i = 0; i < maxAPITries; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, apiURL + apiKey)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            Debug.Log("Sending Google API Request. Please Wait...");
            HttpResponseMessage response = await httpClient.SendAsync(request);

            string responseString = await response.Content.ReadAsStringAsync();
            JObject responseJson = JObject.Parse(responseString);

            Debug.Log("Bot Response: " + responseJson);
            try
            {
                string responseContent = (string)((JArray)responseJson["candidates"])[0]["content"]["parts"][0]["text"];//GoogleAPI
                return responseContent;
            }
            catch (Exception e)
            {
                Debug.Log(e);
                string errorCode = (string)responseJson["error"]["code"];
                if (errorCode == "503")
                    Debug.Log("Attempt " + i + " Failed... API Busy");
            }
        }

        throw new Exception("You reach max limit of free calls, API Busy, Or Invalid API Key");
    }
}
