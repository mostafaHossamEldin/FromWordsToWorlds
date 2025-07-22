using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public enum ConnectionType
{
    Open,
    Door,
    Wall
}

public class Connection // Only supports 2 zones
{
    private static Vector2 tilingPerUnit = new Vector2(1f, 1f);
    public static float depth = 0.05f;
    public static float doorWidth = 1.0f, doorHeight = 2.0f;

    public ConnectionType connectionType;
    public List<ZoneNode> SharedZones;
    public float x, z, width, height;
    public Orientation orientation;

    public Connection(float x, float z, float width, float height, Orientation orientation, List<ZoneNode> sharedZones = null)
    {
        this.x = x;
        this.z = z;
        this.width = width;
        this.height = height;
        this.orientation = orientation;
        SharedZones = (sharedZones != null ? sharedZones : new List<ZoneNode>());
        connectionType = ConnectionType.Wall;
    }

    public string SharedZonesToString()
    {
        string result = $"{string.Join("&", SharedZones.Select(s=>s.GetExtendedName()))}";

        return result;
    }

    private void ApplyMaterial(GameObject wall)
    {
        float wallWidth = orientation == Orientation.Horizontal ? wall.transform.localScale.x : wall.transform.localScale.z;
        float wallHeight = wall.transform.localScale.y;

        MeshRenderer renderer = wall.GetComponent<MeshRenderer>();

        Mesh mesh = wall.GetComponent<MeshFilter>().mesh;

        mesh.subMeshCount = 6;

        var triangles = mesh.triangles;
        for (int i = 0; i < 6; i++)
        {
            mesh.SetTriangles(triangles.Skip(i * 6).Take(6).ToArray(), i);
        }
        if (SharedZones.Select(zone => zone.wallsMaterial).Where(t => t is not null).Count() > 1)
        {
            Material material1 = new Material(SharedZones[0].wallsMaterial);
            Material material2 = new Material(SharedZones[1].wallsMaterial);

            float mat1WidthRatio = material1.mainTexture.width / material1.mainTexture.height;
            float mat2WidthRatio = material2.mainTexture.width / material2.mainTexture.height;

            if (material1 != null)
                material1.SetTextureScale("_MainTex", new Vector2(wallWidth * tilingPerUnit.x, wallHeight * tilingPerUnit.y * mat1WidthRatio));

            if (material2 != null)
                material2.SetTextureScale("_MainTex", new Vector2(wallWidth * tilingPerUnit.x, wallHeight * tilingPerUnit.y * mat2WidthRatio));

            Vector3 pos1 = SharedZones[0].platform.transform.position;
            Vector3 pos2 = SharedZones[1].platform.transform.position;

            renderer.materials = new Material[] {
            pos1.z > pos2.z ? material1 : material2, // front
            material1, // bottom
            pos1.z < pos2.z ? material1 : material2, // back
            material2, // top
            pos1.x <= pos2.x ? material1 : material2, // left
            pos1.x > pos2.x ? material1 : material2}; // right
        }
        else if (SharedZones.Select(zone => zone.wallsMaterial).Where(t => t != null).Count() == 1)
        {
            Material material = new Material(SharedZones[0].wallsMaterial);
            float matWidthRatio = material.mainTexture.width / material.mainTexture.height;
            if (material != null)
                material.SetTextureScale("_MainTex", new Vector2(wallWidth * tilingPerUnit.x, wallHeight * tilingPerUnit.y * matWidthRatio));
            renderer.materials = new Material[] { material, material, material, material, material, material };
        }
    }

