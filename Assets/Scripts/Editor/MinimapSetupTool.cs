using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Herramienta de Editor para crear o reconstruir el minimapa en la escena Game.
/// Ejecuta: Tools > A-Toda-Olla > Setup Minimap
/// </summary>
public static class MinimapSetupTool
{
    [MenuItem("Tools/A-Toda-Olla/Setup Minimap")]
    public static void Setup()
    {
        // --- 1. Textura del circulo en 512x512 (alta definicion) ---
        Texture2D circleTex = new Texture2D(512, 512, TextureFormat.ARGB32, false);
        circleTex.name = "MinimapCircle_Procedural";
        float cCenter = 255.5f;
        float cOuterR = 253f;
        float cInnerR = 241f; // Borde oscuro elegante
        for (int y = 0; y < 512; y++)
        {
            for (int x = 0; x < 512; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cCenter, cCenter));
                if (dist > cOuterR)
                    circleTex.SetPixel(x, y, Color.clear);
                else if (dist > cInnerR)
                    circleTex.SetPixel(x, y, new Color(0.08f, 0.08f, 0.08f, 0.95f)); // Borde negro/oscuro
                else
                    circleTex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f)); // Centro blanco para la mascara
            }
        }
        circleTex.Apply();
        Sprite circleSprite = Sprite.Create(circleTex, new Rect(0, 0, 512, 512), new Vector2(0.5f, 0.5f), 100f);

        // --- 2. Textura del triangulo del jugador ---
        Texture2D triTex = new Texture2D(64, 64, TextureFormat.ARGB32, false);
        triTex.name = "PlayerIcon_Procedural";
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float nx = (x - 31.5f) / 31.5f;
                float ny = y / 63f;
                if (ny >= 0.1f && Mathf.Abs(nx) <= (1f - ny * 0.75f) * 0.9f)
                    triTex.SetPixel(x, y, Color.white);
                else
                    triTex.SetPixel(x, y, Color.clear);
            }
        }
        triTex.Apply();
        Sprite triSprite = Sprite.Create(triTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 32f);

        // --- Camara del minimapa ---
        GameObject oldCam = GameObject.Find("MinimapCamera");
        float preservedSize = 20f;
        Vector3 preservedPos = new Vector3(0f, 150f, 0f);
        if (oldCam != null)
        {
            Camera existingCam = oldCam.GetComponent<Camera>();
            if (existingCam != null && existingCam.orthographicSize > 0f)
                preservedSize = existingCam.orthographicSize;
            if (oldCam.transform.position.y > 0f)
                preservedPos = oldCam.transform.position;
            Object.DestroyImmediate(oldCam);
        }

        GameObject camGO = new GameObject("MinimapCamera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = preservedSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.15f, 0.12f, 1f);
        cam.depth = -2;
        cam.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));
        camGO.transform.position = preservedPos;
        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // --- HUDCanvas ---
        Canvas hudCanvas = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>())
            if (c.name == "HUDCanvas") { hudCanvas = c; break; }

        if (hudCanvas == null)
        {
            Debug.LogError("[MinimapSetup] No se encontro HUDCanvas en la escena.");
            return;
        }

        Transform oldContainer = hudCanvas.transform.Find("MinimapContainer");
        if (oldContainer != null) Object.DestroyImmediate(oldContainer.gameObject);

        Transform oldMgr = hudCanvas.transform.Find("MinimapManager");
        if (oldMgr != null) Object.DestroyImmediate(oldMgr.gameObject);

        // --- MinimapContainer (Tamano grande 280x280) ---
        GameObject containerGO = new GameObject("MinimapContainer", typeof(RectTransform));
        containerGO.transform.SetParent(hudCanvas.transform, false);
        RectTransform containerRect = containerGO.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(1f, 0f);
        containerRect.anchorMax = new Vector2(1f, 0f);
        containerRect.pivot = new Vector2(1f, 0f);
        containerRect.sizeDelta = new Vector2(280f, 280f);
        containerRect.anchoredPosition = new Vector2(-24f, 24f);

        // --- MinimapBorder (fondo y borde negro del circulo) ---
        GameObject borderGO = new GameObject("MinimapBorder", typeof(RectTransform));
        borderGO.transform.SetParent(containerGO.transform, false);
        RectTransform borderRect = borderGO.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero; borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero; borderRect.offsetMax = Vector2.zero;
        Image borderImg = borderGO.AddComponent<Image>();
        borderImg.sprite = circleSprite;
        borderImg.color = Color.white;
        borderImg.raycastTarget = false;

        // --- MinimapMask (recorta la camara al circulo) ---
        GameObject maskGO = new GameObject("MinimapMask", typeof(RectTransform));
        maskGO.transform.SetParent(containerGO.transform, false);
        RectTransform maskRect = maskGO.GetComponent<RectTransform>();
        maskRect.anchorMin = Vector2.zero; maskRect.anchorMax = Vector2.one;
        maskRect.offsetMin = new Vector2(8f, 8f);
        maskRect.offsetMax = new Vector2(-8f, -8f);
        Image maskImg = maskGO.AddComponent<Image>();
        maskImg.sprite = circleSprite;
        maskImg.raycastTarget = false;
        Mask maskComp = maskGO.AddComponent<Mask>();
        maskComp.showMaskGraphic = false;

        // --- MinimapView (RawImage que muestra el RenderTexture) ---
        GameObject viewGO = new GameObject("MinimapView", typeof(RectTransform));
        viewGO.transform.SetParent(maskGO.transform, false);
        RectTransform viewRect = viewGO.GetComponent<RectTransform>();
        viewRect.anchorMin = Vector2.zero; viewRect.anchorMax = Vector2.one;
        viewRect.offsetMin = Vector2.zero; viewRect.offsetMax = Vector2.zero;
        RawImage rawImage = viewGO.AddComponent<RawImage>();
        rawImage.raycastTarget = false;

        // --- PlayerIcon (triangulo blanco en el centro, 24x24) ---
        GameObject playerIconGO = new GameObject("PlayerIcon", typeof(RectTransform));
        playerIconGO.transform.SetParent(containerGO.transform, false);
        RectTransform playerIconRect = playerIconGO.GetComponent<RectTransform>();
        playerIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        playerIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        playerIconRect.pivot = new Vector2(0.5f, 0.5f);
        playerIconRect.sizeDelta = new Vector2(24f, 24f);
        playerIconRect.anchoredPosition = Vector2.zero;
        Image playerIconImg = playerIconGO.AddComponent<Image>();
        playerIconImg.sprite = triSprite;
        playerIconImg.color = Color.white;
        playerIconImg.raycastTarget = false;

        // --- ObjectiveMarker (cuadrado amarillo estilo GTA, 20x20) ---
        GameObject markerGO = new GameObject("ObjectiveMarker", typeof(RectTransform));
        markerGO.transform.SetParent(containerGO.transform, false);
        RectTransform markerRect = markerGO.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(20f, 20f);
        markerRect.anchoredPosition = Vector2.zero;
        Image markerBorder = markerGO.AddComponent<Image>();
        markerBorder.color = Color.black;
        markerBorder.raycastTarget = false;

        GameObject fillGO = new GameObject("MarkerFill", typeof(RectTransform));
        fillGO.transform.SetParent(markerGO.transform, false);
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2.5f, 2.5f);
        fillRect.offsetMax = new Vector2(-2.5f, -2.5f);
        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(1f, 0.843f, 0f); // #FFD700
        fillImg.raycastTarget = false;

        // --- MinimapController en MinimapCamera ---
        MinimapController ctrl = camGO.AddComponent<MinimapController>();
        ctrl.minimapCamera = cam;
        ctrl.minimapView = rawImage;
        ctrl.playerIcon = playerIconRect;
        ctrl.objectiveMarker = markerRect;
        ctrl.worldRadius = preservedSize;
        ctrl.uiRadius = 0f; // 0 = automatico segun el tamano del contenedor
        ctrl.cameraHeight = preservedPos.y;
        ctrl.renderTextureSize = 512;

        // Marcar la escena como modificada
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"[MinimapSetup] Minimapa UI grande (280x280) configurado correctamente en MinimapCamera (Zoom Size = {preservedSize}).");
    }
}
