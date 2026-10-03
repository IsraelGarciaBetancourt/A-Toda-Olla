using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

/// <summary>
/// Herramienta de Editor para construir y configurar el Menú de Pausa en la escena Game.
/// Utiliza FondoPause.png, BotonSalir.png, BotonReanudar.png y los sprites procedurales de sliders.
/// Se ejecuta desde: Tools → A-Toda-Olla → Setup Pause Menu
/// </summary>
public static class PauseMenuSetupTool
{

    private const string FONDO_PAUSE_PATH = "Assets/UI/Fondos/FondoPause.png";
    private const string BOTON_SALIR_PATH = "Assets/UI/Buttons/BotonSalir.png";
    private const string BOTON_REANUDAR_PATH = "Assets/UI/Buttons/BotonReanudar.png";

    private const string SLIDER_TRACK_PATH = "Assets/UI/Pause/SliderTrack.png";
    private const string SLIDER_FILL_PATH = "Assets/UI/Pause/SliderFill.png";
    private const string SLIDER_HANDLE_PATH = "Assets/UI/Pause/SliderHandle.png";
    private const string SECTION_BADGE_PATH = "Assets/UI/Pause/SectionBadge.png";

    [MenuItem("Tools/A-Toda-Olla/Setup Pause Menu")]
    public static void Setup()
    {
        // ── 1. Cargar Sprites ──────────────────────────────────────────────
        Sprite fondoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FONDO_PAUSE_PATH);
        Sprite salirSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BOTON_SALIR_PATH);
        Sprite reanudarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BOTON_REANUDAR_PATH);

        Sprite trackSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SLIDER_TRACK_PATH);
        Sprite fillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SLIDER_FILL_PATH);
        Sprite handleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SLIDER_HANDLE_PATH);
        Sprite badgeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SECTION_BADGE_PATH);

        if (fondoSprite == null || salirSprite == null || reanudarSprite == null)
        {
            Debug.LogError("[PauseMenuSetup] No se pudieron cargar los sprites principales de pausa.");
            return;
        }

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF.asset");
        if (fontAsset == null)
        {
            fontAsset = TMP_Settings.defaultFontAsset;
        }

        // ── 2. Buscar HUDCanvas ────────────────────────────────────────────
        Canvas hudCanvas = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.name == "HUDCanvas") { hudCanvas = c; break; }
        }

        if (hudCanvas == null)
        {
            Debug.LogError("[PauseMenuSetup] No se encontró HUDCanvas en la escena.");
            return;
        }

        // Eliminar instancias previas si existen
        Transform oldMenu = hudCanvas.transform.Find("PauseMenuPanel");
        if (oldMenu != null) Object.DestroyImmediate(oldMenu.gameObject);

        Transform oldMgr = hudCanvas.transform.Find("PauseManager");
        if (oldMgr != null) Object.DestroyImmediate(oldMgr.gameObject);

        // ── 3. Crear PauseManager (Siempre Activo en la escena) ─────────────
        GameObject pauseManagerGO = new GameObject("PauseManager");
        pauseManagerGO.transform.SetParent(hudCanvas.transform, false);
        PauseMenuController controller = pauseManagerGO.AddComponent<PauseMenuController>();

        // ── 4. Panel Raíz (Pantalla Completa) ──────────────────────────────
        GameObject panelRootGO = new GameObject("PauseMenuPanel", typeof(RectTransform));
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

        controller.panelGroup = panelGroup;

        // Velo oscuro translúcido de fondo
        GameObject dimmerGO = new GameObject("DimmerOverlay", typeof(RectTransform));
        dimmerGO.transform.SetParent(panelRootGO.transform, false);
        RectTransform dimmerRT = dimmerGO.GetComponent<RectTransform>();
        dimmerRT.anchorMin = Vector2.zero;
        dimmerRT.anchorMax = Vector2.one;
        dimmerRT.offsetMin = Vector2.zero;
        dimmerRT.offsetMax = Vector2.zero;
        Image dimmerImg = dimmerGO.AddComponent<Image>();
        dimmerImg.color = new Color(0f, 0f, 0f, 0.55f);
        dimmerImg.raycastTarget = true; // Absorbe clics fuera del cartel

        // ── 5. Cartel Central (FondoPause.png) ─────────────────────────────
        GameObject boardGO = new GameObject("PauseBoard", typeof(RectTransform));
        boardGO.transform.SetParent(panelRootGO.transform, false);
        RectTransform boardRT = boardGO.GetComponent<RectTransform>();
        boardRT.anchorMin = new Vector2(0.5f, 0.5f);
        boardRT.anchorMax = new Vector2(0.5f, 0.5f);
        boardRT.pivot = new Vector2(0.5f, 0.5f);
        // Proporción original de FondoPause (1448 x 1086 ≈ 1.3333). Altura 760 -> Ancho 1013
        boardRT.sizeDelta = new Vector2(1013f, 760f);

        Image boardImg = boardGO.AddComponent<Image>();
        boardImg.sprite = fondoSprite;
        boardImg.preserveAspect = true;
        boardImg.raycastTarget = false;

        // ── 6. Área de Contenido (Sobre la pared de yeso) ───────────────────
        // Coordenadas relativas dentro de FondoPause.png:
        // X(0.12..0.88), Y(0.08..0.62)
        GameObject contentGO = new GameObject("ContentArea", typeof(RectTransform));
        contentGO.transform.SetParent(boardGO.transform, false);
        RectTransform contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.12f, 0.08f);
        contentRT.anchorMax = new Vector2(0.88f, 0.62f);
        contentRT.pivot = new Vector2(0.5f, 0.5f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        // ── 7. SECCIÓN 1: SONIDO ───────────────────────────────────────────
        // Badge "SONIDO"
        GameObject badgeSonidoGO = CreateBadge("Badge_Sonido", contentGO.transform, badgeSprite, "SONIDO",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(130f, 30f), fontAsset);

        // Fila General: Label + Slider
        CreateLabel("Label_General", contentGO.transform, "General", fontAsset,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(28f, -60f), new Vector2(160f, 30f));

        Slider generalSlider = CreateSlider("Slider_General", contentGO.transform, trackSprite, fillSprite, handleSprite,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(220f, -60f), new Vector2(370f, 24f));
        controller.generalVolumeSlider = generalSlider;

        // Fila Radio: Label + Slider
        CreateLabel("Label_Radio", contentGO.transform, "Radio", fontAsset,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(28f, -100f), new Vector2(160f, 30f));

        Slider radioSlider = CreateSlider("Slider_Radio", contentGO.transform, trackSprite, fillSprite, handleSprite,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(220f, -100f), new Vector2(370f, 24f));
        controller.radioVolumeSlider = radioSlider;

        // ── 8. SECCIÓN 2: CONTROLLER ───────────────────────────────────────
        // Badge "CONTROLLER"
        GameObject badgeCtrlGO = CreateBadge("Badge_Controller", contentGO.transform, badgeSprite, "CONTROLLER",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -150f), new Vector2(165f, 30f), fontAsset);

        // Fila Sensibilidad: Label + Slider
        CreateLabel("Label_Sensibilidad", contentGO.transform, "Sensibilidad del mouse", fontAsset,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(28f, -196f), new Vector2(240f, 30f));

        Slider sensSlider = CreateSlider("Slider_Sensibilidad", contentGO.transform, trackSprite, fillSprite, handleSprite,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(275f, -196f), new Vector2(315f, 24f));
        controller.mouseSensitivitySlider = sensSlider;

        // ── 9. BOTONES INFERIORES: SALIR Y REANUDAR ────────────────────────
        // Botón SALIR (Rojo)
        GameObject btnSalirGO = new GameObject("Button_Salir", typeof(RectTransform));
        btnSalirGO.transform.SetParent(contentGO.transform, false);
        RectTransform salirRT = btnSalirGO.GetComponent<RectTransform>();
        salirRT.anchorMin = new Vector2(0.5f, 0f);
        salirRT.anchorMax = new Vector2(0.5f, 0f);
        salirRT.pivot = new Vector2(0.5f, 0.5f);
        salirRT.anchoredPosition = new Vector2(-140f, 52f);
        salirRT.sizeDelta = new Vector2(245f, 82f);
        salirRT.localScale = Vector3.one;

        Image salirImg = btnSalirGO.AddComponent<Image>();
        salirImg.sprite = salirSprite;
        salirImg.preserveAspect = true;

        Button salirBtn = btnSalirGO.AddComponent<Button>();
        salirBtn.targetGraphic = salirImg;
        ColorBlock salirColors = salirBtn.colors;
        salirColors.normalColor = Color.white;
        salirColors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        salirColors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        salirBtn.colors = salirColors;
        controller.exitButton = salirBtn;

        MenuButtonJuice salirJuice = btnSalirGO.AddComponent<MenuButtonJuice>();
        salirJuice.hoverMultiplier = 1.05f;
        salirJuice.pressedMultiplier = 0.95f;

        // Botón REANUDAR (Verde)
        GameObject btnReanudarGO = new GameObject("Button_Reanudar", typeof(RectTransform));
        btnReanudarGO.transform.SetParent(contentGO.transform, false);
        RectTransform reanudarRT = btnReanudarGO.GetComponent<RectTransform>();
        reanudarRT.anchorMin = new Vector2(0.5f, 0f);
        reanudarRT.anchorMax = new Vector2(0.5f, 0f);
        reanudarRT.pivot = new Vector2(0.5f, 0.5f);
        reanudarRT.anchoredPosition = new Vector2(140f, 52f);
        reanudarRT.sizeDelta = new Vector2(260f, 82f);
        reanudarRT.localScale = Vector3.one;

        Image reanudarImg = btnReanudarGO.AddComponent<Image>();
        reanudarImg.sprite = reanudarSprite;
        reanudarImg.preserveAspect = true;

        Button reanudarBtn = btnReanudarGO.AddComponent<Button>();
        reanudarBtn.targetGraphic = reanudarImg;
        ColorBlock reanudarColors = reanudarBtn.colors;
        reanudarColors.normalColor = Color.white;
        reanudarColors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        reanudarColors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        reanudarBtn.colors = reanudarColors;
        controller.resumeButton = reanudarBtn;

        MenuButtonJuice reanudarJuice = btnReanudarGO.AddComponent<MenuButtonJuice>();
        reanudarJuice.hoverMultiplier = 1.05f;
        reanudarJuice.pressedMultiplier = 0.95f;

        // Dejar panel visible en el editor para ajuste visual (en runtime Awake() lo ocultará)
        panelRootGO.SetActive(true);

        // Guardar escena
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log("[PauseMenuSetup] ¡Menú de pausa configurado exitosamente con PauseManager activo!");
    }

    // ── MÉTODOS DE APOYO DE CONSTRUCCIÓN UI ─────────────────────────────────

    private static GameObject CreateBadge(string name, Transform parent, Sprite sprite, string text,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.raycastTarget = false;

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TMP_Text tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = 16f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.24f, 0.20f, 0.16f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return go;
    }

    private static void CreateLabel(string name, Transform parent, string text, TMP_FontAsset font,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.22f, 0.19f, 0.16f, 1f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
    }

    private static Slider CreateSlider(string name, Transform parent, Sprite trackSprite, Sprite fillSprite, Sprite handleSprite,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject sliderGO = new GameObject(name, typeof(RectTransform));
        sliderGO.transform.SetParent(parent, false);
        RectTransform sliderRT = sliderGO.GetComponent<RectTransform>();
        sliderRT.anchorMin = anchorMin;
        sliderRT.anchorMax = anchorMax;
        sliderRT.pivot = pivot;
        sliderRT.anchoredPosition = pos;
        sliderRT.sizeDelta = size;

        Slider slider = sliderGO.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;

        // Background (Pista oscura)
        GameObject bgGO = new GameObject("Background", typeof(RectTransform));
        bgGO.transform.SetParent(sliderGO.transform, false);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.sprite = trackSprite;
        bgImg.type = Image.Type.Sliced;
        bgImg.raycastTarget = true;

        // Fill Area
        GameObject fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRT.anchorMin = Vector2.zero;
        fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.offsetMin = new Vector2(2f, 2f);
        fillAreaRT.offsetMax = new Vector2(-2f, -2f);

        // Fill (Barra dorada)
        GameObject fillGO = new GameObject("Fill", typeof(RectTransform));
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.sprite = fillSprite;
        fillImg.type = Image.Type.Sliced;
        fillImg.raycastTarget = false;

        // Handle Slide Area
        GameObject handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform handleAreaRT = handleAreaGO.GetComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = new Vector2(10f, 0f);
        handleAreaRT.offsetMax = new Vector2(-10f, 0f);

        // Handle (Perilla blanca/crema 3D)
        GameObject handleGO = new GameObject("Handle", typeof(RectTransform));
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        RectTransform handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.anchorMin = new Vector2(0.5f, 0.5f);
        handleRT.anchorMax = new Vector2(0.5f, 0.5f);
        handleRT.pivot = new Vector2(0.5f, 0.5f);
        handleRT.sizeDelta = new Vector2(20f, 36f);
        Image handleImg = handleGO.AddComponent<Image>();
        handleImg.sprite = handleSprite;
        handleImg.type = Image.Type.Simple;
        handleImg.preserveAspect = true;
        handleImg.raycastTarget = true;

        slider.targetGraphic = handleImg;
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;

        return slider;
    }
}
