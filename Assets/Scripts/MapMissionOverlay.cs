using UnityEngine;

public sealed class MapMissionOverlay : MonoBehaviour
{
    private GUIStyle header, panel, title, body, accent, button;
    private Texture2D panelTexture, accentTexture;

    private void EnsureStyles()
    {
        if (header != null) return;
        panelTexture = SolidTexture(new Color(0.018f, 0.065f, 0.075f, 0.92f));
        accentTexture = SolidTexture(new Color(0.06f, 0.52f, 0.43f, 0.96f));
        header = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture }, alignment = TextAnchor.MiddleLeft, fontSize = 22, fontStyle = FontStyle.Bold, padding = new RectOffset(28, 20, 10, 10) };
        panel = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture }, padding = new RectOffset(18, 18, 18, 18) };
        title = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, wordWrap = true };
        body = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(0.66f, 0.82f, 0.84f) }, wordWrap = true };
        accent = new GUIStyle(body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.25f, 1f, 0.72f) } };
        button = new GUIStyle(GUI.skin.button) { normal = { background = accentTexture, textColor = Color.white }, hover = { background = accentTexture, textColor = Color.white }, fontSize = 17, fontStyle = FontStyle.Bold };
    }

    private void OnGUI()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        EnsureStyles();
        GUI.Box(new Rect(0f, 0f, Screen.width, 62f), "  OPERATIONS  /  3D TACTICAL MAP", header);
        DrawMapControls();
        LevelSelectNode selected = LevelSelectNode.Current;
        if (selected != null)
        {
            float rightX = Screen.width - 348f;
            GUI.Box(new Rect(rightX, 84f, 330f, 328f), GUIContent.none, panel);
            GUI.Label(new Rect(rightX + 22f, 106f, 286f, 28f), "MISSION " + selected.Number, accent);
            GUI.Label(new Rect(rightX + 22f, 142f, 286f, 58f), selected.DisplayName, title);
            GUI.Label(new Rect(rightX + 22f, 208f, 286f, 62f), selected.Description, body);
            GUI.Label(new Rect(rightX + 22f, 278f, 286f, 25f), selected.Risk, accent);
            float best = GameProgression.GetBestTime(selected.LevelNumber);
            if (best >= 0f)
                GUI.Label(new Rect(rightX + 22f, 304f, 286f, 24f), "BEST  " + GameProgression.FormatTime(best), body);

            if (!selected.IsUnlocked)
                GUI.Label(new Rect(rightX + 22f, 302f, 286f, 28f), $"LOCKED — COMPLETE LEVEL {selected.LevelNumber - 1}", accent);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = selected.IsUnlocked;
            string buttonText = selected.IsUnlocked ? "START MISSION" : "MISSION LOCKED";
            if (GUI.Button(new Rect(rightX + 22f, 334f, 286f, 54f), buttonText, button))
                SceneTransitionManager.LoadLevel(selected.LevelNumber);
            GUI.enabled = previousEnabled;
        }

        string hint = selected == null ? "LMB Enter Mission   /   Mouse Wheel Zoom" : "ESC Close Details";
        GUI.Label(new Rect(24f, Screen.height - 38f, 580f, 24f), hint, body);
    }

    private void DrawMapControls()
    {
        MapCameraController controller = GetComponent<MapCameraController>();
        float x = Screen.width - 330f;
        GUI.Label(new Rect(x, 18f, 52f, 28f), "ZOOM", accent);

        if (GUI.Button(new Rect(x + 58f, 11f, 42f, 40f), "−", button))
            controller?.ZoomOut();
        if (GUI.Button(new Rect(x + 106f, 11f, 42f, 40f), "+", button))
            controller?.ZoomIn();

        string backText = controller != null && controller.IsFocused ? "ESC  CLOSE" : "ESC  RETURN";
        GUI.Label(new Rect(x + 166f, 18f, 146f, 28f), backText, body);
    }

    public static bool IsPointerOverOverlay(Vector2 mousePosition)
    {
        float guiY = Screen.height - mousePosition.y;
        if (guiY <= 66f)
            return true;

        return LevelSelectNode.Current != null && mousePosition.x >= Screen.width - 360f && guiY >= 76f && guiY <= 420f;
    }

    private static Texture2D SolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
