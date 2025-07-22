using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityGLTF;

public class GeneralScript
{
    public static void SetExactPosition(GameObject gameObject, Vector3 position)
    {
        Vector3 objectCenter = GetAllBounds(gameObject).center;
        Vector3 offset = objectCenter - position;
        gameObject.transform.position -= offset;
    }

    public static Bounds GetBounds(GameObject gameObject)
    {
        Bounds bounds = new Bounds(gameObject.transform.position, Vector3.zero);
        Renderer renderer = gameObject.GetComponent<Renderer>();

        bounds.Encapsulate(renderer.bounds);
        
        return bounds;
    }
    
    public static Bounds GetAllBounds(GameObject gameObject)
    {
        Bounds bounds = new Bounds(gameObject.transform.position, Vector3.zero);

        Stack<GameObject> stack = new Stack<GameObject>();
        stack.Push(gameObject);
        while (stack.Count > 0)
        {
            GameObject go = stack.Pop();
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            renderers.Prepend(go.GetComponent<Renderer>());
            foreach (Renderer r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }
            foreach (Transform child in go.transform)
            {
                stack.Push(child.gameObject);
            }
        }

        return bounds;
    }

    public static Vector3 GetGeneralScale(Transform transform)
    {
        Vector3 scale = transform.localScale;
        Transform current = transform.parent;

        while (current != null)
        {
            scale = Vector3.Scale(scale, current.localScale);
            current = current.parent;
        }

        return scale;
    }

    public static Dictionary<string, object> StringToDict(string input)
    {
        int hh = 0;
        while (hh < input.Length && input[hh] != '{')
        {
            hh++;
        }
        input = input.Substring(hh);
        Dictionary<string, object> searchDict = new Dictionary<string, object>();
        input = input.Replace("\n", "");
        input = input.Replace("\r", "");
        input = input.Replace("\t", "");
        input.Trim();
        if (string.IsNullOrEmpty(input))
        {
            Debug.LogWarning("Input is null or empty");
            return searchDict;
        }

        if (input[0] != '{')
        {
            Debug.LogWarning("Input is not a dictionary");
            return null;
        }


        int start = 1;
        for (int i = 1; i < input.Length - 1; i++)
        {
            if (input[i].Equals(':'))
            {
                string key = input.Substring(start, i - start).Trim();
                key = key.Substring(1, key.Length - 2);
                if (searchDict.ContainsKey(key))
                {
                    int num = 2;
                    while (searchDict.ContainsKey(key + num))
                        num++;
                    key += num;
                }
                int j = i + 1;
                int count = 0;
                while (count >= 0 && (input[j] != ',' || count > 0))
                {
                    if (input[j] == '{' || input[j] == '[' || input[j] == '(')
                    {
                        count++;
                    }
                    else if (input[j] == '}' || input[j] == ']' || input[j] == ')')
                    {
                        count--;
                    }
                    j++;
                }
                string value = input.Substring(i + 1, j - i - 1 - (count < 0 ? 1 : 0)).Trim();
                //Debug.Log("Key: " + key + "\nValue: " + value);
                if (value[0] == '"')
                {
                    value = value.Substring(1, value.Length - 2);
                }
                //Debug.Log("Key: " + key);
                if (value[0] == '{')
                {
                    if(searchDict.ContainsKey(key)){
                        int num = 1;
                        while(searchDict.ContainsKey(key + num))
                            num++;
                        searchDict.Add(key + num, StringToDict(value));
                    }
                    else
                    {
                        searchDict.Add(key, StringToDict(value));
                    }
                }
                else if ((value[0] == '(' || value[0] == '[') && value.Contains(","))
                {
                    string[] values = value.Substring(1, value.Length - 2).Split(',');
                    object vector = (values.Length == 3 ? GeneralScript.StringToVector3(value) : GeneralScript.StringToVector2(value));
                    searchDict.Add(key, vector);
                }
                else
                {
                    searchDict.Add(key, value);
                }
                i = j;
                start = j + 1;
            }
        }


        return searchDict;
    }

