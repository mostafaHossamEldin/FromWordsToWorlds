using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Networking;
using UnityGLTF;
using UnityGLTF.Loader;
using UnityGLTF.Plugins;
public struct AssetDetails
{
    public string uid;
    public string name;
    public string description;
    public string tags;
    public string categories;
    public string userUsername;
    public string license;
}

public abstract class ModelRetrivier
{
    protected static readonly string savePath = "Assets/Scripts/SketchFabModels";
    public HttpClient httpClient;
    public string webURL;
    public string accessToken;
    
    public ModelRetrivier(string webURL)
    {
        httpClient = new HttpClient();
        this.webURL = webURL;
    }

    public abstract Task<List<AssetDetails>> SearchSimilarAssets(string name);

    public abstract Task<string> GetAssetDownloadURL(string modelId, string fileType);

    public abstract Task<(byte[], string fileType)> DownloadAsset(string url);

    public async Task<GameObject> ImportAsset(string filePath)
    {
        if (filePath.Contains(".glb"))
        {
            return await ImportGLBModelFromFile(filePath);
        }
        else
        {
            throw new System.Exception("File type not supported.");
        }
    }

    public async Task<GameObject> ImportGLBModelFromFile(string filePath)
    {
        if(!File.Exists(filePath))
            throw new System.Exception("File does not exist.");
        try
        {
            GameObject gameObject = new GameObject();
            string newFilePath = $"../../{filePath}";

            gameObject.name = Path.GetFileNameWithoutExtension(filePath).Split("=-=-=")[0];
            //await gltfComponent.Load();


            var importOptions = new ImportOptions
            {
                AsyncCoroutineHelper = gameObject.GetComponent<AsyncCoroutineHelper>() ?? gameObject.AddComponent<AsyncCoroutineHelper>()
            };

            var settings = GLTFSettings.GetOrCreateSettings();

            GLTFSceneImporter sceneImporter = null;
            try
            {
                ImporterFactory Factory = ScriptableObject.CreateInstance<DefaultImporterFactory>();

                string fullPath = Path.Combine(Application.streamingAssetsPath, newFilePath.TrimStart(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }));

                string dir = URIHelper.GetDirectoryName(fullPath);
                importOptions.DataLoader = new UnityWebRequestLoader(dir);
                importOptions.DeduplicateResources = DeduplicateOptions.None;
                sceneImporter = Factory.CreateSceneImporter(
                    Path.GetFileName(fullPath),
                    importOptions
                );

                sceneImporter.SceneParent = gameObject.transform;

                // for logging progress
                await sceneImporter.LoadSceneAsync(
                // ,progress: new Progress<ImportProgress>(
                // 	p =>
                // 	{
                // 		Debug.Log("Progress: " + p);
                // 	})
                );
            }
            finally
            {
                if (importOptions.DataLoader != null)
                {
                    sceneImporter?.Dispose();
                    sceneImporter = null;
                    importOptions.DataLoader = null;
                }
            }

            return gameObject;
        }
        catch (System.Exception)
        {
            throw new System.Exception("Failed to load model from file.");
        }
    }

    public void SaveAsset(string name, byte[] data, string fileType)
    {
        File.WriteAllBytes($"{savePath}/{name}.{fileType}", data);
        Debug.Log("Asset saved to " + $"{savePath}/{name}.{fileType}");
    }

    public abstract Task<GameObject> GetAsset(string name, Vector3 size);

    public abstract Task<GameObject> GetAsset(AssetDetails asset);

    public async Task<String[]> SetAssetMetaData(string filePath, AssetDetails assetDetails)
    {
        UnityEngine.Object assetFile = AssetDatabase.LoadAssetAtPath(filePath, typeof(UnityEngine.Object));
        string[] metadata = AssetDatabase.GetLabels(assetFile);
        // metaData = {assetName, authorUsername, license, size};

        if (
            metadata.Length != 4 ||
            metadata.Where(s => s.Split(":")[0].Equals("name")).Count() == 0 ||
            metadata.Where(s => s.Split(":")[0].Equals("author")).Count() == 0 ||
            metadata.Where(s => s.Split(":")[0].Equals("license")).Count() == 0 ||
            metadata.Where(s => s.Split(":")[0].Equals("size")).Count() == 0
            )
        {
            GameObject gameObject = await ImportAsset(filePath); // ImportAsset()

            Vector3 assetSize = GeneralScript.GetAllBounds(gameObject).size;

            GameObject.Destroy(gameObject);

            metadata = new string[] { $"name:{assetDetails.name}", $"author:{assetDetails.userUsername}", $"license:{assetDetails.license}", $"size:({assetSize.x},{assetSize.y},{assetSize.z})" };
            AssetDatabase.SetLabels(assetFile, metadata);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        return metadata;
    }

    public async Task<string[]> GetAssetMetaData(AssetDetails asset)
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

        return metadata;
    }
}
