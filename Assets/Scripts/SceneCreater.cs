using System;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Collections;
using Unity.VisualScripting;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using System.IO;
using UnityEditor.Rendering.LookDev;

public class SceneCreater : MonoBehaviour
{

    [Header("PlayerObjects")]
    public GameObject player;
    public Camera playerCamera;

    [Header("APIs")]
    public string geminiAPIKey;
    public string sketchFabToken;
    LLMAPI geminiAPI;
    private SketchfabRetrivier modelRetrivier;

    [Header("Platform")]
    public GameObject mainPlatform;

    [Header("Global Variables")]
    private System.Random random = new System.Random();
    private TextureSelector textureSelector = new TextureSelector();

    void Start()
    {
        geminiAPI = new GeminiAPI(geminiAPIKey);
        modelRetrivier = new SketchfabRetrivier(sketchFabToken);

        string area = "Gym";
        string language = "German";
        CreateScene(area, language);
    }

    void Update()
    {
    }
    public static string ReadTextFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                string text = File.ReadAllText(filePath);
                return text;
            }
            catch (IOException e)
            {
                Debug.LogError($"IO error reading file: {e.Message}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Unexpected error: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"File not found at path: {filePath}");
        }

        return null;
    }

    GameObject CreateRelativeObject(string? name, GameObject? parent, float x, float y, float z, float sizeX, float sizeY, float sizeZ, Color? color = null)
    {
        if (name == null)
            name = "Object";
        if (color == null)
            color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);

        GameObject newObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        newObject.transform.localScale = new Vector3(sizeX, sizeY, sizeZ);
        newObject.name = name;
        newObject.transform.position = new Vector3(x, y, z) + (parent ? parent.transform.position : Vector3.zero);
        if (parent != null)
            newObject.transform.parent = parent.transform;
        newObject.GetComponent<Renderer>().material.color = color.Value;

        return newObject;
    }

    GameObject CreatePlatform(ZoneNode zoneNode, float x, float z, float sizeX, float sizeZ, GameObject parent, float y = 0)
    {
        string name = zoneNode.name;
        string areaName = name;

        GameObject newPlatform = CreateRelativeObject(
            areaName,
            (mainPlatform),
            x,
            0.2f + y,
            z,
            sizeX,
            0.4f,
            sizeZ,
            new Color(random.Next(100, 200) / 256f, random.Next(150, 256) / 256f, random.Next(120, 200) / 256f));

        if(zoneNode.children.Count < 1)
        {
            GameObject canvasObject = new GameObject("WorldCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(10, 10);

            GameObject textObject = new GameObject("FloorText");
            textObject.transform.parent = canvasObject.transform;

            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = zoneNode.translatedName + $"\n({zoneNode.name})";
            text.fontSize = 30;
            text.alignment = TextAlignment.Center;
            text.anchor = TextAnchor.MiddleCenter; // Center the text in its own bounds

            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.color = Color.black;
            text.characterSize = 0.1f;

            canvasObject.transform.position = newPlatform.transform.position + new Vector3(0, newPlatform.transform.localScale.y/2f + 0.1f, 0);
            canvasObject.transform.rotation = Quaternion.Euler(90, 0, 0);
        }

        Material material = new Material(zoneNode.floorMaterial);
        float matWidthRatio = material.mainTexture.width / material.mainTexture.height;
        if (material != null)
            material.SetTextureScale("_MainTex", new Vector2(sizeX, sizeZ * matWidthRatio));

        newPlatform.GetComponent<MeshRenderer>().materials = new Material[] { material, material, material, material, material, material };

        //if (zoneNode.height > 1f)
        //{
        //    Vector3 pos = newPlatform.transform.position;
        //    Vector3 size = GeneralScript.GetBounds(newPlatform).size;
        //    GameObject newPlatformCeiling = CreateRelativeObject(
        //        zoneNode.name + " Ceiling",
        //        (mainPlatform),
        //        x,
        //        0.05f + y + zoneNode.height,
        //        z,
        //        sizeX,
        //        0.1f,
        //        sizeZ,
        //        new Color(random.Next(100, 200) / 256f, random.Next(150, 256) / 256f, random.Next(120, 200) / 256f));

        //    Material materialCeiling = new Material(TextureSelector.defaultMaterial);
        //    float matWidthRatioCeiling = materialCeiling.mainTexture.width / materialCeiling.mainTexture.height;
        //    if (materialCeiling != null)
        //        materialCeiling.SetTextureScale("_MainTex", new Vector2(size.x, size.z * matWidthRatioCeiling));

        //    newPlatformCeiling.GetComponent<MeshRenderer>().materials = new Material[] { materialCeiling, materialCeiling, materialCeiling, materialCeiling, materialCeiling, materialCeiling };

        //    newPlatformCeiling.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        //}

        zoneNode.platform = newPlatform;

        //if (layoutName != null && layoutName != "")
        //    createLayout(newPlatform, layoutName);

        return newPlatform;
    }

    private List<(ZoneNode, float, float, float, float)> GetZonesBorders(ZoneNode root){
        List<(ZoneNode, float, float, float, float)> zonesBorders = new List<(ZoneNode, float, float, float, float)>(); // ZoneName, right, left, up, down

        Stack<ZoneNode> stack = new Stack<ZoneNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            ZoneNode node = stack.Pop();
            if (node != null)
            {
                if (node.children.Count == 0)
                {
                    float x = node.platform.transform.position.x;
                    float z = node.platform.transform.position.z;
                    float sizeX = node.platform.transform.localScale.x;
                    float sizeZ = node.platform.transform.localScale.z;
                    zonesBorders.Add((node,
                        (x + sizeX / 2f),
                        (x - sizeX / 2f),
                        (z + sizeZ / 2f),
                        (z - sizeZ / 2f)));
                }
                foreach (ZoneNode child in node.children.Values)
                {
                    stack.Push(child);
                }
            }
        }

        return zonesBorders;
    }

    private void SetConnectionsTypes(List<Connection> connections, Dictionary<string, object> response)
    {
        //Debug.Log(connections.Aggregate("", (prev, curr) => prev + "\n" + curr.SharedZonesToString()));
        foreach (KeyValuePair<string, object> obj in response)
        {
            string connection = obj.Key;
            string connectionZone1 = connection.Split('&')[0];
            string connectionZone2 = connection.Split('&')[1];
            string connnectionType = ((string)obj.Value).ToLower();
            ConnectionType type = (connnectionType.Equals("open") ? ConnectionType.Open : connnectionType.Equals("wall") ? ConnectionType.Wall : ConnectionType.Door);
            connections.Where(c => c.SharedZonesToString().Contains(connectionZone1) && c.SharedZonesToString().Contains(connectionZone2)).ToArray()[0].connectionType = type;
        }
    }

    private List<Connection> GetZonesAdjacentConnections(ZoneNode root)
    {
        List<(ZoneNode, float, float, float, float)> zonesBorders = GetZonesBorders(root);
        List<Connection> edges = new List<Connection>();
        for (int i = 0; i < zonesBorders.Count; i++)
        {
            (ZoneNode zone, float right, float left, float up, float down) = zonesBorders[i];
            //Debug.Log(zone.name + " Searching...");
            bool hasUp = false, hasDown = false, hasLeft = false, hasRight = false;
            for (int j = 0; j < zonesBorders.Count; j++)
            {
                if (i == j) continue;
                //Debug.Log("Comparing with: " + zonesBorders[j].Item1.name);
                (ZoneNode otherZone, float otherRight, float otherLeft, float otherUp, float otherDown) = zonesBorders[j];
                if (edges.Exists(wall => wall.SharedZones.Contains(zone) && wall.SharedZones.Contains(otherZone))) // if wall already generated
                {
                    if (Math.Abs(right - otherLeft) < 0.1f) hasRight = true;
                    if (Math.Abs(left - otherRight) < 0.1f) hasLeft = true;
                    if (Math.Abs(up - otherDown) < 0.1f) hasUp = true;
                    if (Math.Abs(down - otherUp) < 0.1f) hasDown = true;
                    continue;
                }
                if (Math.Abs(right - otherLeft) < 0.1f)
                {
                    if (Math.Min(up, otherUp) - Math.Max(down, otherDown) > 0)
                    {
                        edges.Add(new Connection(
                            (right + otherLeft) / 2f,
                            (Math.Min(up, otherUp) + Math.Max(down, otherDown)) / 2f,
                            Math.Min(up, otherUp) - Math.Max(down, otherDown),
                            Math.Max(zone.height, otherZone.height),
                            orientation: Orientation.Vertical,
                            new List<ZoneNode>() { zone, otherZone }));
                        hasRight = true;
                        zone.neighbors.Add(otherZone.name, otherZone);
                        otherZone.neighbors.Add(zone.name, zone);
                        zone.connections.Add(edges[edges.Count - 1]);
                        otherZone.connections.Add(edges[edges.Count - 1]);
                    }
                }
                if (Math.Abs(left - otherRight) < 0.1f)
                {
                    if (Math.Min(up, otherUp) - Math.Max(down, otherDown) > 0)
                    {
                        edges.Add(new Connection(
                            (left + otherRight) / 2f,
                            (Math.Min(up, otherUp) + Math.Max(down, otherDown)) / 2f,
                            Math.Min(up, otherUp) - Math.Max(down, otherDown),
                            Math.Max(zone.height, otherZone.height),
                            orientation: Orientation.Vertical,
                            new List<ZoneNode>() { zone, otherZone }));
                        hasLeft = true;
                        zone.neighbors.Add(otherZone.name, otherZone);
                        otherZone.neighbors.Add(zone.name, zone);
                        zone.connections.Add(edges[edges.Count - 1]);
                        otherZone.connections.Add(edges[edges.Count - 1]);
                    }
                }
                if (Math.Abs(up - otherDown) < 0.1f)
                {
                    if (Math.Min(right, otherRight) - Math.Max(left, otherLeft) > 0)
                    {
                        edges.Add(new Connection(
                            (Math.Min(right, otherRight) + Math.Max(left, otherLeft)) / 2f,
                            (up + otherDown) / 2f,
                            Math.Min(right, otherRight) - Math.Max(left, otherLeft),
                            Math.Max(zone.height, otherZone.height),
                            orientation: Orientation.Horizontal,
                            new List<ZoneNode>() { zone, otherZone }));
                        hasUp = true;
                        zone.neighbors.Add(otherZone.name, otherZone);
                        otherZone.neighbors.Add(zone.name, zone);
                        zone.connections.Add(edges[edges.Count - 1]);
                        otherZone.connections.Add(edges[edges.Count - 1]);
                    }
                }
                if (Math.Abs(down - otherUp) < 0.1f)
                {
                    if (Math.Min(right, otherRight) - Math.Max(left, otherLeft) > 0)
                    {
                        edges.Add(new Connection(
                            (Math.Min(right, otherRight) + Math.Max(left, otherLeft)) / 2f,
                            (down + otherUp) / 2f,
                            Math.Min(right, otherRight) - Math.Max(left, otherLeft),
                            Math.Max(zone.height, otherZone.height),
                            orientation: Orientation.Horizontal,
                            new List<ZoneNode>() { zone, otherZone }));
                        hasDown = true;
                        zone.neighbors.Add(otherZone.name, otherZone);
                        otherZone.neighbors.Add(zone.name, zone);
                        zone.connections.Add(edges[edges.Count - 1]);
                        otherZone.connections.Add(edges[edges.Count - 1]);
                    }
                }
                //Debug.Log("Has Up: " + hasUp + " Has Down: " + hasDown + " Has Left: " + hasLeft + " Has Right: " + hasRight);
            }
            if (!hasUp || !hasDown || !hasLeft || !hasRight)
            {
                if (!hasUp){
                    edges.Add(new Connection((right + left) / 2f, up, right - left, zone.height, Orientation.Horizontal, new List<ZoneNode>() { zone }));
                }
                if (!hasDown){
                    edges.Add(new Connection((right + left) / 2f, down, right - left, zone.height, Orientation.Horizontal, new List<ZoneNode>() { zone }));
                }
                if (!hasLeft){
                    edges.Add(new Connection(left, (up + down) / 2f, up - down, zone.height, Orientation.Vertical, new List<ZoneNode>() { zone }));
                }
                if (!hasRight){
                    edges.Add(new Connection(right, (up + down) / 2f, up - down, zone.height, Orientation.Vertical, new List<ZoneNode>() { zone }));
                }
                zone.connections.Add(edges[edges.Count - 1]);
            }
        }

        return edges;
    }

    private async Task<Dictionary<string, object>> GetConnectionsTypes(ZoneNode root)
    {
        string edgesString = "";

        foreach (ZoneNode node in root.GetLeafNodes())
        {
            edgesString += $"{node.GetExtendedName()} ({node.size}): {{{string.Join(", ", node.neighbors.Values.Select(n => $"{n.GetExtendedName()}").ToArray())}}}\n";
        }

        string prompt = ReadTextFile(Application.dataPath + "/Prompts/ConnectionsPrompt.txt");
        prompt = prompt.Replace("{Place}", root.name);
        prompt = prompt.Replace("{Data}", edgesString);
        Debug.Log("Connections Prompt: " + prompt);
        string llmResponse = await geminiAPI.SendPrompt(prompt);


        Dictionary<string, object> response = GeneralScript.StringToDict(llmResponse);
        return response;
    }

    private async Task GenerateConnections(ZoneNode root)
    {
        List<Connection> connections = GetZonesAdjacentConnections(root);

        int tryNumber = 1;
        while (!root.AllHasConnections() && tryNumber <= 5)
        {
            Debug.Log("Getting Connections try number: " + tryNumber++);
            try
            {
                Dictionary<string, object> connectionTypes = await GetConnectionsTypes(root); // Dependant on the above line
                Debug.Log(GeneralScript.DictToString(connectionTypes));
                SetConnectionsTypes(connections, connectionTypes);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        for (int i = 0; i < connections.Count; i++)
        {
            Connection wall = connections[i];
            wall.Load(mainPlatform);
        }
    }

    private (double, double) GetShape(float size)
    {
        //Old approach: Getting int values for x and z that make up at least 1:2 ratio if exists. Else returning square room.
        //New approach: Getting x and z values that make up 1:2 ratio at any case.
        // x * 1.5x = size
        // x ^ 2 = size / 1.5
        // x = sqrt(size / 1.5)
        float ratio = 1f; // 1 means square room, 1.5 means 1:2 ratio.
        return (Math.Sqrt(size / ratio), Math.Sqrt(size / ratio) * ratio);
    }

    void CreatePlatforms(ZoneNode root)
    {
        (double x, double z) = GetShape(root.size);

        List<ZoneNode> children = root.children.Values.ToList();
        children.Sort((a, b) => (b.side == "center" ? -100 : 0) + (b.size > a.size ? 1 : -1));
        
        if (children.Count != 0)
        {
            if ((children[0].side == "right" || children[0].side == "left") && x < z || (children[0].side == "up" || children[0].side == "down") && x > z)
            {
                foreach (ZoneNode child in children)
                {
                    child.side = (child.side == "left" ? "down" :
                        child.side == "down" ? "right" :
                        child.side == "right" ? "up" :
                        child.side == "up" ? "left" :
                        "center");
                }
            }
        }
        
        DividePlatform(root, 0, 0, (float)x, (float)z, mainPlatform);
    }

    void DividePlatform(ZoneNode root, float x, float z, float sizeX, float sizeZ, GameObject parent)
    {
        if (root.children.Count == 0)
        {
            CreatePlatform(root, x, z, sizeX, sizeZ, parent);
            return;
        }

        float freeX = sizeX;
        float freeZ = sizeZ;
        float freeCenterX = x;
        float freeCenterZ = z;

        List<ZoneNode> children = root.children.Values.ToList();
        children.Sort((a, b) => (b.side == "center" ? -100 : 0) + (b.size > a.size ? 1 : -1));

        if (children.Count != 0)
        {
            if ((children[0].side == "right" || children[0].side == "left") && sizeX < sizeZ || (children[0].side == "up" || children[0].side == "down") && sizeX > sizeZ)
            {
                foreach (ZoneNode child in children)
                {
                    child.side = (child.side == "left" ? "down" :
                        child.side == "down" ? "right" :
                        child.side == "right" ? "up" :
                        child.side == "up" ? "left" :
                        "center");
                }
            }
        }

        //Placing
        //Place biggest subzone.
        //Make one of its sides equal to the smallest side in the free area.
        //Calculate the other side by dividing the subzone size over the first side.
        GameObject parentPlatform = CreatePlatform(root, freeCenterX, freeCenterZ, freeX, freeZ, parent, -0.01f);
        for (int i = 0; i < children.Count; i++)
        {
            //First approach: if freeX < freeZ, place up, else place right.
            //Second approach: place it according to its side.
            ZoneNode child = children[i];
            if (child.side == "up")
            {
                float childSizeX = freeX;
                float childSizeZ = child.size / childSizeX;
                float childCenterX = freeCenterX;
                float childCenterZ = freeCenterZ + (freeZ / 2f) - childSizeZ / 2f;
                DividePlatform(child, childCenterX, childCenterZ, childSizeX, childSizeZ, parentPlatform);
                freeZ -= childSizeZ;
                freeCenterZ -= childSizeZ / 2f;
            }
            else if (child.side == "down")
            {
                float childSizeX = freeX;
                float childSizeZ = child.size / childSizeX;
                float childCenterX = freeCenterX;
                float childCenterZ = freeCenterZ - (freeZ / 2f) + childSizeZ / 2f;
                DividePlatform(child, childCenterX, childCenterZ, childSizeX, childSizeZ, parentPlatform);
                freeZ -= childSizeZ;
                freeCenterZ += childSizeZ / 2f;
            }
            else if (child.side == "right")
            {
                float childSizeZ = freeZ;
                float childSizeX = child.size / childSizeZ;
                float childCenterZ = freeCenterZ;
                float childCenterX = freeCenterX + freeX / 2f - childSizeX / 2f;
                DividePlatform(child, childCenterX, childCenterZ, childSizeX, childSizeZ, parentPlatform);
                freeX -= childSizeX;
                freeCenterX -= childSizeX / 2f;
            }
            else if (child.side == "left")
            {
                float childSizeZ = freeZ;
                float childSizeX = child.size / childSizeZ;
                float childCenterZ = freeCenterZ;
                float childCenterX = freeCenterX - freeX / 2f + childSizeX / 2f;
                DividePlatform(child, childCenterX, childCenterZ, childSizeX, childSizeZ, parentPlatform);
                freeX -= childSizeX;
                freeCenterX += childSizeX / 2f;
            }
            else
            {
                if(freeX <= freeZ)
                {
                    float childSizeX = freeX;
                    float childSizeZ = child.size / childSizeX;
                    float childCenterX = freeCenterX;
                    float childCenterZ = freeCenterZ + (freeZ / 2f) - childSizeZ / 2f;
                    DividePlatform(child, childCenterX, childCenterZ, childSizeX, childSizeZ, parentPlatform);
                    freeZ -= childSizeZ;
                    freeCenterZ -= childSizeZ / 2f;
                }
                else
                {
                    float childSizeZ = freeZ;
                    float childSizeX = child.size / childSizeZ;
                    float childCenterZ = freeCenterZ;
                    float childCenterX = freeCenterX + freeX / 2f - childSizeX / 2f;
                    DividePlatform(child, childCenterX, childCenterZ, childSizeX, childSizeZ, parentPlatform);
                    freeX -= childSizeX;
                    freeCenterX -= childSizeX / 2f;
                }
            }
        }
    }

    private async Task<string> GetObjects(string message)
    {
        string responseContent = await geminiAPI.SendPrompt(message);
        return responseContent;
    }

    private async Task<bool> GetTextures(ZoneNode root)
    {
        List<ZoneNode> zones = root.GetLeafNodes();
        foreach (ZoneNode zone in zones)
        {
            zone.floorMaterial = await textureSelector.GetMaterial(zone.floorTextureName);
            if (zone.wallsTextureName != null)
                zone.wallsMaterial = await textureSelector.GetMaterial(zone.wallsTextureName);
        }
        return true;
    }

    private async Task<bool> SelectAssets(ZoneNode root)
    {
        Dictionary<string, object> zonesAssetsPrompt = new Dictionary<string, object>();
        Dictionary<string, object> objectsAssetsDetails = new Dictionary<string, object>();
        Stack<ZoneNode> stack = new Stack<ZoneNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            ZoneNode node = stack.Pop();
            foreach (var child in node.children.Values)
            {
                stack.Push(child);
            }

            if (node.objects.Count == 0)
                continue;

            Dictionary<string, object> promptZone = new Dictionary<string, object>();
            zonesAssetsPrompt.Add(node.GetExtendedName(), promptZone);
            foreach (KeyValuePair<string, Vector3> obj in node.objects)
            {
                List<AssetDetails> assets = await modelRetrivier.SearchSimilarAssets(obj.Key);
                //Debug.Log($"Assets for {obj.Key} in the {node.name}: {string.Join(", ", assets.Select(asset => asset.name))}");

                Dictionary<string, object> promptObject = new Dictionary<string, object>();
                promptZone.Add(obj.Key, promptObject);

                Dictionary<string, object> objectAssets = new Dictionary<string, object>();
                if (!objectsAssetsDetails.ContainsKey(obj.Key))
                    objectsAssetsDetails.Add(obj.Key, objectAssets);

                for (int i = 0; i < assets.Count(); i++)
                {
                    Dictionary<string, object> asset = new Dictionary<string, object>();
                    asset.Add("name", assets[i].name);
                    asset.Add("tags", assets[i].tags);
                    asset.Add("categories", assets[i].categories);
                    //asset.Add("size", GeneralScript.Vector3ToString(await modelRetrivier.GetRelativeAssetSize(assets[i], obj.Value)));

                    promptObject.Add("asset " + i, asset);
                    objectAssets.Add("asset " + i, assets[i]);
                }
            }
        }
        string zonesAssetsString = GeneralScript.DictToString(zonesAssetsPrompt);

        string assetSelectionPrompt = ReadTextFile(Application.dataPath + "/Prompts/AssetSelectionPrompt.txt");

        assetSelectionPrompt = assetSelectionPrompt.Replace("{Place}", root.name);
        assetSelectionPrompt = assetSelectionPrompt.Replace("{Data}", zonesAssetsString); 
        Debug.Log("Asset Selection Prompt: " + assetSelectionPrompt);

        string response = await geminiAPI.SendPrompt(assetSelectionPrompt);
        Dictionary<string, object> responseDict = GeneralScript.StringToDict(response);
        Debug.Log(GeneralScript.DictToString(responseDict));

        foreach (KeyValuePair<string, object> responseZone in responseDict)
        {
            ZoneNode node = root.GetZoneNode(responseZone.Key);
            Dictionary<string, object> responseZoneObjects = (Dictionary<string, object>)responseZone.Value;
            foreach (KeyValuePair<string, object> responseObject in responseZoneObjects)
            {
                string objectName = responseObject.Key;
                Debug.Log(objectName);

                string chooseAssetName = responseObject.Value + "";
                Debug.Log(chooseAssetName);

                if (!objectsAssetsDetails.ContainsKey(objectName) || !chooseAssetName.Contains("asset"))
                    continue;
                Dictionary<string, object> objAssets = (Dictionary<string, object>)objectsAssetsDetails[objectName];

                AssetDetails choosenAsset = (AssetDetails)objAssets[chooseAssetName];
                node.assets.Add(objectName, choosenAsset);
            }
        }
        return true;
    }

    private async Task<bool> PlaceAssets(ZoneNode root)
    {
        List<ZoneNode> leafNodes = root.GetLeafNodes();
        foreach (ZoneNode leafNode in leafNodes)
        {
            if(leafNode.assets.Count == 0)
            {
                Debug.LogWarning($"No assets for {leafNode.name}\n{string.Join(", ", leafNode.objects.Keys)}");
                continue;
            }
            Vector3 zoneSize = GeneralScript.GetAllBounds(leafNode.platform).size;
            Vector3 zonePosition = leafNode.platform.transform.position;
            Debug.Log(leafNode.name + ": " + leafNode.connections.Count + $"\n" +
                $"{leafNode.connections.Select(c => $"{c.connectionType}: {c.x - zonePosition.x}, {c.z - zonePosition.z}").Aggregate(zonePosition.ToString(), (a, b) => $"{a}\n{b}")}");
            string top = (leafNode.connections.Where(c => c.z > zonePosition.z).Count() == 0) ? "" : leafNode.connections
                .Where(c => c.z > zonePosition.z)
                .Select(c => {
                    string connectionType = (c.connectionType == ConnectionType.Open ? "Open" : c.connectionType == ConnectionType.Wall ? "Wall" : "Door");
                    float areaWidth = c.connectionType == ConnectionType.Door ? Connection.doorWidth : c.width;
                    string startPos = $"({c.x - areaWidth / 2f - zonePosition.x}, {c.z - zonePosition.z})";
                    string endPos = $"({c.x + areaWidth / 2f - zonePosition.x}, {c.z - zonePosition.z})";
                    string res = $"{connectionType} from {startPos} to {endPos}";
                    return res;
                }).Aggregate((a, b) => $"{a}, {b}");
            string bottom = (leafNode.connections.Where(c => c.z < zonePosition.z).Count() == 0) ? "" : leafNode.connections
                .Where(c => c.z < zonePosition.z)
                .Select(c =>
                {
                    string connectionType = (c.connectionType == ConnectionType.Open ? "Open" : c.connectionType == ConnectionType.Wall ? "Wall" : "Door");
                    float areaWidth = c.connectionType == ConnectionType.Door ? Connection.doorWidth : c.width;
                    string startPos = $"({c.x - areaWidth / 2f - zonePosition.x}, {c.z - zonePosition.z})";
                    string endPos = $"({c.x + areaWidth / 2f - zonePosition.x}, {c.z - zonePosition.z})";
                    string res = $"{connectionType} from {startPos} to {endPos}";
                    return res;
                }).Aggregate((a, b) => $"{a}, {b}");
            string left = (leafNode.connections.Where(c => c.x < zonePosition.x).Count() == 0) ? "" : leafNode.connections
                .Where(c => c.x < zonePosition.x)
                .Select(c =>
                {
                    string connectionType = (c.connectionType == ConnectionType.Open ? "Open" : c.connectionType == ConnectionType.Wall ? "Wall" : "Door");
                    float areaWidth = c.connectionType == ConnectionType.Door ? Connection.doorWidth : c.width;
                    string startPos = $"({c.x - zonePosition.x}, {c.z - areaWidth / 2f - zonePosition.z})";
                    string endPos = $"({c.x - zonePosition.x}, {c.z + areaWidth / 2f - zonePosition.z})";
                    string res = $"{connectionType} from {startPos} to {endPos}";
                    return res;
                }).Aggregate((a, b) => $"{a}, {b}");
            string right = (leafNode.connections.Where(c => c.x < zonePosition.x).Count() == 0) ? "" : leafNode.connections
                .Where(c => c.x > zonePosition.x)
                .Select(c =>
                {
                    string connectionType = (c.connectionType == ConnectionType.Open ? "Open" : c.connectionType == ConnectionType.Wall ? "Wall" : "Door");
                    float areaWidth = c.connectionType == ConnectionType.Door ? Connection.doorWidth : c.width;
                    string startPos = $"({c.x - zonePosition.x}, {c.z - areaWidth / 2f - zonePosition.z})";
                    string endPos = $"({c.x - zonePosition.x}, {c.z + areaWidth / 2f - zonePosition.z})";
                    string res = $"{connectionType} from {startPos} to {endPos}";
                    return res;
                }).Aggregate((a, b) => $"{a}, {b}");
            string roomEdges =
                $"Top: {top} \n" +
                $"Bottom: {bottom}\n" +
                $"Left: {left}\n" +
                $"Right: {right}";
            string objects = string.Join("\n", leafNode.assets.Select(pair => $"{pair.Key}: ({leafNode.objects[pair.Key].x} * {leafNode.objects[pair.Key].z})"));

            string prompt = ReadTextFile(Application.dataPath + "/Prompts/ObjectPlacement.txt");
            prompt = prompt.Replace("{Dimensions}", $"({zoneSize.x} width * {zoneSize.z} length)");
            prompt = prompt.Replace("{SubZone}", $"{leafNode.name}");
            prompt = prompt.Replace("{Place}", $"{root.name}");
            prompt = prompt.Replace("{Objects}", $"{objects}");
            prompt = prompt.Replace("{ZoneEdges}", $"{roomEdges}");
            Debug.Log("Object Placement Prompt: " + prompt);

            string response = await geminiAPI.SendPrompt(prompt);
            Debug.Log(response);
            Dictionary<string, object> responseDict = GeneralScript.StringToDict(response);
            Debug.Log(GeneralScript.DictToString(responseDict));
            Vector3 platformSize = GeneralScript.GetAllBounds(leafNode.platform).size;
            foreach (KeyValuePair<string, object> responseObject in responseDict)
            {
                try
                {
                    string responseObjectName = responseObject.Key.ToLower().Replace("_", "").Replace(" ", "");
                    int count = 0;
                    while (Char.IsDigit(responseObjectName[responseObjectName.Length - 1 - count]))
                        count++;
                    responseObjectName = responseObjectName.Substring(0, responseObjectName.Length - count);
                    //Debug.Log(responseObjectName);

                    (string objectName, AssetDetails assetDetails) = leafNode.assets.Where(obj => {
                        string objName = obj.Key.ToLower().Trim().Replace(" ", "");
                        return objName.Contains(responseObjectName) || responseObjectName.Contains(objName);
                    }).ToArray()[0];
                    Dictionary<string, object> objectDetails = (Dictionary<string, object>)responseObject.Value;
                    Vector3 objectSize = leafNode.objects[objectName];
                    Vector2 position = GeneralScript.StringToVector2(objectDetails["position"].ToString());
                    Vector3 objectPosition = new Vector3(position.x, 0.001f, position.y);
                    string direction = objectDetails["direction"].ToString().Trim();
                    float objectRotation = (direction == "north" ? 0 : direction == "east" ? 90 : direction == "south" ? 180 : direction == "west" ? 270 : 0);

                    GameObject model = await modelRetrivier.GetAsset(assetDetails);
                    model.name = assetDetails.name;
                    GeneralScript.RescaleAsset(model, objectSize);
                    Vector3 modelSize = GeneralScript.GetAllBounds(model).size;
                    GeneralScript.SetExactPosition(model, leafNode.platform.transform.position + objectPosition + modelSize / 2f + platformSize / 2f - new Vector3(platformSize.x, 0, platformSize.z) / 2f);
                    model.transform.rotation = Quaternion.Euler(0, objectRotation, 0);
                    model.transform.SetParent(leafNode.platform.transform);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Object not placed " + e);
                }
            }
        }
        return true;
    }

    async void CreateScene(string context, string language)
    {
        string floorPlanPrompt = ReadTextFile(Application.dataPath + "/Prompts/FloorPlan.txt");
        floorPlanPrompt = floorPlanPrompt.Replace("{Place}", context);
        Debug.Log($"FloorPlan Prompt: {floorPlanPrompt}");


        string llmResponse = $"{await geminiAPI.SendPrompt(floorPlanPrompt)}";
        Debug.Log($"FloorPlan Response: {llmResponse}");

        ZoneNode root = ZoneNode.GetZoneNodeFromString(llmResponse);
        Debug.Log(root);

        await GetTextures(root);

        CreatePlatforms(root);

        await GenerateConnections(root);

        await SelectAssets(root);

        await PlaceAssets(root);
        Debug.Log("Scene Generated");
    }


    //---------------------------------------------------------------------//
    //---------------------------------------------------------------------//
    //---------------------------------------------------------------------//
    //---------------------------------------------------------------------//
    //--------------Old Layout Approach Code and Unused Code---------------//
    //---------------------------------------------------------------------//
    //---------------------------------------------------------------------//
    //---------------------------------------------------------------------//
    //---------------------------------------------------------------------//




    private void createLayout(GameObject newPlatform, string layoutName)
    {
        //Object Creation
        //GameObject newObject = Resources.Load<GameObject>("Cafeteria Bench");
        GameObject newObject = Resources.Load<GameObject>("Cafeteria Table");
        //GameObject newObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

        Vector3 objOriginalRotation = new Vector3(0, 0, 0);
        newObject.transform.rotation = Quaternion.Euler(objOriginalRotation);

        Vector3 platformGlobalBounds = GeneralScript.GetBounds(newPlatform).size;
        Vector3 objectGlobalBounds = GeneralScript.GetBounds(newObject).size;

        bool onX = false;
        Vector3 roomRotation = new Vector3(0, 0, 0);
        rotatePlatform(newPlatform, ref onX, ref roomRotation);

        float xSize = 0, zSize = 0, unUsableSizeX = 0, unUsableSizeZ = 0;
        int cols = 0, rows = 0;

        if (layoutName == "Zoned")
        {
            calculateBounds(platformGlobalBounds, newObject, ref xSize, ref zSize,
                ref rows, ref cols, ref unUsableSizeX, ref unUsableSizeZ, 1.5f, 1f);
            //Top
            for (float i = 1; i < cols - 1; i++)
            {
                float posX = (i - cols / 2f) * xSize;
                float posZ = (rows / 2f) * zSize - xSize / 2f;
                GameObject obj = Instantiate(newObject);
                setObjectPosition(obj, newPlatform, posX, posZ, xSize, zSize);
            }
            newObject.transform.rotation = Quaternion.Euler(objOriginalRotation + new Vector3(0, 90, 0));
            calculateBounds(platformGlobalBounds, newObject, ref xSize, ref zSize,
                ref rows, ref cols, ref unUsableSizeX, ref unUsableSizeZ, 1f, 1.5f);
            //Sides
            for (int j = 0; j < rows - 1; j++)
            {
                float posX = (cols / 2f) * xSize - zSize / 2f;
                float posZ = (j - rows / 2f) * zSize;

                GameObject obj1 = Instantiate(newObject);
                setObjectPosition(obj1, newPlatform, posX, posZ, xSize, zSize);

                GameObject obj2 = Instantiate(newObject);
                obj2.transform.rotation = Quaternion.Euler(objOriginalRotation + new Vector3(0, 270, 0));
                setObjectPosition(obj2, newPlatform, posX, posZ, xSize, zSize);
                obj2.transform.localPosition = new Vector3(obj2.transform.localPosition.x * -1, obj2.transform.localPosition.y, obj2.transform.localPosition.z);
            }
        }
        else if (layoutName == "Grid")
        {
            calculateBounds(platformGlobalBounds, newObject, ref xSize, ref zSize,
                ref rows, ref cols, ref unUsableSizeX, ref unUsableSizeZ, 1.5f, 1.5f);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    float posX = (j - cols / 2f) * xSize;
                    float posZ = (i - rows / 2f) * zSize;
                    GameObject obj = Instantiate(newObject);
                    setObjectPosition(obj, newPlatform, posX, posZ, xSize, zSize);
                }
            }
        }
        else if (layoutName == "Linear")
        {
            newObject.transform.rotation = Quaternion.Euler(objOriginalRotation + new Vector3(0, 90, 0));
            calculateBounds(platformGlobalBounds, newObject, ref xSize, ref zSize,
                ref rows, ref cols, ref unUsableSizeX, ref unUsableSizeZ, 2f, 1f);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    float posX = (j - cols / 2f) * xSize;
                    float posZ = (i - rows / 2f) * zSize;
                    GameObject obj = Instantiate(newObject);
                    setObjectPosition(obj, newPlatform, posX, posZ, xSize, zSize);
                }
            }
        }
        else if (layoutName == "Clustered")
        {
            calculateBounds(platformGlobalBounds, newObject, ref xSize, ref zSize,
                ref rows, ref cols, ref unUsableSizeX, ref unUsableSizeZ, 1f, 2f);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    if (random.Next(0, 10) < 3)
                    {
                        continue;
                    }
                    float posX = (j - cols / 2f) * xSize;
                    float posZ = (i - rows / 2f) * zSize;
                    GameObject obj = Instantiate(newObject);
                    setObjectPosition(obj, newPlatform, posX, posZ, xSize, zSize);
                }
            }
        }
        else if (layoutName == "Open")
        {
            newObject.transform.rotation = Quaternion.Euler(objOriginalRotation + new Vector3(0, 90, 0));
            calculateBounds(platformGlobalBounds, newObject, ref xSize, ref zSize,
                ref rows, ref cols, ref unUsableSizeX, ref unUsableSizeZ, 1f, 2f);

            for (int i = 0; i < rows; i++)
            {
                float posX = (cols / 2f) * xSize - xSize / 2f;
                float posZ = (i - rows / 2f) * zSize;
                GameObject obj1 = Instantiate(newObject);
                setObjectPosition(obj1, newPlatform, posX, posZ, xSize, zSize);

                GameObject obj2 = Instantiate(newObject);
                obj2.transform.rotation = Quaternion.Euler(objOriginalRotation + new Vector3(0, 270, 0));
                setObjectPosition(obj2, newPlatform, posX, posZ, xSize, zSize);
                obj2.transform.localPosition = new Vector3(obj2.transform.localPosition.x * -1, obj2.transform.localPosition.y, obj2.transform.localPosition.z);
            }
        }
        else if (layoutName == "Flow - Optimized")
        {
            //Top
            calculateBounds(platformGlobalBounds, newObject, ref xSize, ref zSize,
                ref rows, ref cols, ref unUsableSizeX, ref unUsableSizeZ, 1f, 1f);
            for (float i = 0; i < cols; i++)
            {
                float posX = (i - cols / 2f) * xSize;
                float posZ = (rows / 2f) * zSize - zSize / 2f;
                GameObject obj = Instantiate(newObject);
                setObjectPosition(obj, newPlatform, posX, posZ, xSize, zSize);
            }
            //Sides
            newObject.transform.rotation = Quaternion.Euler(objOriginalRotation + new Vector3(0, 90, 0));
            calculateBounds(platformGlobalBounds - new Vector3(0, 0, objectGlobalBounds.x), newObject, ref xSize, ref zSize,
                ref rows, ref cols, ref unUsableSizeX, ref unUsableSizeZ, 1f, 1f);//
            for (int j = 0; j < rows; j++)
            {
                float posX = (cols / 2f) * xSize - xSize / 2f;
                float posZ = (j - rows / 2f) * zSize - zSize;

                GameObject obj1 = Instantiate(newObject);
                setObjectPosition(obj1, newPlatform, posX, posZ, xSize, zSize);

                GameObject obj2 = Instantiate(newObject);
                obj2.transform.rotation = Quaternion.Euler(objOriginalRotation + new Vector3(0, 270, 0));
                setObjectPosition(obj2, newPlatform, posX, posZ, xSize, zSize);
                obj2.transform.localPosition = new Vector3(obj2.transform.localPosition.x * -1, obj2.transform.localPosition.y, obj2.transform.localPosition.z);
            }

            //Center
            calculateBounds(platformGlobalBounds - new Vector3(0, 0, objectGlobalBounds.z), newObject, ref xSize, ref zSize,
                ref rows, ref cols, ref unUsableSizeX, ref unUsableSizeZ, 2f, 1f);
            for (float i = 1; i < cols - 1; i++)
            {
                float posX = (i - cols / 2f) * xSize;
                for (int j = 0; j < rows - 1; j++)
                {
                    float posZ = (j - rows / 2f) * zSize - zSize;
                    GameObject obj = Instantiate(newObject);
                    setObjectPosition(obj, newPlatform, posX, posZ, xSize, zSize);
                }
            }
        }
        newPlatform.transform.rotation = Quaternion.Euler(roomRotation);
    }
    private void rotatePlatform(GameObject newPlatform, ref bool onX, ref Vector3 roomRotation)
    {
        Vector3 platformPosition = newPlatform.transform.localPosition;

        if (platformPosition.z > 0 && platformPosition.z >= Math.Abs(platformPosition.x))
        {
            roomRotation = new Vector3(0, 0, 0);
        }
        else if (platformPosition.z <= 0 && Math.Abs(platformPosition.z) >= Math.Abs(platformPosition.x))
        {
            roomRotation = new Vector3(0, 180, 0);
        }
        else if (platformPosition.x > 0 && platformPosition.x > Math.Abs(platformPosition.z))
        {
            roomRotation = new Vector3(0, 90, 0);
            onX = true;
        }
        else if (platformPosition.x < 0 && Math.Abs(platformPosition.x) > Math.Abs(platformPosition.z))
        {
            roomRotation = new Vector3(0, 270, 0);
            onX = true;
        }

        if (onX)
        {
            newPlatform.transform.localScale = new Vector3(newPlatform.transform.localScale.z, newPlatform.transform.localScale.y, newPlatform.transform.localScale.x);
        }
    }

    private void setObjectPosition(GameObject obj, GameObject newPlatform, float x, float z, float sizeX, float sizeZ)
    {
        Vector3 platformGlobalBounds = GeneralScript.GetBounds(newPlatform).size;
        Vector3 objGlobalBounds = GeneralScript.GetBounds(obj).size;
        obj.transform.parent = newPlatform.transform;
        obj.transform.localPosition =
            GeneralScript.DivideVectors(
                    new Vector3(x, 0, z)
                    + new Vector3(sizeX, objGlobalBounds.y, sizeZ) / 2f
            , platformGlobalBounds);
    }



    //delegate void LayoutFunction(GameObject newPlatform, float actualSizeX, float actualSizeZ);

    //private void applyLayout(GameObject newPlatform, string layoutName)
    //{
    //    Vector3 position = newPlatform.transform.localPosition;
    //    Debug.Log(position);
    //    //Because it is not precise enough for some cases such as == operator.
    //    float precision = 0.1f;

    //    //size is 1/10 of the original platform size
    //    float globalObjectSizeX = 1f / 10f;
    //    float globalObjectSizeZ = 1f / 20f;

    //    float actualSizeX = globalObjectSizeX / newPlatform.transform.localScale.x;
    //    float actualSizeZ = globalObjectSizeZ / newPlatform.transform.localScale.z;

    //    //float borderSpaceX = actualSizeX;
    //    //float borderSpaceZ = actualSizeZ;

    //    float originalLLX = (-0.5f + actualSizeX / 2f);
    //    float originalLLZ = (-0.5f + actualSizeZ / 2f);
    //    float originalULX = (0.5f - actualSizeX / 2f);
    //    float originalULZ = (0.5f - actualSizeZ / 2f);

    //    #nullable enable
    //    LayoutFunction layoutFunction = null;

    //    switch (layoutName)
    //    {
    //        case "Zoned":
    //            layoutFunction = (GameObject newPlatform, float actualSizeX, float actualSizeZ)=>{
    //                //Top Part
    //                float llX = originalLLX + actualSizeX;
    //                float ulX = originalULX - actualSizeX;
    //                float unUsableSizeX = ((originalULX - actualSizeX) * 2 + precision * actualSizeX) % (1.5f * actualSizeX);
    //                llX += unUsableSizeX / 2f;
    //                ulX -= unUsableSizeX / 2f;
    //                for (float i = llX; i - precision * actualSizeX <= ulX; i += actualSizeX * 1.5f)
    //                {
    //                    float posX = i;
    //                    float posZ = originalULZ;
    //                    Color color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);
    //                    createObject(null, newPlatform, posX, newPlatform.transform.localScale.y / 2f + 0.25f, posZ, actualSizeX, 0.5f, actualSizeZ, color);
    //                }

    //                //Sides
    //                for(float i = originalLLZ; i - precision * actualSizeZ <= originalULZ - actualSizeZ; i += actualSizeZ * 1.5f)
    //                {
    //                    float posX = originalLLX;
    //                    float posZ = i;
    //                    Color color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);
    //                    createObject(null, newPlatform, posX, newPlatform.transform.localScale.y / 2f + 0.25f, posZ, actualSizeX, 0.5f, actualSizeZ, color);

    //                    posX *= -1;
    //                    color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);
    //                    createObject(null, newPlatform, posX, newPlatform.transform.localScale.y / 2f + 0.25f, posZ, actualSizeX, 0.5f, actualSizeZ, color);
    //                }
    //            };
    //            break;
    //        case "Grid":
    //            layoutFunction = (GameObject newPlatform, float actualSizeX, float actualSizeZ) => {
    //                float unUsableSizeX = (originalULX * 2 + precision * actualSizeX) % (1.5f * actualSizeX);
    //                float unUsableSizeZ = (originalULZ * 2 + precision * actualSizeZ) % (1.5f * actualSizeZ);
    //                float llX = originalLLX + unUsableSizeX / 2f;
    //                float llZ = originalLLZ + unUsableSizeZ / 2f;
    //                float ulX = originalULX - unUsableSizeX / 2f;
    //                float ulZ = originalULZ - unUsableSizeZ / 2f;
    //                for (float i = llX; i - precision * actualSizeX <= ulX; i += 1.5f * actualSizeX)
    //                {
    //                    for (float j = llZ; j - precision * actualSizeZ <= ulZ; j += 1.5f * actualSizeZ)
    //                    {
    //                        float posX = i;
    //                        float posZ = j;
    //                        Color color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);
    //                        createObject(null, newPlatform, posX, newPlatform.transform.localScale.y / 2f + 0.25f, posZ, actualSizeX, 0.5f, actualSizeZ, color);
    //                    }
    //                }
    //            };
    //            break;
    //        case "Radial":
    //            layoutFunction = (GameObject newPlatform, float actualSizeX, float actualSizeZ) => {
    //                //float unUsableSizeX = (originalULX * 2 + precision * actualSizeX) % (0.5f * actualSizeX);
    //                //float unUsableSizeZ = (originalULZ * 2 + precision * actualSizeZ) % (0.5f * actualSizeZ);
    //                //float llX = originalLLX + unUsableSizeX / 2f;
    //                //float llZ = originalLLZ + unUsableSizeZ / 2f;
    //                //float ulX = originalULX - unUsableSizeX / 2f;
    //                //float ulZ = originalULZ - unUsableSizeZ / 2f;
    //                //float blockSize = Math.Max(actualSizeX, actualSizeZ) * 0.5f;
    //                //float objectSize = 1 - Math.Max(actualSizeX, actualSizeZ);
    //                //for (float i = llX; i - precision * actualSizeX < ulX; i += 0.5f * actualSizeX)
    //                //{
    //                //    for (float j = llZ; j - precision * actualSizeZ < ulZ; j += 0.5f * actualSizeZ)
    //                //    {

    //                //        if (Math.Sqrt(i * i + j * j) >= (objectSize / 2) - blockSize / 2 && Math.Sqrt(i * i + j * j) <= (objectSize / 2) + blockSize / 2)
    //                //        {
    //                //            float posX = i;
    //                //            float posZ = j;
    //                //            Color color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);
    //                //            createObject(null, newPlatform, posX, newPlatform.transform.localScale.y / 2f + 0.25f, posZ, actualSizeX, 0.5f, actualSizeZ, color);
    //                //        }
    //                //    }
    //                //}
    //            };
    //            break;
    //        case "Linear":

    //            layoutFunction = (GameObject newPlatform, float actualSizeX, float actualSizeZ) => {
    //                float unUsableSizeX = (originalULX * 2 + precision * actualSizeX) % (1.5f * actualSizeX);
    //                float unUsableSizeZ = (originalULZ * 2 + precision * actualSizeZ) % (1.5f * actualSizeZ);
    //                float llX = originalLLX + unUsableSizeX / 2f;
    //                float ulX = originalULX - unUsableSizeX / 2f;
    //                float llZ = originalLLZ + unUsableSizeZ / 2f;
    //                float ulZ = originalULZ - unUsableSizeZ / 2f;
    //                for (float i = llX; i - precision * actualSizeX < ulX; i += 1.5f * actualSizeX)
    //                {
    //                    for (float j = llZ; j - precision * actualSizeZ < ulZ; j += actualSizeZ)
    //                    {
    //                        float posX = i;
    //                        float posZ = j;
    //                        Color color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);
    //                        createObject(null, newPlatform, posX, newPlatform.transform.localScale.y / 2f + 0.25f, posZ, actualSizeX, 0.5f, actualSizeZ, color);
    //                    }
    //                }
    //            };
    //            break;
    //        case "Clustered":
    //            layoutFunction = (GameObject newPlatform, float actualSizeX, float actualSizeZ) => {
    //                float unUsableSizeX = (originalULX * 2 + precision * actualSizeX) % (actualSizeX);
    //                float unUsableSizeZ = (originalULZ * 2 + precision * actualSizeZ) % (2f * actualSizeZ);
    //                float llX = originalLLX + unUsableSizeX / 2f;
    //                float llZ = originalLLZ + unUsableSizeZ / 2f;
    //                float ulX = originalULX - unUsableSizeX / 2f;
    //                float ulZ = originalULZ - unUsableSizeZ / 2f;
    //                for (float i = llX; i - precision * actualSizeX <= ulX; i += actualSizeX)
    //                {
    //                    for (float j = llZ; j - precision * actualSizeZ <= ulZ; j += 2f * actualSizeZ)
    //                    {
    //                        if(random.Next(0, 10) < 3)
    //                        {
    //                            continue;
    //                        }
    //                        float posX = i;
    //                        float posZ = j;
    //                        Color color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);
    //                        createObject(null, newPlatform, posX, newPlatform.transform.localScale.y / 2f + 0.25f, posZ, actualSizeX, 0.5f, actualSizeZ, color);
    //                    }
    //                }
    //            };
    //            break;
    //        case "Open":
    //            layoutFunction = (GameObject newPlatform, float actualSizeX, float actualSizeZ) => {
    //                float unUsableSizeZ = (originalULZ * 2 + precision * actualSizeZ) % (1.5f * actualSizeZ);
    //                float llZ = originalLLZ + unUsableSizeZ / 2f;
    //                float ulZ = originalULZ - unUsableSizeZ / 2f;
    //                for (float i = llZ; i - precision * actualSizeZ < ulZ; i += actualSizeZ * 1.5f)
    //                {
    //                    float posX = originalLLX;
    //                    float posZ = i;
    //                    Color color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);
    //                    createObject(null, newPlatform, posX, newPlatform.transform.localScale.y / 2f + 0.25f, posZ, actualSizeX, 0.5f, actualSizeZ, color);

    //                    posX *= -1;
    //                    color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);
    //                    createObject(null, newPlatform, posX, newPlatform.transform.localScale.y / 2f + 0.25f, posZ, actualSizeX, 0.5f, actualSizeZ, color);
    //                }
    //            };
    //            break;
    //        case "Flow - Optimized":
    //            layoutFunction = (GameObject newPlatform, float actualSizeX, float actualSizeZ) => {
    //                float unUsableSizeX = 0;
    //                float unUsableSizeZ = 0;
    //                float llX = originalLLX + unUsableSizeX / 2f;
    //                float llZ = originalLLZ + unUsableSizeZ / 2f;
    //                float ulX = originalULX - unUsableSizeX / 2f;
    //                float ulZ = originalULZ - unUsableSizeZ / 2f;
    //                //Top
    //                for (float i = llX + actualSizeX; i - precision * actualSizeX <= ulX - actualSizeX; i += actualSizeX)
    //                {
    //                    float posX = i;
    //                    float posZ = ulZ;
    //                    Color color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);
    //                    createObject(null, newPlatform, posX, newPlatform.transform.localScale.y / 2f + 0.25f, posZ, actualSizeX, 0.5f, actualSizeZ, color);
    //                }

    //                //Sides
    //                for (float i = llZ; i - precision * actualSizeZ <= ulZ; i += actualSizeZ)
    //                {
    //                    float posX = llX;
    //                    float posZ = i;
    //                    Color color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);
    //                    createObject(null, newPlatform, posX, newPlatform.transform.localScale.y / 2f + 0.25f, posZ, actualSizeX, 0.5f, actualSizeZ, color);

    //                    posX *= -1;
    //                    color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);
    //                    createObject(null, newPlatform, posX, newPlatform.transform.localScale.y / 2f + 0.25f, posZ, actualSizeX, 0.5f, actualSizeZ, color);
    //                }

    //                //Linear Middle
    //                llX = originalLLX + actualSizeX * 2;
    //                ulZ = originalULZ - actualSizeZ * 2;
    //                ulX = originalULX - actualSizeX * 2;

    //                unUsableSizeX = (ulX * 2) % (2f * actualSizeX);
    //                llX += unUsableSizeX / 2f;
    //                ulX -= unUsableSizeX / 2f;

    //                for (float i = llX; i - precision * actualSizeX < ulX; i += 2f * actualSizeX)
    //                {
    //                    for (float j = originalLLZ; j - precision * actualSizeZ < ulZ; j += actualSizeZ)
    //                    {
    //                        float posX = i;
    //                        float posZ = j;
    //                        Color color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);
    //                        createObject(null, newPlatform, posX, newPlatform.transform.localScale.y / 2f + 0.25f, posZ, actualSizeX, 0.5f, actualSizeZ, color);
    //                    }
    //                }
    //            };
    //            break;
    //        default:
    //            Debug.Log("!Default Layout!");
    //            layoutFunction = (GameObject newPlatform, float actualSizeX, float actualSizeZ) => {
    //                for (float i = originalLLX; i - precision * actualSizeX <= originalULX; i += actualSizeX)
    //                {
    //                    for (float j = originalLLZ; j - precision * actualSizeZ <= originalULZ; j += actualSizeZ)
    //                    {
    //                        float posX = i;
    //                        float posZ = j;
    //                        Color color = new Color(random.Next(100, 150) / 256f, random.Next(100, 150) / 256f, random.Next(200, 256) / 256f);
    //                        createObject(null, newPlatform, posX, newPlatform.transform.localScale.y / 2f + 0.25f, posZ, actualSizeX, 0.5f, actualSizeZ, color);
    //                    }
    //                }
    //            };
    //            break;
    //    }
    //    Debug.Log("Layout Function: " + layoutFunction);
    //    layoutFunction.Invoke(newPlatform, actualSizeX, actualSizeZ);
    //    Debug.Log("Invoked");
    //    //Debug.Log(llX + ", " + llZ);
    //}

    private void calculateBounds(Vector3 platformBounds, GameObject newObject, ref float xSize, ref float zSize, ref int rows, ref int cols, ref float unUsableSizeX, ref float unUsableSizeZ, float xSpace, float zSpace)
    {
        Vector3 objGlobalBounds = GeneralScript.GetBounds(newObject).size;

        xSize = (objGlobalBounds.x * xSpace);
        zSize = (objGlobalBounds.z * zSpace);
        cols = (int)(platformBounds.x / xSize);
        rows = (int)(platformBounds.z / zSize);
        unUsableSizeX = platformBounds.x - (int)(cols * xSize);
        unUsableSizeZ = platformBounds.z - (int)(rows * zSize);
    }

}