    public static string DictToString(Dictionary<string, object> dict)
    {
        return GetExtendedString(dict, 0);
    }

    private static string GetExtendedString(Dictionary<string, object> dict, int i)
    {
        string result = "";
        foreach (KeyValuePair<string, object> entry in dict)
        {
            result += new string(' ', i*3);
            result += entry.Key + ": ";
            if (entry.Value is Dictionary<string, object>)
            {
                result += $"{{\n{GetExtendedString((Dictionary<string, object>)entry.Value, i + 1)}{new string(' ', i*3)}}}\n";
            }
            else if(entry.Value is Vector3)
                result += string.Format("({0}, {1}, {2})\n", ((Vector3)entry.Value).x, ((Vector3)entry.Value).y, ((Vector3)entry.Value).z);
            else
            {
                result += entry.Value + "\n";
            }
        }
        return result;
    }

    public static Vector3 DivideVectors(Vector3 vectorA, Vector3 vectorB)
    {
        return new Vector3((float)vectorA.x / (float)vectorB.x, (float)vectorA.y / (float)vectorB.y, (float)vectorA.z / (float)vectorB.z);
    }

    public static Vector3 StringToVector3(string input)
    {
        input.Trim();
        string[] values = input.Substring(1, input.Length - 2).Split(',');
        return new Vector3(SolveEquation(values[0]), SolveEquation(values[1]), SolveEquation(values[2]));
    }

    public static Vector2 StringToVector2(string input)
    {
        input.Trim();
        string[] values = input.Substring(1, input.Length - 2).Split(',');
        return new Vector2(SolveEquation(values[0]), SolveEquation(values[1]));
    }

    public static void RescaleAsset(GameObject model, Vector3 referenceBounds)
    {
        Vector3 bounds = GeneralScript.GetAllBounds(model).extents * 2f; // For problems in Unity's bounds calculation

        // Rotated Reference?
        float minRef1 = Mathf.Min(referenceBounds.x, referenceBounds.y, referenceBounds.z);
        float minRef2 = Mathf.Max(referenceBounds.x, referenceBounds.y, referenceBounds.z);
        float minRef3 = referenceBounds.x + referenceBounds.y + referenceBounds.z - minRef1 - minRef2;
        referenceBounds = new Vector3(minRef1, minRef2, minRef3);
        float min1 = Mathf.Min(bounds.x, bounds.y, bounds.z);
        float min2 = Mathf.Max(bounds.x, bounds.y, bounds.z);
        float min3 = bounds.x + bounds.y + bounds.z - min1 - min2;
        float scale = (minRef1/min1 + minRef2 / min2 + minRef3 / min3) / 3f;
        
        model.transform.localScale = new Vector3(scale, scale, scale);
    }

    public static string Vector3ToString(Vector3 vector)
    {
        return $"({vector.x}, {vector.y}, {vector.z})";
    }

    public static float SolveEquation(string equation)
    {
        string[] parts = equation.Split(' ');
        Stack<string> stack = new Stack<string>();
        for (int i = 0; i < parts.Length; i++)
        {
            if (IsOperator(parts[i]))
            {
                stack.Push(Calculate(float.Parse(stack.Pop()), float.Parse("" + stack.Pop()), parts[i]).ToString());
            }
            else
            {
                stack.Push(parts[i]);
            }
        }
        return float.Parse(stack.Peek());
    }

    private static bool IsOperator(string s)
    {
        if (s.Equals("*") || s.Equals("/") || s.Equals("+") || s.Equals("-")
                 || s.Equals("^"))
            return true;
        return false;
    }
    private static double Calculate(double a, double b, String s)
    {
        if (s.Equals("-"))
            return b - a;
        else if (s.Equals("+"))
            return b + a;
        else if (s.Equals("*"))
            return b * a;
        else if (s.Equals("/"))
            return b / a;
        else if (s.Equals("^"))
        {
            return Math.Pow(b, a);
        }
        return -1;
    }
}
