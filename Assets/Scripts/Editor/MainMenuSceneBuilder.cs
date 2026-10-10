using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Herramienta de Editor para construir la escena completa MainMenu.unity
/// utilizando los nuevos assets gráficos:
/// - Fondo con logo integrado: Assets/UI/MainMenu/MenuPrincipal.png
/// - Botones ilustrados: BotonJugar.png, BotonOpciones.png, BotonSalir.png
/// - Ventana completa de Opciones idéntica a la diseñada en el juego normal (FondoPause.png, badges y sliders custom).
///
/// Menú: Tools → Menu → Build Complete MainMenu Scene
/// </summary>
public static class MainMenuSceneBuilder
{
    private const string SCENE_PATH = "Assets/Scenes/MainMenu.unity";
    private const string GAME_SCENE_PATH = "Assets/Scenes/Game.unity";

    // Nuevos assets del Menú Principal
    private const string FONDO_MENU_PATH = "Assets/UI/MainMenu/MenuPrincipal.png";
    private const string BOTON_JUGAR_PATH = "Assets/UI/MainMenu/BotonJugar.png";
    private const string BOTON_OPCIONES_PATH = "Assets/UI/MainMenu/BotonOpciones.png";
    private const string BOTON_SALIR_PATH = "Assets/UI/MainMenu/BotonSalir.png";

    // Assets de la ventana de opciones (idéntica a la del juego normal)
    private const string FONDO_PAUSE_PATH = "Assets/UI/Fondos/FondoPause.png";
    private const string SLIDER_TRACK_PATH = "Assets/UI/Pause/SliderTrack.png";
    private const string SLIDER_FILL_PATH = "Assets/UI/Pause/SliderFill.png";
    private const string SLIDER_HANDLE_PATH = "Assets/UI/Pause/SliderHandle.png";
    private const string SECTION_BADGE_PATH = "Assets/UI/Pause/SectionBadge.png";
    private const string BOTON_METALICO_PATH = "Assets/UI/MainMenu/BotonMetalico.png";

