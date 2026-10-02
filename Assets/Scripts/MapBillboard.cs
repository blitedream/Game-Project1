using UnityEngine;

public sealed class MapBillboard : MonoBehaviour
{
    private void LateUpdate()
    {
        if (Camera.main != null)
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
    }
}
