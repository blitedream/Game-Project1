using UnityEngine;

public sealed class RotatingDepthMarker : MonoBehaviour
{
    public float speed = 45f;

    private void Update()
    {
        transform.Rotate(new Vector3(0.35f, 1f, 0.2f), speed * Time.deltaTime, Space.World);
    }
}