//For a({zoneSize.x} width * { zoneSize.z} length) {leafNode.name} in a {root.name} 2D space, organize the following objects in it given their sizes (width, length), and the layout should match a realistic layout. You can repeat the same object multiple times, set which direction the object is facing, set its position any where inside the zone, state the exact object name you are referencing, and ensure the objects are facing the right direction. The objects are:
//{objects}

//Here is some additional information about the room edges:
//{roomEdges}

//Respond in a json format like the following example with no text before or after:
//{
//    "object1": {
//        "position": (x, y),
//        "direction": "south"
//    },
//    "object1": {
//        "position": (x, y),
//        "direction": "north"
//    },
//    "object2": {
//        "position": (x, y),
//        "direction": "east"
//    },
//    "object2": {
//        "position": (x, y),
//        "direction": "west"
//    }
//}

//Ensure that the response JSON format is correct!

//For a "area", give me its subareas NAMES (and each ones subareas) that form it up in a json format. Give the result in a tree form, without mentioning very specific objects, just the zones names. Give the size of each leaf zone of the tree in square meters, and give the wall height in meters for each leaf, and give the position of each subzone relevant to its zone weather it is "right", "left", "up", "down", or "center". And for each leaf give its objects extended name that could build it up (that can be placed on the floor), the name has to be very descriptive and just one long phrase starting with the object name, the object name must not be in plural, and give the reference dimensions for each object in meters as [width, height, length]. And for each leaf give the walls texture (if closed), and the floor texture.