    private GameObject BuildDoorWall(GameObject parent)
    {

        GameObject doorWall = new GameObject();
        if (width / 2f - doorWidth / 2f > 0)
        {
            GameObject wall1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall1.transform.localScale = new Vector3(Math.Max(0, width / 2f - doorWidth / 2f), height, depth);
            wall1.transform.position = new Vector3(
                x - (orientation == Orientation.Horizontal ? width / 4f + doorWidth / 4f : 0),
                height / 2 + parent.transform.position.y,
                z - (orientation == Orientation.Vertical ? width / 4f + doorWidth / 4f : 0));
            if (orientation == Orientation.Vertical)
                wall1.transform.localScale = new Vector3(wall1.transform.localScale.z, wall1.transform.localScale.y, wall1.transform.localScale.x);
            wall1.transform.parent = parent.transform;
            ApplyMaterial(wall1);

            GameObject wall2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall2.transform.localScale = new Vector3(Math.Max(0, width / 2f - doorWidth / 2f), height, depth);
            wall2.transform.position = new Vector3(
                x + (orientation == Orientation.Horizontal ? width / 4f + doorWidth / 4f : 0),
                height / 2 + parent.transform.position.y,
                z + (orientation == Orientation.Vertical ? width / 4f + doorWidth / 4f : 0));
            if (orientation == Orientation.Vertical)
                wall2.transform.localScale = new Vector3(wall2.transform.localScale.z, wall2.transform.localScale.y, wall2.transform.localScale.x);
            wall2.transform.parent = parent.transform;
            ApplyMaterial(wall2);

            wall1.transform.parent = doorWall.transform;
            wall2.transform.parent = doorWall.transform;
        }
        GameObject wall3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall3.transform.localScale = new Vector3(Math.Min(doorWidth, width), height - doorHeight, depth);
        wall3.transform.position = new Vector3(
            x,
            height / 2 + parent.transform.position.y + doorHeight/2f,
            z);
        if (orientation == Orientation.Vertical)
            wall3.transform.localScale = new Vector3(wall3.transform.localScale.z, wall3.transform.localScale.y, wall3.transform.localScale.x);
        wall3.transform.parent = parent.transform;
        ApplyMaterial(wall3);

        wall3.transform.parent = doorWall.transform;

        doorWall.name = SharedZonesToString();
        doorWall.transform.parent = parent.transform;

        return doorWall;
    }

    private static bool HasGoodAlternative(ZoneNode node)
    {
        if(node.connections.Where(c => c.connectionType != ConnectionType.Wall && c.width >= 1.5f * doorWidth).Count() > 0)
            return true;
        return false;
    }

    public void Load(GameObject? parent)
    {
        if (connectionType != ConnectionType.Wall && width < doorWidth * 1.1f && HasGoodAlternative(SharedZones[0]) && HasGoodAlternative(SharedZones[1]))
        {
            connectionType = ConnectionType.Wall;
        }

        if (connectionType == ConnectionType.Open)
        {
            if (Math.Abs(SharedZones[0].height - SharedZones[1].height) > 0.1f)
            {
                float higherHeight = Math.Max(SharedZones[0].height, SharedZones[1].height);
                float lowerHeight = Math.Min(SharedZones[0].height, SharedZones[1].height);
                if(lowerHeight < 1.5f) return;
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.localScale = new Vector3(width, higherHeight - lowerHeight, depth);
                if (orientation == Orientation.Vertical)
                    wall.transform.localScale = new Vector3(depth, higherHeight - lowerHeight, width);
                wall.transform.position = new Vector3(x, lowerHeight + (higherHeight - lowerHeight)/2f + parent.transform.position.y, z);
                wall.name = SharedZonesToString();
                wall.transform.parent = parent.transform;
                ApplyMaterial(wall);
            }
        }
        else if (connectionType == ConnectionType.Wall)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.localScale = new Vector3(width, height, depth);
            wall.transform.position = new Vector3(x, height / 2 + parent.transform.position.y, z);
            wall.name = SharedZonesToString();
            if (orientation == Orientation.Vertical)
                wall.transform.localScale = new Vector3(depth, height, width);
            wall.transform.parent = parent.transform;
            ApplyMaterial(wall);
        }
        else
        {
            GameObject doorWall = BuildDoorWall(parent);
        }
    }

    internal ZoneNode GetNeighbor(ZoneNode zoneNode)
    {
        if (SharedZones.Count < 2)
            return null;
        if (SharedZones[0] == zoneNode)
            return SharedZones[1];
        if (SharedZones[1] == zoneNode)
            return SharedZones[0];
        return null;
    }
}
