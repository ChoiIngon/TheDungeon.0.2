using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class Scythe : MonoBehaviour
{
    public Color scytheHandleColor = new Color32(101, 67, 33, 255);
    public Color scytheBladeColor = new Color32(200, 200, 200, 255);

    public void Build(Transform parent)
    {
        transform.SetParent(parent, false);
        transform.localPosition = new Vector3(0.55f, 0.4f, 0.77f);
        transform.localRotation = Quaternion.Euler(-20.0f, -70.0f, -55.0f);

        // 낫 자루
        Primitive.CreateCube("ScytheHandle", new Vector3(0, 0, 0), new Vector3(0.3f, 8f, 0.3f), scytheHandleColor, transform);

        // 낫 칼날
        GameObject bladeRoot = new GameObject("BladeRoot");
        bladeRoot.transform.SetParent(transform, false);
        bladeRoot.transform.localPosition = new Vector3(0, 3.6f, 0);

        GameObject bladePart1 = Primitive.CreateCube("BladePart1", new Vector3(1.2f, 0.24f, 0), new Vector3(2.0f, 0.7f, 0.1f), scytheBladeColor, bladeRoot.transform);
        bladePart1.transform.localRotation = Quaternion.Euler(0, 0, 20);

        GameObject bladePart2 = Primitive.CreateCube("BladePart2", new Vector3(2.65f, 0.22f, 0), new Vector3(2.0f, 0.6f, 0.1f), scytheBladeColor, bladeRoot.transform);
        bladePart2.transform.localRotation = Quaternion.Euler(0, 0, -25);
    }
}