//Example Output:
//{
//    "Zone Name": {
//        "Subzone Name": {
//            "Subzone Name": {
//                "size": 20,
//                "type": "open",
//                "side": "left",
//                "height": 5,
//                "floor": "concrete",
//                "objects": {
//                    "object 1": [x, y, z],
//                    "object 2": [x, y, z],
//                    "object 3": [x, y, z],
//                    "object 4": [x, y, z]
//                }
//            },
//            "Subzone Name": {
//                "size": 15,
//                "type": "closed",
//                "side": "right",
//                "height": 5,
//                "walls": "marple",
//                "floor": "rock",
//                "objects": {
//                    "object 1": [x, y, z],
//                    "object 2": [x, y, z],
//                    "object 3": [x, y, z]
//                }
//            },
//            "side": "center"
//        },
//        "Subzone Name": {
//            "Subzone Name": {
//                "size": 30,
//                "type": "open",
//                "side": "up",
//                "height": 6,
//                "walls": "paving stones",
//                "objects": {
//                    "object 1": [x, y, z],
//                    "object 2": [x, y, z],
//                    "object 3": [x, y, z]
//                }
//            },
//            "Subzone Name": {
//                "size": 50,
//                "type": "closed",
//                "side": "down",
//                "height": 6,
//                "walls": "bricks",
//                "floor": "asphalt",
//                "objects": {
//                    "object 1": [x, y, z],
//                    "object 2": [x, y, z],
//                    "object 3": [x, y, z]
//                }
//            },
//            "Subzone Name": {
//                "size": 40,
//                "type": "open",
//                "side": "left",
//                "height": 6,
//                "floor": "asphalt",
//                "objects": {
//                    "object 1": [x, y, z],
//                    "object 2": [x, y, z],
//                    "object 3": [x, y, z]
//                }
//            },
//            "side": "right"
//        },
//        "Subzone Name": {
//            "Subzone Name": {
//                "size": 25,
//                "type": "closed",
//                "side": "down",
//                "height": 4,
//                "walls": "bricks",
//                "floor": "asphalt",
//                "objects": {
//                    "object 1": [x, y, z],
//                    "object 2": [x, y, z],
//                    "object 3": [x, y, z]
//                }
//            },
//            "side": "up"
//        }
//    }
//}

