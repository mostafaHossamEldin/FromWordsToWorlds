using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FuzzySharp;
using UnityEditor;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.Networking;

public struct AmbientTexture
{
    public string name;
    public string uri;
    public string fileType;
}

public class TextureSelector
{
    private static readonly string savePath = "Assets/Scripts/Textures";

    public static Material defaultMaterial { get { return new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Scripts/Textures/Default.mat")); } }

    private List<AmbientTexture> textureNames = new List<AmbientTexture>();
    private HttpClient httpClient = new HttpClient();

    public TextureSelector()
    {
        LoadCSV();
    }

    private void LoadCSV()
    {
        string filePath = "Assets/Scripts/ambientCG_Textures.csv";
        if (File.Exists(filePath))
        {
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines.Skip(1))
            {
                var columns = line.Split(',');
                if (columns.Length > 0)
                {
                    string name = new string(columns[0].Where(c => Char.IsLetter(c)).ToArray());
                    if (columns[1].ToLower().Contains("1k") && textureNames.Where(ambientTexture => ambientTexture.name == name).Count() == 0)
                        textureNames.Add(new AmbientTexture { name = name, fileType = columns[2], uri = columns[4] });
                }
            }
        }
        else
        {
            Debug.LogError("CSV file not found at: " + filePath);
        }
    }

    private AmbientTexture SearchTexture(string query)
    {
        AmbientTexture result = textureNames
            .Select(texture => new { texture = texture, Score = Fuzz.Ratio(query, texture.name) })
            .OrderByDescending(result => result.Score)
            .Take(1)
            .Select(result => result.texture )
            .ToList()[0];

        return result;
    }

    private async Task<bool> DownloadTexture(AmbientTexture texture)
    {
        if (Directory.Exists($"{savePath}/{texture.name}")) { return true; }

        UnityWebRequest uwr = UnityWebRequest.Get(texture.uri);

        uwr.SendWebRequest();

        while (!uwr.isDone)
        {
            await Task.Yield();
        }

        if (uwr.result != UnityWebRequest.Result.Success)
        {
            throw new System.Exception($"Failed to download file: {uwr.error} --- {texture.uri}");
        }

        SaveTexture(texture.name, uwr.downloadHandler.data, texture.fileType);
        return true;
    }

    private async void SaveTexture(string name, byte[] data, string fileType)
    {
        Directory.CreateDirectory($"{savePath}/{name}");
        string newSavePath = $"{savePath}/{name}";
        File.WriteAllBytes($"{newSavePath}/{name}.{fileType}", data);

        if (fileType == "zip")
        {
            using (ZipArchive archive = ZipFile.OpenRead($"{newSavePath}/{name}.{fileType}"))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string filePath = Path.Combine(newSavePath, entry.Name);
                    entry.ExtractToFile(filePath, overwrite: true);
                }
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private Material CreateZipMaterial(string path, string fileType)
    {
        string extractedFolder = $"{savePath}/{Path.GetFileNameWithoutExtension(path)}";
        string[] files = Directory.GetFiles(extractedFolder);

        Material material = new Material(Shader.Find("Standard"));

        foreach (string file in files)
        {
            string fileName = file.Replace("/", "\\");
            if (Path.GetExtension(fileName).ToLower() == $".{fileType}")
            {
                Texture2D texture = (Texture2D)AssetDatabase.LoadAssetAtPath(fileName, typeof(Texture2D));
                texture.wrapMode = TextureWrapMode.Repeat;
                if (fileName.ToLower().Contains("occlusion"))
                {
                    material.SetTexture("_OcclusionMap", texture);
                }
                else if (fileName.ToLower().Contains("normal"))
                {
                    TextureImporter importer = AssetImporter.GetAtPath(fileName) as TextureImporter;
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();

                    material.SetTexture("_BumpMap", texture);
                }
                else if (fileName.ToLower().Contains("roughness"))
                {
                    material.SetTexture("_MetallicGlossMap", texture);
                }
                else if (fileName.ToLower().Contains("displacement"))
                {
                    material.SetTexture("_ParallaxMap", texture);
                }
                else if (fileName.ToLower().Contains("specular"))
                {
                    material.SetTexture("_SpecGlossMap", texture);
                }
                else if (fileName.ToLower().Contains("color"))
                {
                    material.SetTexture("_MainTex", texture);
                }
            }
        }
        
        return material;
    }

    private void CreateMaterial(AmbientTexture ambientTexture)
    {
        string directory = $"{savePath}/{ambientTexture.name}";
        if (File.Exists($"{savePath}/{ambientTexture.name}.mat"))
            return;

        string path = $"{directory}/{ambientTexture.name}.{ambientTexture.fileType}";

        Material material = new Material(Shader.Find("Standard"));

        if (ambientTexture.fileType.ToLower() == "zip")
        {
            material = CreateZipMaterial(path, ambientTexture.uri.Split('.')[1].Split('-').Last().ToLower());
        }
        else
        {
            Texture2D texture = (Texture2D) AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D));
            material.SetTexture("_MainTex", texture);
        }

        AssetDatabase.CreateAsset(material, $"{savePath}/{ambientTexture.name}.mat");
        AssetDatabase.SaveAssets();
    }

    private Material LoadMaterial(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    public async Task<Material> GetMaterial(string query)
    {
        AmbientTexture texture = SearchTexture(query);
        string path = $"{savePath}/{texture.name}.mat";

        await DownloadTexture(texture);

        CreateMaterial(texture);

        Material material = LoadMaterial(path);

        return material;
    }
}