    // Fuentes
    private const string ROBOTO_BOLD_FONT_PATH = "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF.asset";
    private const string BANGERS_FONT_PATH = "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset";
    private const string LIBERATION_FONT_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Tools/Menu/Build Complete MainMenu Scene")]
    public static void BuildMainMenuScene()
    {
        // ── 1. Crear nueva escena limpia ──────────────────────────────────
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 2. Cámara Principal ───────────────────────────────────────────
        GameObject camGO = new GameObject("Main Camera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.08f, 0.05f);
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0, 0, -10f);

        // ── 3. EventSystem con New Input System ────────────────────────────
        GameObject eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        var inputModule = eventSystemGO.AddComponent<InputSystemUIInputModule>();
        var actionsAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        if (actionsAsset != null)
        {
            inputModule.actionsAsset = actionsAsset;
        }

        // ── 4. Cargar Sprites y Fuentes ────────────────────────────────────
        Sprite fondoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FONDO_MENU_PATH);
        Sprite jugarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BOTON_JUGAR_PATH);
        Sprite opcionesSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BOTON_OPCIONES_PATH);
        Sprite salirSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BOTON_SALIR_PATH);

        Sprite pauseBoardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FONDO_PAUSE_PATH);
        Sprite trackSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SLIDER_TRACK_PATH);
        Sprite fillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SLIDER_FILL_PATH);
        Sprite handleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SLIDER_HANDLE_PATH);
        Sprite badgeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SECTION_BADGE_PATH);
        Sprite metalicoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BOTON_METALICO_PATH);

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ROBOTO_BOLD_FONT_PATH);
        if (fontAsset == null)
            fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BANGERS_FONT_PATH);
        if (fontAsset == null)
            fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LIBERATION_FONT_PATH);
        if (fontAsset == null)
            fontAsset = TMP_Settings.defaultFontAsset;

        // ── 5. Canvas Principal ────────────────────────────────────────────
        GameObject canvasGO = new GameObject("MainMenuCanvas", typeof(RectTransform));
        canvasGO.layer = 5; // UI
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Controlador del Menú
        MainMenuController menuController = canvasGO.AddComponent<MainMenuController>();
        menuController.gameSceneName = "Game";

        // ── 6. Fondo Ilustrado (MenuPrincipal.png) ──────────────────────────
        GameObject bgGO = new GameObject("Background", typeof(RectTransform));
        bgGO.layer = 5;
        bgGO.transform.SetParent(canvasGO.transform, false);

        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image bgImage = bgGO.AddComponent<Image>();
        if (fondoSprite != null)
        {
            bgImage.sprite = fondoSprite;
        }
        bgImage.color = Color.white;
        bgImage.raycastTarget = false;

        // ── 7. Contenedor de Botones (Alineado en el muro central debajo del logo) ──
        // El logo "A Toda OLLA" termina sobre el centro, y el muro de ladrillos/yeso
        // se extiende entre Y=-90 y Y=-420. Centramos los botones en Y = -255.
        GameObject buttonsContainer = new GameObject("ButtonsContainer", typeof(RectTransform));
        buttonsContainer.layer = 5;
        buttonsContainer.transform.SetParent(canvasGO.transform, false);

        RectTransform containerRect = buttonsContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = new Vector2(0f, -255f);
        containerRect.sizeDelta = new Vector2(450f, 340f);

        VerticalLayoutGroup layout = buttonsContainer.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // Crear los 3 botones utilizando sus sprites individuales que ya contienen el texto estilizado 3D
        Button btnJugar = CreateGraphicButton("Button_Jugar", jugarSprite, new Vector2(390f, 115f), buttonsContainer.transform);
        Button btnOpciones = CreateGraphicButton("Button_Opciones", opcionesSprite, new Vector2(390f, 91f), buttonsContainer.transform);
        Button btnSalir = CreateGraphicButton("Button_Salir", salirSprite, new Vector2(390f, 91f), buttonsContainer.transform);

        // Conectar eventos onClick
        UnityEventTools.AddPersistentListener(btnJugar.onClick, menuController.PlayGame);
        UnityEventTools.AddPersistentListener(btnOpciones.onClick, menuController.OpenOptions);
        UnityEventTools.AddPersistentListener(btnSalir.onClick, menuController.QuitGame);

        // ── 8. Ventana Modal de Opciones (Idéntica a la del juego normal) ──
        GameObject optionsPanel = CreateFullOptionsModal(
            canvasGO.transform,
            menuController,
            pauseBoardSprite,
            opcionesSprite,
            badgeSprite,
            trackSprite,
            fillSprite,
            handleSprite,
            metalicoSprite,
            fontAsset
        );

        menuController.optionsPanel = optionsPanel;

        // Iniciar con el panel de opciones cerrado
        optionsPanel.SetActive(false);

        // ── 9. Guardar la Escena MainMenu.unity ────────────────────────────
        EditorSceneManager.SaveScene(newScene, SCENE_PATH);

        // ── 10. Actualizar Build Settings ──────────────────────────────────
        UpdateBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[MainMenuBuilder] 🎉 ¡Escena MainMenu creada exitosamente con los nuevos assets y panel de opciones completo!");
    }

    /// <summary>
    /// Crea un botón usando directamente un sprite con texto ilustrado (Jugar, Opciones, Salir).
    /// </summary>
    private static Button CreateGraphicButton(string name, Sprite sprite, Vector2 size, Transform parent)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform));
        btnGO.layer = 5;
        btnGO.transform.SetParent(parent, false);

        RectTransform rect = btnGO.GetComponent<RectTransform>();
        rect.sizeDelta = size;

        Image img = btnGO.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = true;

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;

        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.selectedColor = Color.white;
        btn.colors = colors;

        // Micro-animación en hover y click
        MenuButtonJuice juice = btnGO.AddComponent<MenuButtonJuice>();
        juice.hoverMultiplier = 1.05f;
        juice.pressedMultiplier = 0.95f;
        juice.animationSpeed = 14f;

        LayoutElement le = btnGO.AddComponent<LayoutElement>();
        le.preferredWidth = size.x;
        le.preferredHeight = size.y;
        le.minWidth = size.x;
        le.minHeight = size.y;

        return btn;
    }

    /// <summary>
    /// Construye la ventana completa de opciones idéntica al Menú de Pausa del juego normal:
    /// Cartel FondoPause.png, badges "SONIDO" y "CONTROLLER", sliders estilizados y botón "VOLVER".
    /// </summary>
    private static GameObject CreateFullOptionsModal(
        Transform canvasParent,
        MainMenuController menuController,
        Sprite boardSprite,
        Sprite opcionesHeaderSprite,
        Sprite badgeSprite,
        Sprite trackSprite,
        Sprite fillSprite,
        Sprite handleSprite,
        Sprite metalicoSprite,
        TMP_FontAsset font)
    {
        // 1. Panel Raíz (Pantalla Completa)
        GameObject panelRootGO = new GameObject("OptionsPanel", typeof(RectTransform));
        panelRootGO.layer = 5;
        panelRootGO.transform.SetParent(canvasParent, false);

        RectTransform panelRootRT = panelRootGO.GetComponent<RectTransform>();
        panelRootRT.anchorMin = Vector2.zero;
        panelRootRT.anchorMax = Vector2.one;
        panelRootRT.offsetMin = Vector2.zero;
        panelRootRT.offsetMax = Vector2.zero;

        CanvasGroup panelGroup = panelRootGO.AddComponent<CanvasGroup>();
        panelGroup.alpha = 1f;
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;

        // 2. Velo Oscuro de Fondo (Dimmer)
        GameObject dimmerGO = new GameObject("DimmerOverlay", typeof(RectTransform));
        dimmerGO.layer = 5;
        dimmerGO.transform.SetParent(panelRootGO.transform, false);

        RectTransform dimmerRT = dimmerGO.GetComponent<RectTransform>();
        dimmerRT.anchorMin = Vector2.zero;
        dimmerRT.anchorMax = Vector2.one;
        dimmerRT.offsetMin = Vector2.zero;
        dimmerRT.offsetMax = Vector2.zero;

        Image dimmerImg = dimmerGO.AddComponent<Image>();
        dimmerImg.color = new Color(0f, 0f, 0f, 0.65f);
        dimmerImg.raycastTarget = true;

        // Botón en el fondo para cerrar haciendo clic afuera
        Button dimmerBtn = dimmerGO.AddComponent<Button>();
        dimmerBtn.transition = Selectable.Transition.None;
        UnityEventTools.AddPersistentListener(dimmerBtn.onClick, menuController.CloseOptions);

        // 3. Cartel Central (FondoPause.png)
        GameObject boardGO = new GameObject("OptionsBoard", typeof(RectTransform));
        boardGO.layer = 5;
        boardGO.transform.SetParent(panelRootGO.transform, false);

        RectTransform boardRT = boardGO.GetComponent<RectTransform>();
        boardRT.anchorMin = new Vector2(0.5f, 0.5f);
        boardRT.anchorMax = new Vector2(0.5f, 0.5f);
        boardRT.pivot = new Vector2(0.5f, 0.5f);
        boardRT.anchoredPosition = Vector2.zero;
        boardRT.sizeDelta = new Vector2(1013f, 760f); // Proporción original (1448 x 1086)

        Image boardImg = boardGO.AddComponent<Image>();
        boardImg.sprite = boardSprite;
        boardImg.preserveAspect = true;
        boardImg.raycastTarget = true; // Bloquea clics hacia el dimmer

        // 3.1. Placa de cabecera "OPCIONES" sobre el letrero superior
        if (opcionesHeaderSprite != null)
        {
            GameObject headerPlateGO = new GameObject("HeaderPlate_Opciones", typeof(RectTransform));
            headerPlateGO.layer = 5;
            headerPlateGO.transform.SetParent(boardGO.transform, false);

            RectTransform headerRT = headerPlateGO.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0.5f, 1f);
            headerRT.anchorMax = new Vector2(0.5f, 1f);
            headerRT.pivot = new Vector2(0.5f, 0.5f);
            headerRT.anchoredPosition = new Vector2(0f, -72f);
            headerRT.sizeDelta = new Vector2(360f, 84f);

            Image headerImg = headerPlateGO.AddComponent<Image>();
            headerImg.sprite = opcionesHeaderSprite;
            headerImg.preserveAspect = true;
            headerImg.raycastTarget = false;
        }

        // 3.2. Botón de Cerrar "X" en la esquina superior derecha
        GameObject btnCloseXGO = new GameObject("Button_CloseX", typeof(RectTransform));
        btnCloseXGO.layer = 5;
        btnCloseXGO.transform.SetParent(boardGO.transform, false);

        RectTransform closeXRT = btnCloseXGO.GetComponent<RectTransform>();
        closeXRT.anchorMin = new Vector2(1f, 1f);
        closeXRT.anchorMax = new Vector2(1f, 1f);
        closeXRT.pivot = new Vector2(0.5f, 0.5f);
        closeXRT.anchoredPosition = new Vector2(-60f, -65f);
        closeXRT.sizeDelta = new Vector2(46f, 46f);

        Image closeXImg = btnCloseXGO.AddComponent<Image>();
        closeXImg.color = new Color(0.85f, 0.22f, 0.22f, 0.95f);
        closeXImg.raycastTarget = true;

        Button closeXBtn = btnCloseXGO.AddComponent<Button>();
        UnityEventTools.AddPersistentListener(closeXBtn.onClick, menuController.CloseOptions);

        GameObject closeXTextGO = new GameObject("Text", typeof(RectTransform));
        closeXTextGO.layer = 5;
        closeXTextGO.transform.SetParent(btnCloseXGO.transform, false);
        RectTransform closeXTextRT = closeXTextGO.GetComponent<RectTransform>();
        closeXTextRT.anchorMin = Vector2.zero;
        closeXTextRT.anchorMax = Vector2.one;
        closeXTextRT.offsetMin = Vector2.zero;
        closeXTextRT.offsetMax = Vector2.zero;

        TMP_Text closeXTMP = closeXTextGO.AddComponent<TextMeshProUGUI>();
        closeXTMP.font = font;
        closeXTMP.text = "✕";
        closeXTMP.fontSize = 26f;
        closeXTMP.fontStyle = FontStyles.Bold;
        closeXTMP.color = Color.white;
        closeXTMP.alignment = TextAlignmentOptions.Center;
        closeXTMP.raycastTarget = false;

        btnCloseXGO.AddComponent<MenuButtonJuice>();

        // 4. Área de Contenido (Sobre la pared de yeso clara de FondoPause)
        GameObject contentGO = new GameObject("ContentArea", typeof(RectTransform));
        contentGO.layer = 5;
        contentGO.transform.SetParent(boardGO.transform, false);

        RectTransform contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.12f, 0.08f);
        contentRT.anchorMax = new Vector2(0.88f, 0.62f);
        contentRT.pivot = new Vector2(0.5f, 0.5f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        // ── 5. SECCIÓN 1: SONIDO ───────────────────────────────────────────
        CreateBadge("Badge_Sonido", contentGO.transform, badgeSprite, "SONIDO",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(130f, 30f), font);

        // Fila General: Label + Slider
        CreateLabel("Label_General", contentGO.transform, "General", font,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(28f, -60f), new Vector2(160f, 30f));

        Slider generalSlider = CreateSlider("Slider_General", contentGO.transform, trackSprite, fillSprite, handleSprite,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(220f, -60f), new Vector2(370f, 24f));
        menuController.generalVolumeSlider = generalSlider;
        menuController.volumeSlider = generalSlider;
        UnityEventTools.AddPersistentListener(generalSlider.onValueChanged, menuController.OnGeneralVolumeChanged);

        // Fila Radio: Label + Slider
        CreateLabel("Label_Radio", contentGO.transform, "Radio", font,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(28f, -100f), new Vector2(160f, 30f));

        Slider radioSlider = CreateSlider("Slider_Radio", contentGO.transform, trackSprite, fillSprite, handleSprite,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(220f, -100f), new Vector2(370f, 24f));
        menuController.radioVolumeSlider = radioSlider;
        UnityEventTools.AddPersistentListener(radioSlider.onValueChanged, menuController.OnRadioVolumeChanged);

        // ── 6. SECCIÓN 2: CONTROLLER ───────────────────────────────────────
        CreateBadge("Badge_Controller", contentGO.transform, badgeSprite, "CONTROLLER",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -150f), new Vector2(165f, 30f), font);

        // Fila Sensibilidad: Label + Slider
        CreateLabel("Label_Sensibilidad", contentGO.transform, "Sensibilidad del mouse", font,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(28f, -196f), new Vector2(240f, 30f));

        Slider sensSlider = CreateSlider("Slider_Sensibilidad", contentGO.transform, trackSprite, fillSprite, handleSprite,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(275f, -196f), new Vector2(315f, 24f));
        menuController.mouseSensitivitySlider = sensSlider;
        UnityEventTools.AddPersistentListener(sensSlider.onValueChanged, menuController.OnMouseSensitivityChanged);

        // ── 7. BOTÓN INFERIOR: VOLVER ──────────────────────────────────────
        GameObject btnVolverGO = new GameObject("Button_Volver", typeof(RectTransform));
        btnVolverGO.layer = 5;
        btnVolverGO.transform.SetParent(contentGO.transform, false);

        RectTransform volverRT = btnVolverGO.GetComponent<RectTransform>();
        volverRT.anchorMin = new Vector2(0.5f, 0f);
        volverRT.anchorMax = new Vector2(0.5f, 0f);
        volverRT.pivot = new Vector2(0.5f, 0.5f);
        volverRT.anchoredPosition = new Vector2(0f, 48f);
        volverRT.sizeDelta = new Vector2(260f, 78f);
        volverRT.localScale = Vector3.one;

        Image volverImg = btnVolverGO.AddComponent<Image>();
        volverImg.sprite = metalicoSprite;
        volverImg.preserveAspect = true;

        Button volverBtn = btnVolverGO.AddComponent<Button>();
        volverBtn.targetGraphic = volverImg;
        ColorBlock volverColors = volverBtn.colors;
        volverColors.normalColor = Color.white;
        volverColors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        volverColors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        volverBtn.colors = volverColors;
        menuController.backButton = volverBtn;

        UnityEventTools.AddPersistentListener(volverBtn.onClick, menuController.CloseOptions);

        MenuButtonJuice volverJuice = btnVolverGO.AddComponent<MenuButtonJuice>();
        volverJuice.hoverMultiplier = 1.05f;
        volverJuice.pressedMultiplier = 0.95f;

        // Texto "VOLVER"
        GameObject volverTextGO = new GameObject("Text", typeof(RectTransform));
        volverTextGO.layer = 5;
        volverTextGO.transform.SetParent(btnVolverGO.transform, false);

        RectTransform volverTextRT = volverTextGO.GetComponent<RectTransform>();
        volverTextRT.anchorMin = Vector2.zero;
        volverTextRT.anchorMax = Vector2.one;
        volverTextRT.offsetMin = Vector2.zero;
        volverTextRT.offsetMax = Vector2.zero;

        TMP_Text volverTMP = volverTextGO.AddComponent<TextMeshProUGUI>();
        volverTMP.font = font;
        volverTMP.text = "VOLVER";
        volverTMP.fontSize = 36f;
        volverTMP.fontStyle = FontStyles.Bold;
        volverTMP.alignment = TextAlignmentOptions.Center;
        volverTMP.color = new Color(0.96f, 0.93f, 0.86f, 1f);
        volverTMP.outlineColor = new Color32(25, 25, 30, 255);
        volverTMP.outlineWidth = 0.22f;
        volverTMP.raycastTarget = false;

        return panelRootGO;
    }

    private static GameObject CreateBadge(string name, Transform parent, Sprite sprite, string text,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
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
        textGO.layer = 5;
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
        go.layer = 5;
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
        sliderGO.layer = 5;
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
        bgGO.layer = 5;
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
        fillAreaGO.layer = 5;
        fillAreaGO.transform.SetParent(sliderGO.transform, false);

        RectTransform fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRT.anchorMin = Vector2.zero;
        fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.offsetMin = new Vector2(2f, 2f);
        fillAreaRT.offsetMax = new Vector2(-2f, -2f);

        // Fill (Barra dorada)
        GameObject fillGO = new GameObject("Fill", typeof(RectTransform));
        fillGO.layer = 5;
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
        handleAreaGO.layer = 5;
        handleAreaGO.transform.SetParent(sliderGO.transform, false);

        RectTransform handleAreaRT = handleAreaGO.GetComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = new Vector2(10f, 0f);
        handleAreaRT.offsetMax = new Vector2(-10f, 0f);

        // Handle (Perilla blanca/crema 3D)
        GameObject handleGO = new GameObject("Handle", typeof(RectTransform));
        handleGO.layer = 5;
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

    private static void UpdateBuildSettings()
    {
        EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[2];
        newScenes[0] = new EditorBuildSettingsScene(SCENE_PATH, true);
        newScenes[1] = new EditorBuildSettingsScene(GAME_SCENE_PATH, true);

        EditorBuildSettings.scenes = newScenes;
        Debug.Log("[MainMenuBuilder] 📋 Build Settings actualizados: [0] MainMenu, [1] Game.");
    }
}
