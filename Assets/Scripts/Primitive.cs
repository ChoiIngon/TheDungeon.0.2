using System.Collections.Generic;
using UnityEngine;

static class Primitive
{
    private static Material templateMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
    private static Dictionary<Color, Material> materialCache = new Dictionary<Color, Material>();
    private static GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Color color, Transform parent)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = scale;
        GameObject.Destroy(obj.GetComponent<Collider>());
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        { 
            renderer.sharedMaterial = CreateMaterial(color, name); 
        }
            
        return obj;
    }

    public static GameObject CreateCube(string name, Vector3 localPos, Vector3 scale, Color color, Transform parent)
    {
        return CreatePrimitive(PrimitiveType.Cube, name, localPos, scale, color, parent);
    }

    public static GameObject CreateSphere(string name, Vector3 localPos, Vector3 scale, Color color, Transform parent)
    {
        return CreatePrimitive(PrimitiveType.Sphere, name, localPos, scale, color, parent);
    }

    public static GameObject CreateCylinder(string name, Vector3 localPos, Vector3 scale, Color color, Transform parent)
    {
        return CreatePrimitive(PrimitiveType.Cylinder, name, localPos, scale, color, parent);
    }

    public static Material CreateMaterial(Color color, string name)
    {
        if (null == templateMaterial)
        {
            return null;
        }

        if (true == materialCache.TryGetValue(color, out Material cachedMaterial))
        {
            return cachedMaterial;
        }

        Material mat = new Material(templateMaterial);
        mat.name = name;

        if (true == mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (true == mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
        else
        {
            mat.color = color;
        }

        if (true == mat.HasProperty("_Glossiness"))
        {
            mat.SetFloat("_Glossiness", 0.0f);
        }

        if (true == mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", 0.0f);
        }

        materialCache[color] = mat;
        return mat;
    }
}
