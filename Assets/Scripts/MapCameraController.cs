using UnityEngine;

public sealed class MapCameraController : MonoBehaviour
{
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 focusPosition;
    private Quaternion focusRotation;
    private bool focused;
    private Camera mapCamera;
    private float overviewFieldOfView;
    private float focusFieldOfView = 38f;

    public bool IsFocused => focused;

    private void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        mapCamera = GetComponent<Camera>();
        overviewFieldOfView = mapCamera != null ? mapCamera.fieldOfView : 40f;
    }

    public void FocusOn(Vector3 target)
    {
        focused = true;
        focusFieldOfView = 38f;
        focusPosition = target + new Vector3(0f, 9.5f, -8.5f);
        focusRotation = Quaternion.LookRotation((target + Vector3.up * 0.3f) - focusPosition);
    }

    public void ExitFocus()
    {
        focused = false;
        LevelSelectNode.ClearSelection();
    }

    public void ZoomIn() => AdjustZoom(1f);
    public void ZoomOut() => AdjustZoom(-1f);

    private void AdjustZoom(float direction)
    {
        if (focused)
        {
            focusFieldOfView = Mathf.Clamp(focusFieldOfView - direction * 4f, 24f, 46f);
            return;
        }

        startPosition += startRotation * Vector3.forward * direction * 2.2f;
        startPosition.y = Mathf.Clamp(startPosition.y, 22f, 34f);
    }

    private void Update()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        if (focused)
        {
            transform.position = Vector3.Lerp(transform.position, focusPosition, Time.deltaTime * 3.8f);
            transform.rotation = Quaternion.Slerp(transform.rotation, focusRotation, Time.deltaTime * 4.5f);
            if (mapCamera != null)
                mapCamera.fieldOfView = Mathf.Lerp(mapCamera.fieldOfView, focusFieldOfView, Time.deltaTime * 4f);
            return;
        }

        if (Input.GetMouseButtonDown(0) && !MapMissionOverlay.IsPointerOverOverlay(Input.mousePosition))
        {
            Ray ray = GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                LevelSelectNode node = hit.collider.GetComponentInParent<LevelSelectNode>();
                if (node != null)
                {
                    node.Select();
                    return;
                }
            }
        }

        float x = (Input.mousePosition.x / Mathf.Max(1f, Screen.width) - 0.5f) * 1.7f;
        float y = (Input.mousePosition.y / Mathf.Max(1f, Screen.height) - 0.5f) * 0.8f;
        AdjustZoom(Input.mouseScrollDelta.y * 0.57f);
        transform.position = Vector3.Lerp(transform.position, startPosition + new Vector3(x, y, 0f), Time.deltaTime * 3f);
        transform.rotation = Quaternion.Slerp(transform.rotation, startRotation, Time.deltaTime * 3f);
        if (mapCamera != null)
            mapCamera.fieldOfView = Mathf.Lerp(mapCamera.fieldOfView, overviewFieldOfView, Time.deltaTime * 5f);
    }
}
