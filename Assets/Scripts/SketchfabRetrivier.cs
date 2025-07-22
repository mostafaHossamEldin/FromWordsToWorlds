using Newtonsoft.Json.Linq;
using System;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling.Memory.Experimental;

public class SketchfabRetrivier : ModelRetrivier
{
    private static readonly string downloadingFileType = "glb";
    public SketchfabRetrivier(string token) : base("https://api.sketchfab.com")
    {
        httpClient.DefaultRequestHeaders.Add("Authorization", "Token " + token);
        //httpClient.DefaultRequestHeaders.Add("Authorization", "Token " + "b8760a7ff0134de9945f73696b8b5d67");
    }

    public override async Task<List<AssetDetails>> SearchSimilarAssets(string name)
    {
        name = name.Replace("_", " ");
        string endpoint = $"{webURL}/v3/search?q={name}";
        HttpResponseMessage response = await httpClient.GetAsync(endpoint);
        //
        if (response.IsSuccessStatusCode)
        {
            try
            {
                string stringResponse = await response.Content.ReadAsStringAsync();
                JObject jsonResponse = JObject.Parse(stringResponse);

                JArray models = (JArray)jsonResponse["results"]["models"];
                List<AssetDetails> modelsUIDs = new List<AssetDetails>();
                for (int i = 0; i < models.Count && modelsUIDs.Count < 5; i++)
                {
                    if (models[i]["isDownloadable"].ToString().Equals("True"))
                    {
                        modelsUIDs.Add(new AssetDetails {
                            uid = models[i]["uid"].ToString(),
                            name = models[i]["name"].ToString(),
                            description = models[i]["description"].ToString(),
                            tags = ((JArray)models[i]["tags"]).Select(t => t["name"].ToString()).Aggregate("", (str, tag) => $"{str}{tag}, "),
                            categories = ((JArray)models[i]["categories"]).Select(t => t["name"].ToString()).Aggregate("", (str, tag) => $"{str}{tag}, "),
                            userUsername = models[i]["user"]["username"].ToString(),
                            license = models[i]["license"]["label"].ToString()});
                    }
                }
                if (modelsUIDs.Count > 1)
                    return modelsUIDs;
                else
                {
                    if (name.Split(" ").Count() > 1)
                    {
                        List<AssetDetails> similarAssets = await SearchSimilarAssets(string.Join(" ", name.Split(" ").Take(name.Split(" ").Count() - 1)));
                        if (similarAssets.Count > 1)
                            return similarAssets;
                        else
                            return await SearchSimilarAssets(string.Join(" ", name.Split(" ").Skip(1)));
                    }
                    else
                        return new List<AssetDetails>();
                }
            }
            catch (System.Exception ex)
            {
                throw new System.Exception("Sketchfab: An error occurred while parsing the response JSON.", ex);
            }
        }
        else
            throw new System.Exception("Sketchfab: Response status code is not success: " + response.ToString());
    }

    public override async Task<string> GetAssetDownloadURL(string modelId, string fileType)
    {
        await Task.Delay(2000);
        string endpoint = $"{webURL}/v3/models/{modelId}/download";
        HttpResponseMessage response = await httpClient.GetAsync(endpoint);

        if (response.IsSuccessStatusCode)
        {
            try
            {
                string stringResponse = await response.Content.ReadAsStringAsync();
                JObject jsonResponse = JObject.Parse(stringResponse);
                Debug.Log(jsonResponse);
                string modelURL = jsonResponse[fileType]["url"].ToString();

                return modelURL;
            }
            catch (System.Exception ex)
            {
                throw new System.Exception("Sketchfab Download URL: An error occurred while parsing the response JSON.", ex);
            }
        }
        else
        {
            throw new System.Exception("Sketchfab Download URL: Response status code is not success, is user authenticated?: " + response);
        }
    }
    
    public static string GetFileExtensionFromUrl(string url)
    {
        url = url.Split('?')[0];
        url = url.Split('/').Last();
        return url.Contains('.') ? url.Substring(url.LastIndexOf('.') + 1) : "";
    }

    public override async Task<(byte[], string fileType)> DownloadAsset(string uid)
    {
        string url = await GetAssetDownloadURL(uid, downloadingFileType);

        UnityWebRequest uwr = UnityWebRequest.Get(url);

        uwr.SendWebRequest();

        while (!uwr.isDone)
        {
            await Task.Yield();
        }

        if (uwr.result != UnityWebRequest.Result.Success)
        {
            throw new System.Exception($"Failed to download file: {uwr.error} --- {url}");
        }

        Debug.Log($"Successfully downloaded file");
        return (uwr.downloadHandler.data, GetFileExtensionFromUrl(url));
    }

