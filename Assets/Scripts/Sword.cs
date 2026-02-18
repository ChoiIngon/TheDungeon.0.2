using UnityEngine;

public class Sword : MonoBehaviour
{
    public void Build(Transform handParent)
    {
        transform.SetParent(handParent, false);
        transform.localRotation = Quaternion.Euler(0, 90, 90);
        transform.localPosition = new Vector3(0, 0, 1);

        Primitive.CreateCube("Blade", new Vector3(0, 0.6f, 0), new Vector3(0.3f, 3.6f, 0.3f), Color.gray, this.transform);
        Primitive.CreateCube("Crossguard", Vector3.zero, new Vector3(0.9f, 0.3f, 0.3f), Color.gray, this.transform);
        Primitive.CreateCube("Hilt", new Vector3(0, -0.2f, 0), new Vector3(0.3f, 0.9f, 0.3f), Color.gray, this.transform);
    }
}
