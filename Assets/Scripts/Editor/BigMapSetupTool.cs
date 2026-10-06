using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

/// <summary>
/// Herramienta de Editor para crear y configurar el Mapa Grande utilizando
/// la ilustración de la cartelera del vecindario (Assets/UI/Fondos/FondoMapa.png).
/// Se ejecuta desde: Tools → A-Toda-Olla → Setup Big Map
/// </summary>
public static class BigMapSetupTool
{
    private const string FONDO_MAPA_PATH = "Assets/UI/Fondos/FondoMapa.png";

    [MenuItem("Tools/A-Toda-Olla/Setup Big Map")]
    public static void Setup()
    {
        // ── 1. Eliminar CUALQUIER BigMapCamera previa ──────────────────────
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go != null && go.name == "BigMapCamera" && !EditorUtility.IsPersistent(go))
            {
                Object.DestroyImmediate(go);
            }
        }

        // ── 2. Cargar FondoMapa.png ────────────────────────────────────────
        Sprite fondoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FONDO_MAPA_PATH);
        if (fondoSprite == null)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(FONDO_MAPA_PATH);
            if (tex != null)
            {
                fondoSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            else
            {
                Debug.LogError($"[BigMapSetup] No se pudo encontrar {FONDO_MAPA_PATH}. Asegúrate de que el archivo exista.");
                return;
            }
        }

        // ── 3. Icono del Jugador (Triángulo nítido con borde negro) ────────
        Texture2D triTex = new Texture2D(64, 64, TextureFormat.ARGB32, false);
        triTex.name = "BigMapPlayerIcon_Procedural";
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float nx = (x - 31.5f) / 31.5f;
                float ny = y / 63f;
                bool inOuter = (ny >= 0.08f && Mathf.Abs(nx) <= (1f - ny * 0.75f) * 0.95f);
                bool inInner = (ny >= 0.18f && Mathf.Abs(nx) <= (1f - ny * 0.75f) * 0.78f);

                if (inInner)
                    triTex.SetPixel(x, y, Color.white);
                else if (inOuter)
                    triTex.SetPixel(x, y, new Color(0.1f, 0.1f, 0.1f, 0.95f));
                else
                    triTex.SetPixel(x, y, Color.clear);
            }
        }
        triTex.Apply();
        Sprite triSprite = Sprite.Create(triTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 32f);

        // ── 4. Calcular Centro y Zoom de la Ciudad ─────────────────────────
        GameObject city = GameObject.Find("City");
        Vector3 computedCenter = new Vector3(0f, 250f, 0f);
        float computedSize = 205f;

        if (city != null)
        {
            Renderer[] rends = city.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++)
                    b.Encapsulate(rends[i].bounds);

                computedCenter = new Vector3(b.center.x, 250f, b.center.z);
                // Proporción de la cartelera interior (1334 / 620 ≈ 2.15)
                float aspect = 1334f / 620f;
                float halfH = b.extents.z;
                float halfW = b.extents.x;
                computedSize = Mathf.Max(halfH, halfW / aspect) * 1.08f;
            }
        }

        // ── 5. Crear BigMapCamera ──────────────────────────────────────────
        GameObject camGO = new GameObject("BigMapCamera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = computedSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        // Fondo pizarra cálido para que coincida exactamente con la cartelera de FondoMapa.png
        cam.backgroundColor = new Color(0.22f, 0.20f, 0.18f, 1f);
        cam.depth = -3;
        cam.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));
        camGO.transform.position = computedCenter;
        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.enabled = false;

        // ── 6. Buscar HUDCanvas ────────────────────────────────────────────
        Canvas hudCanvas = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>())
        {
            if (c.name == "HUDCanvas") { hudCanvas = c; break; }
        }

        if (hudCanvas == null)
        {
            Debug.LogError("[BigMapSetup] No se encontró HUDCanvas en la escena.");
            return;
        }

        // Eliminar panel anterior si existe
        Transform oldPanel = hudCanvas.transform.Find("BigMapPanel");
        if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);

        // ── 7. Crear BigMapPanel (Raíz a pantalla completa) ────────────────
        GameObject panelRootGO = new GameObject("BigMapPanel", typeof(RectTransform));
        panelRootGO.transform.SetParent(hudCanvas.transform, false);
        RectTransform panelRootRT = panelRootGO.GetComponent<RectTransform>();
        panelRootRT.anchorMin = Vector2.zero;
        panelRootRT.anchorMax = Vector2.one;
        panelRootRT.offsetMin = Vector2.zero;
        panelRootRT.offsetMax = Vector2.zero;

        CanvasGroup panelGroup = panelRootGO.AddComponent<CanvasGroup>();
        panelGroup.alpha = 1f;
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;

        // Fondo oscuro para letterboxing en pantallas no-16:9
        GameObject dimmerGO = new GameObject("DimmerBackground", typeof(RectTransform));
        dimmerGO.transform.SetParent(panelRootGO.transform, false);
        RectTransform dimmerRT = dimmerGO.GetComponent<RectTransform>();
        dimmerRT.anchorMin = Vector2.zero;
        dimmerRT.anchorMax = Vector2.one;
        dimmerRT.offsetMin = Vector2.zero;
        dimmerRT.offsetMax = Vector2.zero;
        Image dimmerImg = dimmerGO.AddComponent<Image>();
        dimmerImg.color = new Color(0.04f, 0.04f, 0.05f, 0.96f);
        dimmerImg.raycastTarget = false;

        // ── 8. Contenedor de la Ilustración (FondoMapa.png) ────────────────
        // Mantiene la relación de aspecto exacta de la imagen (1672 x 941 ≈ 1.7768)
        GameObject bgGO = new GameObject("BillboardBackground", typeof(RectTransform));
        bgGO.transform.SetParent(panelRootGO.transform, false);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        bgRT.pivot = new Vector2(0.5f, 0.5f);

        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.sprite = fondoSprite;
        bgImg.color = Color.white;
        bgImg.raycastTarget = false;

        AspectRatioFitter bgFitter = bgGO.AddComponent<AspectRatioFitter>();
        bgFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        bgFitter.aspectRatio = 1672f / 941f;

        // ── 9. Viewport dentro de la Cartelera (Anclado a las coordenadas reales) ─
        // Coordenadas medidas de la pizarra: X(171..1505) -> 0.102..0.900, Y(178..798) -> 0.152..0.811
        GameObject viewportGO = new GameObject("MapViewport", typeof(RectTransform));
        viewportGO.transform.SetParent(bgGO.transform, false);
        RectTransform viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin = new Vector2(0.102f, 0.152f);
        viewportRT.anchorMax = new Vector2(0.900f, 0.811f);
        viewportRT.pivot = new Vector2(0.5f, 0.5f);
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;

        // Fondo de respaldo de la pizarra y máscara para que nada se salga del marco metálico
        Image vpBg = viewportGO.AddComponent<Image>();
        vpBg.color = new Color(0.20f, 0.18f, 0.16f, 1f);
        vpBg.raycastTarget = false;

        Mask vpMask = viewportGO.AddComponent<Mask>();
        vpMask.showMaskGraphic = true;

        // BigMapView (RawImage donde se proyecta la cámara en tiempo real)
        GameObject viewGO = new GameObject("BigMapView", typeof(RectTransform));
        viewGO.transform.SetParent(viewportGO.transform, false);
        RectTransform viewRT = viewGO.GetComponent<RectTransform>();
        viewRT.anchorMin = Vector2.zero;
        viewRT.anchorMax = Vector2.one;
        viewRT.offsetMin = Vector2.zero;
        viewRT.offsetMax = Vector2.zero;
        viewRT.pivot = new Vector2(0.5f, 0.5f);
        RawImage rawImage = viewGO.AddComponent<RawImage>();
        rawImage.raycastTarget = false;

        // BigMapPlayerIcon (Icono del jugador, tamaño 28x28)
        GameObject playerIconGO = new GameObject("BigMapPlayerIcon", typeof(RectTransform));
        playerIconGO.transform.SetParent(viewportGO.transform, false);
        RectTransform playerIconRT = playerIconGO.GetComponent<RectTransform>();
        playerIconRT.anchorMin = new Vector2(0.5f, 0.5f);
        playerIconRT.anchorMax = new Vector2(0.5f, 0.5f);
        playerIconRT.pivot = new Vector2(0.5f, 0.5f);
        playerIconRT.sizeDelta = new Vector2(28f, 28f);
        playerIconRT.anchoredPosition = Vector2.zero;
        Image playerIconImg = playerIconGO.AddComponent<Image>();
        playerIconImg.sprite = triSprite;
        playerIconImg.color = Color.white;
        playerIconImg.raycastTarget = false;

        // BigMapObjectiveMarker (Marcador de entrega amarillo con borde negro estilo GTA)
        GameObject markerGO = new GameObject("BigMapObjectiveMarker", typeof(RectTransform));
        markerGO.transform.SetParent(viewportGO.transform, false);
        RectTransform markerRT = markerGO.GetComponent<RectTransform>();
        markerRT.anchorMin = new Vector2(0.5f, 0.5f);
        markerRT.anchorMax = new Vector2(0.5f, 0.5f);
        markerRT.pivot = new Vector2(0.5f, 0.5f);
        markerRT.sizeDelta = new Vector2(24f, 24f);
        markerRT.anchoredPosition = Vector2.zero;

        Image markerBorder = markerGO.AddComponent<Image>();
        markerBorder.color = Color.black;
        markerBorder.raycastTarget = false;

        GameObject markerFillGO = new GameObject("MarkerFill", typeof(RectTransform));
        markerFillGO.transform.SetParent(markerGO.transform, false);
        RectTransform markerFillRT = markerFillGO.GetComponent<RectTransform>();
        markerFillRT.anchorMin = Vector2.zero;
        markerFillRT.anchorMax = Vector2.one;
        markerFillRT.offsetMin = new Vector2(3f, 3f);
        markerFillRT.offsetMax = new Vector2(-3f, -3f);
        Image markerFillImg = markerFillGO.AddComponent<Image>();
        markerFillImg.color = new Color(1f, 0.843f, 0f); // #FFD700
        markerFillImg.raycastTarget = false;

        // ── 10. Header y Footer Estilizados ─────────────────────────────────
        // Barra Superior: Título de la zona y botón para cerrar
        GameObject headerGO = new GameObject("HeaderBar", typeof(RectTransform));
        headerGO.transform.SetParent(bgGO.transform, false);
        RectTransform headerRT = headerGO.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0.102f, 0.825f);
        headerRT.anchorMax = new Vector2(0.900f, 0.950f);
        headerRT.pivot = new Vector2(0.5f, 0.5f);
        headerRT.offsetMin = Vector2.zero;
        headerRT.offsetMax = Vector2.zero;

        // Título estilizado con sombra
        GameObject titleGO = new GameObject("TitleText", typeof(RectTransform));
        titleGO.transform.SetParent(headerGO.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 0f);
        titleRT.anchorMax = new Vector2(0.65f, 1f);
        titleRT.pivot = new Vector2(0f, 0.5f);
        titleRT.offsetMin = new Vector2(10f, 0f);
        titleRT.offsetMax = Vector2.zero;
        TMP_Text titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "MAPA DEL VECINDARIO";
        titleTMP.fontSize = 24f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(1f, 0.96f, 0.90f);
        titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
        titleTMP.raycastTarget = false;

        // Indicador de tecla M para cerrar
        GameObject closeGO = new GameObject("CloseHint", typeof(RectTransform));
        closeGO.transform.SetParent(headerGO.transform, false);
        RectTransform closeRT = closeGO.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(0.65f, 0f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 0.5f);
        closeRT.offsetMin = Vector2.zero;
        closeRT.offsetMax = new Vector2(-10f, 0f);
        TMP_Text closeTMP = closeGO.AddComponent<TextMeshProUGUI>();
        closeTMP.text = "[ M ]  CERRAR MAPA";
        closeTMP.fontSize = 17f;
        closeTMP.fontStyle = FontStyles.Bold;
        closeTMP.color = new Color(1f, 0.85f, 0.2f);
        closeTMP.alignment = TextAlignmentOptions.MidlineRight;
        closeTMP.raycastTarget = false;

        // Leyenda inferior limpia sobre la acera
        GameObject footerGO = new GameObject("FooterBar", typeof(RectTransform));
        footerGO.transform.SetParent(bgGO.transform, false);
        RectTransform footerRT = footerGO.GetComponent<RectTransform>();
        footerRT.anchorMin = new Vector2(0.102f, 0.040f);
        footerRT.anchorMax = new Vector2(0.900f, 0.135f);
        footerRT.pivot = new Vector2(0.5f, 0.5f);
        footerRT.offsetMin = Vector2.zero;
        footerRT.offsetMax = Vector2.zero;

        TMP_Text legendTMP = footerGO.AddComponent<TextMeshProUGUI>();
        legendTMP.text = "🟡 Casa de entrega      ⚪ Tu posición      Presiona [M] o [ESC] para volver";
        legendTMP.fontSize = 14f;
        legendTMP.fontStyle = FontStyles.Normal;
        legendTMP.color = new Color(0.92f, 0.88f, 0.82f);
        legendTMP.alignment = TextAlignmentOptions.Center;
        legendTMP.raycastTarget = false;

        // ── 11. Configurar Controlador ─────────────────────────────────────
        BigMapController ctrl = camGO.AddComponent<BigMapController>();
        ctrl.bigMapCamera = cam;
        ctrl.cityCenter = computedCenter;
        ctrl.orthographicSize = computedSize;
        ctrl.mapPanelGroup = panelGroup;
        ctrl.bigMapView = rawImage;
        ctrl.playerIcon = playerIconRT;
        ctrl.objectiveMarker = markerRT;

        Transform miniContainer = hudCanvas.transform.Find("MinimapContainer");
        if (miniContainer != null)
            ctrl.minimapContainer = miniContainer.GetComponent<RectTransform>();

        ctrl.textureWidth = 1920;
        ctrl.textureHeight = 1080;
        ctrl.fadeDuration = 0.15f;

        // En el editor se deja activo con alpha=1 para poder editarlo en Scene View.
        // Al darle Play, BigMapController.Awake() lo oculta automáticamente.
        panelRootGO.SetActive(true);

        // Guardar escena
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log("[BigMapSetup] ¡Mapa grande configurado con éxito usando FondoMapa.png!");
    }
}