    public override async Task<GameObject> GetAsset(string name, Vector3 size)
    {
        string fileType = "glb";

        List<AssetDetails> similarAssets = await SearchSimilarAssets(name); //SearchSimilarAssets(name)
        // Item1 is the UID of the model
        // Item2 is the name of the model
        // Item3 is the username of the model author
        // Item4 is the license of the model

        if(similarAssets.Count == 0)
        {
            Debug.LogWarning("No similar assets found for " + name);
            return null;
        }
        Debug.Log("Similar Assets: " + similarAssets.Count);

        Vector3 sizeRatio = size / size.magnitude;

        UnityEngine.Object choosenAsset = null;
        float choosenAssetSizeRatioAbsDifference = float.MaxValue;
        foreach (AssetDetails asset in similarAssets) // ForEach(Asset)
        {
            string modelName = $"Sketchfab {asset.uid}";
            string filePath = $"{savePath}/{modelName}.{fileType}";

            Vector3 assetSizeRatio = Vector3.zero;
            UnityEngine.Object assetFile;
            if (!File.Exists(filePath)) // Asset In Storage
            { // No
                (byte[] modelData, string fileType1) = await DownloadAsset(asset.uid); // DownloadAsset()
                SaveAsset(modelName, modelData, fileType1);
            }

            assetFile = AssetDatabase.LoadAssetAtPath(filePath, typeof(UnityEngine.Object));

            string[] metadata = AssetDatabase.GetLabels(assetFile);
            // metaData = {assetName, authorUsername, license, sizeRatio, size};

            if (metadata.Length != 4 || metadata.Where(s => s.Split(":")[0].Equals("size")).Count() == 0)
            {
                GameObject gameObject = await ImportAsset(filePath); // ImportAsset()

                Vector3 assetSize = GeneralScript.GetAllBounds(gameObject).size;

                GameObject.Destroy(gameObject);

                assetSizeRatio = assetSize / assetSize.magnitude; // CalculateSizeRatio()
                metadata = new string[] { $"name:{asset.name}", $"author:{asset.userUsername}", $"license:{asset.license}", $"size:({assetSize.x},{assetSize.y},{assetSize.z})" };
                AssetDatabase.SetLabels(assetFile, metadata);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return gameObject;
            }
            else
            {
                Vector3 assetSize = GeneralScript.StringToVector3(metadata.Where(s => s.Contains("size")).ToArray()[0].Split(":")[1]);
                assetSizeRatio = assetSize / assetSize.magnitude;
            }

            float assetSizeRatioMSRDifference = (float)Math.Sqrt(
                Math.Pow(sizeRatio.x - assetSizeRatio.x, 2) +
                Math.Pow(sizeRatio.y - assetSizeRatio.y, 2) +
                Math.Pow(sizeRatio.z - assetSizeRatio.z, 2));
            if (assetSizeRatioMSRDifference < choosenAssetSizeRatioAbsDifference)
            {
                choosenAsset = assetFile;
                choosenAssetSizeRatioAbsDifference = assetSizeRatioMSRDifference;
            }

        }

        GameObject gameObject1 = await ImportAsset(AssetDatabase.GetAssetPath(choosenAsset));
        return gameObject1;
    }

    public override async Task<GameObject> GetAsset(AssetDetails asset)
    {
        string fileType = "glb";

        string modelName = $"Sketchfab {asset.uid}";
        string filePath = $"{savePath}/{modelName}.{fileType}";

        Vector3 assetSizeRatio = Vector3.zero;
        if (!File.Exists(filePath)) // Asset In Storage
        { // No
            (byte[] modelData, string fileType1) = await DownloadAsset(asset.uid); // DownloadAsset()
            SaveAsset(modelName, modelData, fileType1);
        }
        string[] metadata = await SetAssetMetaData(filePath, asset);

        return await ImportAsset(filePath);
    }

    public async Task<Vector3> GetRelativeAssetSize(AssetDetails asset, Vector3 referenceSize)
    {
        string[] metadata = await GetAssetMetaData(asset);
        Vector3 size = GeneralScript.StringToVector3(metadata.Where(s => s.Contains("size:")).ToArray()[0].Split(":")[1]);

        return size;
    }

    public async Task<Vector3> GetAssetSizeRatio(AssetDetails asset)
    {
        string fileType = "glb";

        string modelName = $"Sketchfab {asset.uid}";
        string filePath = $"{savePath}/{modelName}.{fileType}";

        Vector3 assetSizeRatio = Vector3.zero;
        UnityEngine.Object assetFile;

        if (!File.Exists(filePath)) // Asset In Storage
        { // No
            (byte[] modelData, string fileType1) = await DownloadAsset(asset.uid); // DownloadAsset()
            SaveAsset(modelName, modelData, fileType1);
        }

        assetFile = AssetDatabase.LoadAssetAtPath(filePath, typeof(UnityEngine.Object));

        string[] metadata = AssetDatabase.GetLabels(assetFile);

        if (metadata.Length != 4)
        {
            GameObject gameObject = await ImportAsset(filePath); // ImportAsset()

            Vector3 assetSize = GeneralScript.GetAllBounds(gameObject).size;

            GameObject.Destroy(gameObject);

            assetSizeRatio = assetSize / assetSize.magnitude; // CalculateSizeRatio()
            metadata = new string[] { $"name:{asset.name}", $"author:{asset.userUsername}", $"license:{asset.license}", $"sizeRatio:({assetSizeRatio.x},{assetSizeRatio.y},{assetSizeRatio.z})" };
            AssetDatabase.SetLabels(assetFile, metadata);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return assetSizeRatio;
        }

        return GeneralScript.StringToVector3(metadata.Where(s => s.Contains("sizeRatio")).ToArray()[0].Split(":")[1]);
    }
}