/*
GoogleAPI Reponse:
{
  "candidates": [
    {
      "content": {
        "parts": [
          {
            "text": "AI works by combining large amounts of data with fast, iterative processing and intelligent algorithms, allowing the software to learn automatically from patterns or features in the data.  There's no single \"how it works\" answer, as AI encompasses many different techniques, but here's a breakdown of some key concepts:\n\n**1. Data is King:** AI systems learn from data.  The more data, and the higher the quality of that data, the better the AI system will typically perform. This data can be structured (like rows and columns in a spreadsheet) or unstructured (like images, text, or audio).\n\n**2. Algorithms are the Engine:**  These are sets of rules and statistical techniques that allow the AI to learn from the data. Different AI approaches use different algorithms.  Some common types include:\n\n* **Machine Learning (ML):** This is a broad category where systems learn from data without explicit programming.  Instead of being explicitly programmed with rules, they identify patterns and relationships in the data to make predictions or decisions.  Key sub-categories include:\n    * **Supervised Learning:** The algorithm is trained on a labeled dataset (data where the desired output is known).  For example, training an image recognition system with pictures labeled \"cat\" or \"dog.\"\n    * **Unsupervised Learning:** The algorithm is trained on an unlabeled dataset and tries to find structure or patterns in the data on its own.  For example, clustering similar customers together based on their purchase history.\n    * **Reinforcement Learning:** The algorithm learns through trial and error by interacting with an environment and receiving rewards or penalties for its actions.  This is often used in robotics and game playing.\n\n* **Deep Learning (DL):** A subset of machine learning that uses artificial neural networks with multiple layers (hence \"deep\"). These networks are inspired by the structure and function of the human brain. Deep learning excels at tasks involving complex patterns, like image recognition, natural language processing, and speech recognition.\n\n* **Expert Systems:** These systems use a knowledge base of rules and facts to mimic the decision-making of a human expert in a specific domain.\n\n**3. Processing Power is Crucial:**  Training AI models, especially deep learning models, requires significant computational power.  This is often achieved using powerful Graphics Processing Units (GPUs) or specialized AI hardware.\n\n**4. Iterative Process:**  AI development is iterative.  Models are trained, evaluated, and refined based on their performance.  This involves adjusting parameters, adding more data, or trying different algorithms until a satisfactory level of accuracy or performance is achieved.\n\n**5. Evaluation and Feedback:**  The performance of an AI system is constantly monitored and evaluated using various metrics.  This feedback is used to improve the system over time.\n\n\n**In simpler terms:** Imagine teaching a dog a trick.  You show the dog (the AI) examples (the data) of the trick, reward it when it does it correctly (feedback), and correct it when it makes mistakes.  Over time, the dog learns to perform the trick reliably.  AI works similarly, but with algorithms and data instead of treats and corrections.\n\n\nIt's important to note that AI is a constantly evolving field, and new techniques and algorithms are being developed all the time.  The above explanation provides a general overview of the core principles.\n"
          }
        ],
        "role": "model"
      },
      "finishReason": "STOP",
      "citationMetadata": {
        "citationSources": [
          {
            "endIndex": 176,
            "uri": "https://www.aidataanalytics.network/data-science-ai/articles/applied-ai-enterprise-data-science"
          },
          {
            "startIndex": 1529,
            "endIndex": 1652,
            "uri": "https://github.com/NimmyBibin/ML-Assignments"
          }
        ]
      },
      "avgLogprobs": -0.194971492524781
    }
  ],
  "usageMetadata": {
    "promptTokenCount": 4,
    "candidatesTokenCount": 692,
    "totalTokenCount": 696
  },
  "modelVersion": "gemini-1.5-flash-002"
}

GoogleAPI Solvable Error
{
  "error": {
    "code": 503,
    "message": "The model is overloaded. Please try again later.",
    "status": "UNAVAILABLE"
  }
}
*/