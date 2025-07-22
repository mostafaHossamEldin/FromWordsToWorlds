using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TreeEditor;
using UnityEditor;
using UnityEngine;

public class ZoneNode
{
    public string name { get; set; }
    public string translatedName { get; set; }
    public Dictionary<string, ZoneNode> children { get; set; }
    public Dictionary<string, ZoneNode> neighbors { get; set; }
    public List<Connection> connections { get; set; }
    public ZoneNode parent { get; set; }
    public float size { get; set; }
    public float height { get; set; }
    public string side { get; set; }
    public string type { get; set; }
    public string floorTextureName { get; set; }
    public string wallsTextureName { get; set; }
    public Material floorMaterial { get; set; }
    public Material wallsMaterial { get; set; }
    public GameObject platform { get; set; }
    public Dictionary<string, Vector3> objects { get; set; }
    public Dictionary<string, AssetDetails> assets { get; set; }

    public ZoneNode(string name, string side = "", float size = 0, float height = 0)
    {
        this.name = name;
        children = new Dictionary<string, ZoneNode>();
        neighbors = new Dictionary<string, ZoneNode>();
        connections = new List<Connection>();
        this.size = size;
        this.side = side;
        this.height = height + 1;
        objects = new Dictionary<string, Vector3>();
        assets = new Dictionary<string, AssetDetails>();
        wallsMaterial = TextureSelector.defaultMaterial;
        floorMaterial = TextureSelector.defaultMaterial;
    }

    public void AddChild(ZoneNode child)
    {
        children.Add(child.name, child);
        child.parent = this;
        ZoneNode curr = this;
        while (curr != null)
        {
            curr.size += child.size;
            curr = curr.parent;
        }
    }

    public static ZoneNode GetZoneNodeFromString(string jsonString)
    {
        Dictionary<string, object> searchDict = GeneralScript.StringToDict(jsonString);
        return GetZoneNodeFromDict(searchDict);
    }

    private static ZoneNode GetZoneNodeFromDict(Dictionary<string, object> dict)
    {
        if (dict.Count == 0) return null;
        if (dict.Count > 1)
        {
            Debug.LogWarning("Getting ZoneNode, Dictionary has more than one key, only the first key will be used");
        }

        string zoneName = (new List<string>(dict.Keys))[0];
        ZoneNode zone = new ZoneNode(zoneName);
        Dictionary<string, object> childDict = (Dictionary<string, object>)dict[zoneName];

        if (childDict.ContainsKey("size") || childDict.ContainsKey("type")) //A Leaf Node
        {
            zone.name = zoneName;
            zone.size = float.Parse(childDict["size"].ToString());
            zone.height = float.Parse(childDict["height"].ToString());
            if(zone.height > 2)
                zone.height++;
            if(childDict["translatedName"] != null)
                zone.translatedName = childDict["translatedName"].ToString();
            zone.type = childDict["type"].ToString();
            zone.floorTextureName = childDict["floor"].ToString();
            if (childDict.ContainsKey("walls"))
                zone.wallsTextureName = childDict["walls"].ToString();
            Dictionary<string, object> objects = (Dictionary<string, object>)childDict["objects"];
            foreach(KeyValuePair<string, object> obj in objects)
            {
                string objectName = obj.Key;
                Vector3 objectDimensions = GeneralScript.StringToVector3(obj.Value.ToString());
                zone.objects.Add(objectName, objectDimensions);
            }
        }
        else
        {
            foreach (KeyValuePair<string, object> child in childDict)
            {
                if (child.Key != "side")
                {
                    ZoneNode childZone = GetZoneNodeFromDict(new Dictionary<string, object> { { child.Key, child.Value } });
                    zone.AddChild(childZone);
                }
            }
        }

        if (childDict.ContainsKey("side"))
            zone.side = childDict["side"].ToString();

        return zone;
    }

    public override string ToString()
    {
        return GetExtendedString(0);
    }

    public string GetExtendedString(int i)
    {
        string result = new string(' ', i) + name;
        if (children.Count > 0)
        {
            foreach (KeyValuePair<string, ZoneNode> child in children)
            {
                result += "\n" + child.Value.GetExtendedString(i + 4);
            }
        }
        foreach (KeyValuePair<string, AssetDetails> obj in assets)
        {
            result +=
                $"\n{new string(' ', i + 4)}{obj.Key}: {{" +
                $"\n{new string(' ', i + 8)}name: {obj.Value.name}" +
                $"\n{new string(' ', i + 8)}uid: {obj.Value.uid}" +
                $"\n{new string(' ', i + 8)}author: {obj.Value.userUsername}" +
                $"\n{new string(' ', i + 8)}license: {obj.Value.license}" +
                $"\n{new string(' ', i + 8)}categories: {obj.Value.categories}" +
                $"\n{new string(' ', i + 8)}tags: {obj.Value.tags}" +
                $"\n{new string(' ', i + 4)}}}";
        }
        return result;
    }

    public string GetExtendedName()
    {
        return (this.parent != null ? this.parent.GetExtendedName() + "'s " : "") + name;
    }

    public ZoneNode GetZoneNode(string extendedName)
    {
        if (extendedName == name)
        {
            return this;
        }
        Stack<ZoneNode> stack = new Stack<ZoneNode>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            ZoneNode curr = stack.Pop();
            if (curr.GetExtendedName() == extendedName)
            {
                return curr;
            }
            foreach (KeyValuePair<string, ZoneNode> child in curr.children)
            {
                stack.Push(child.Value);
            }
        }
        return null;
    }

    public List<ZoneNode> GetLeafNodes()
    {
        if (children.Count == 0)
        {
            return new List<ZoneNode> { this };
        }
        List<ZoneNode> leafs = new List<ZoneNode>();
        foreach (KeyValuePair<string, ZoneNode> child in children)
        {
            leafs.AddRange(child.Value.GetLeafNodes());
        }
        return leafs;
    }

    public bool AllHasConnections()
    {
        List<ZoneNode> leafs = GetLeafNodes();
        ZoneNode origin = leafs[0];
        for(int i = 1; i < leafs.Count; i++)
        {
            if (!origin.HasPath(leafs[i]))
                return false;
        }
        return true;
    }

    private bool HasPath(ZoneNode zoneNode, List<ZoneNode> visited = null)
    {
        if(visited == null)
            visited = new List<ZoneNode>();

        if (visited.Contains(this))
            return false;

        visited.Add(this);
        if (this == zoneNode)
            return true;
        foreach (Connection connection in connections)
        {
            ZoneNode neighbor = connection.GetNeighbor(this);
            if (neighbor == null)
                continue;
            if (connection.connectionType != ConnectionType.Wall && neighbor.HasPath(zoneNode, visited))
                return true;
        }
        return false;
    }
}