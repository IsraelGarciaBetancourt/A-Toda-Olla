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
/// configurando el fondo con logo, los botones metálicos animados, el panel de opciones
/// y registrando ambas escenas en los Build Settings.
/// </summary>
public static class MainMenuSceneBuilder
{
    private const string SCENE_PATH = "Assets/Scenes/MainMenu.unity";
    private const string GAME_SCENE_PATH = "Assets/Scenes/Game.unity";

    private const string FONDO_SPRITE_PATH = "Assets/UI/MainMenu/FondoMenu.png";
    private const string BOTON_SPRITE_PATH = "Assets/UI/MainMenu/BotonMetalico.png";
    private const string BANGERS_FONT_PATH = "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset";
    private const string LIBERATION_FONT_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Tools/Menu/Build Complete MainMenu Scene")]
    public static void BuildMainMenuScene()
    {
        // 1. Crear nueva escena limpia
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Main Camera
        GameObject camGO = new GameObject("Main Camera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.08f, 0.05f); // Tono cálido acorde al atardecer
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0, 0, -10f);

        // 3. EventSystem (con InputSystemUIInputModule)
        GameObject eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        var inputModule = eventSystemGO.AddComponent<InputSystemUIInputModule>();
        var actionsAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        if (actionsAsset != null)
        {
            inputModule.actionsAsset = actionsAsset;
        }

        // Cargar Sprites y Fuente
        Sprite fondoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FONDO_SPRITE_PATH);
        Sprite botonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BOTON_SPRITE_PATH);
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BANGERS_FONT_PATH);
        if (fontAsset == null)
        {
            fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LIBERATION_FONT_PATH);
        }

        // 4. Canvas Principal
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

        // 5. Fondo con Logo integrado (FondoMenu)
        GameObject bgGO = new GameObject("Background", typeof(RectTransform));
        bgGO.layer = 5;
        bgGO.transform.SetParent(canvasGO.transform, false);

        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;

        Image bgImage = bgGO.AddComponent<Image>();
        if (fondoSprite != null)
        {
            bgImage.sprite = fondoSprite;
        }
        bgImage.color = Color.white;
        bgImage.raycastTarget = false;

        // 6. Contenedor de Botones (Centrado debajo del logo)
        GameObject buttonsContainer = new GameObject("ButtonsContainer", typeof(RectTransform));
        buttonsContainer.layer = 5;
        buttonsContainer.transform.SetParent(canvasGO.transform, false);

        RectTransform containerRect = buttonsContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = new Vector2(0f, -90f);
        containerRect.sizeDelta = new Vector2(500f, 480f);

        VerticalLayoutGroup layout = buttonsContainer.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // Crear los 3 botones
        Button btnJugar = CreateMenuButton("Button_Jugar", "Jugar", botonSprite, fontAsset, buttonsContainer.transform);
        Button btnOpciones = CreateMenuButton("Button_Opciones", "Opciones", botonSprite, fontAsset, buttonsContainer.transform);
        Button btnSalir = CreateMenuButton("Button_Salir", "Salir", botonSprite, fontAsset, buttonsContainer.transform);

        // Vincular eventos de los botones a MainMenuController
        UnityEventTools.AddPersistentListener(btnJugar.onClick, menuController.PlayGame);
        UnityEventTools.AddPersistentListener(btnOpciones.onClick, menuController.OpenOptions);
        UnityEventTools.AddPersistentListener(btnSalir.onClick, menuController.QuitGame);

        // 7. Panel Modal de Opciones
        GameObject optionsPanel = CreateOptionsPanel(canvasGO.transform, fontAsset, botonSprite, menuController);
        menuController.optionsPanel = optionsPanel;

        // 8. Guardar la Escena MainMenu.unity
        EditorSceneManager.SaveScene(newScene, SCENE_PATH);

        // 9. Actualizar los Build Settings para incluir MainMenu (0) y Game (1)
        UpdateBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[MainMenuBuilder] 🎉 ¡Escena MainMenu creada y configurada con éxito! (Assets/Scenes/MainMenu.unity)");
    }

    private static Button CreateMenuButton(string name, string text, Sprite botonSprite, TMP_FontAsset font, Transform parent)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform));
        btnGO.layer = 5;
        btnGO.transform.SetParent(parent, false);

        RectTransform rect = btnGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(440f, 135f);

        Image img = btnGO.AddComponent<Image>();
        if (botonSprite != null) img.sprite = botonSprite;
        img.color = Color.white;

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;

        // Micro-animación en hover / press
        btnGO.AddComponent<MenuButtonJuice>();

        // Texto del botón
        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.layer = 5;
        textGO.transform.SetParent(btnGO.transform, false);

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = new Vector2(0f, 2f); // Ligero ajuste vertical para centrar con el bisel metálico

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = 58;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        // Sombra / contorno oscuro
        tmp.outlineColor = new Color32(30, 30, 35, 255);
        tmp.outlineWidth = 0.22f;

        return btn;
    }

    private static GameObject CreateOptionsPanel(Transform canvasParent, TMP_FontAsset font, Sprite botonSprite, MainMenuController menuController)
    {
        GameObject panelRoot = new GameObject("OptionsPanel", typeof(RectTransform));
        panelRoot.layer = 5;
        panelRoot.transform.SetParent(canvasParent, false);

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // Fondo oscuro bloqueador
        Image blocker = panelRoot.AddComponent<Image>();
        blocker.color = new Color(0.04f, 0.04f, 0.06f, 0.85f);

        // Tarjeta central de opciones
        GameObject card = new GameObject("Card", typeof(RectTransform));
        card.layer = 5;
        card.transform.SetParent(panelRoot.transform, false);

        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(620f, 480f);
        cardRect.anchoredPosition = Vector2.zero;

        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

        // Título "OPCIONES"
        GameObject titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.layer = 5;
        titleGO.transform.SetParent(card.transform, false);

        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -50f);
        titleRect.sizeDelta = new Vector2(0f, 60f);

        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        if (font != null) titleTMP.font = font;
        titleTMP.text = "OPCIONES";
        titleTMP.fontSize = 54;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(1f, 0.85f, 0.2f, 1f);

        // Etiqueta Volumen
        GameObject volLabelGO = new GameObject("VolumeLabel", typeof(RectTransform));
        volLabelGO.layer = 5;
        volLabelGO.transform.SetParent(card.transform, false);

        RectTransform volLabelRect = volLabelGO.GetComponent<RectTransform>();
        volLabelRect.anchoredPosition = new Vector2(0f, 45f);
        volLabelRect.sizeDelta = new Vector2(400f, 35f);

        TextMeshProUGUI volLabel = volLabelGO.AddComponent<TextMeshProUGUI>();
        if (font != null) volLabel.font = font;
        volLabel.text = "VOLUMEN GENERAL";
        volLabel.fontSize = 32;
        volLabel.alignment = TextAlignmentOptions.Center;
        volLabel.color = Color.white;

        // Slider de Volumen
        GameObject sliderGO = CreateSimpleSlider("VolumeSlider", card.transform);
        Slider slider = sliderGO.GetComponent<Slider>();
        menuController.volumeSlider = slider;

        // Botón "VOLVER"
        Button btnVolver = CreateMenuButton("Button_Volver", "Volver", botonSprite, font, card.transform);
        RectTransform btnVolverRect = btnVolver.GetComponent<RectTransform>();
        btnVolverRect.anchoredPosition = new Vector2(0f, -145f);
        btnVolverRect.sizeDelta = new Vector2(300f, 95f);
        btnVolver.GetComponentInChildren<TextMeshProUGUI>().fontSize = 42;

        UnityEventTools.AddPersistentListener(btnVolver.onClick, menuController.CloseOptions);

        // Iniciar panel cerrado
        panelRoot.SetActive(false);

        return panelRoot;
    }

    private static GameObject CreateSimpleSlider(string name, Transform parent)
    {
        GameObject sliderRoot = new GameObject(name, typeof(RectTransform));
        sliderRoot.layer = 5;
        sliderRoot.transform.SetParent(parent, false);

        RectTransform rootRect = sliderRoot.GetComponent<RectTransform>();
        rootRect.anchoredPosition = new Vector2(0f, -5f);
        rootRect.sizeDelta = new Vector2(380f, 30f);

        // Background del slider
        GameObject bg = new GameObject("Background", typeof(RectTransform));
        bg.layer = 5;
        bg.transform.SetParent(sliderRoot.transform, false);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.22f, 0.26f, 1f);

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.layer = 5;
        fillArea.transform.SetParent(sliderRoot.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.sizeDelta = Vector2.zero;

        GameObject fill = new GameObject("Fill", typeof(RectTransform));
        fill.layer = 5;
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.sizeDelta = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.85f, 0.4f, 1f);

        // Handle Slide Area
        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.layer = 5;
        handleArea.transform.SetParent(sliderRoot.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.sizeDelta = Vector2.zero;

        GameObject handle = new GameObject("Handle", typeof(RectTransform));
        handle.layer = 5;
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(30f, 30f);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;

        Slider slider = sliderRoot.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        return sliderRoot;
    }

    private static void UpdateBuildSettings()
    {
        EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes;
        bool hasMainMenu = false;
        bool hasGame = false;

        foreach (var scene in currentScenes)
        {
            if (scene.path == SCENE_PATH) hasMainMenu = true;
            if (scene.path == GAME_SCENE_PATH) hasGame = true;
        }

        EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[2];
        newScenes[0] = new EditorBuildSettingsScene(SCENE_PATH, true);
        newScenes[1] = new EditorBuildSettingsScene(GAME_SCENE_PATH, true);

        EditorBuildSettings.scenes = newScenes;
        Debug.Log("[MainMenuBuilder] 📋 Build Settings actualizados: [0] MainMenu, [1] Game.");
    }
}
