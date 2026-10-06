using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Herramienta de Editor para configurar automáticamente la mecánica del Taller Mecánico:
/// 1. Encuentra o configura 'EntradaMecanico' con el cilindro brillante azul estilo GTA y su script MechanicWorkshopZone.
/// 2. Configura en HUDCanvas el panel de vista de mejoras con MecanicoFondo.png y su script MechanicWorkshopUI.
/// 3. Configura el aviso HUD (MechanicPromptHUD) para mantener presionada la tecla [F].
/// 
/// Se ejecuta desde: Tools → A-Toda-Olla → Configurar Taller Mecánico (Setup Workshop)
/// </summary>
[InitializeOnLoad]
public static class MechanicWorkshopSetupTool
{
    private const string MECANICO_FONDO_PATH = "Assets/UI/Mecanico/MecanicoFondo.png";
    private const string BOTON_SALIR_PATH = "Assets/UI/Buttons/BotonSalir.png";
    private const string ROUNDED_BOX_PATH = "Assets/UI/rounded_box.png";
    private const string CORONA_MAT_PATH = "Assets/Materials/Mechanic_GTACorona_Blue.mat";
    private const string RING_MAT_PATH = "Assets/Materials/Mechanic_GTARing_Blue.mat";

    static MechanicWorkshopSetupTool()
    {
        EditorApplication.delayCall += CheckAndSetupAuto;
    }

    private static void CheckAndSetupAuto()
    {
        if (Application.isPlaying) return;

        // Si EntradaMecanico existe pero no tiene MechanicWorkshopZone, o falta la UI en el canvas
        GameObject entradaGO = GameObject.Find("EntradaMecanico");
        Canvas hud = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>())
        {
            if (c.name == "HUDCanvas") { hud = c; break; }
        }

        bool needsSetup = false;
        if (entradaGO != null && entradaGO.GetComponent<MechanicWorkshopZone>() == null)
        {
            needsSetup = true;
        }
        else if (hud != null && (hud.transform.Find("MechanicWorkshopPanel") == null || hud.transform.Find("MechanicPromptHUD") == null))
        {
            needsSetup = true;
        }

        if (needsSetup)
        {
            Setup();
        }
    }

    [MenuItem("Tools/A-Toda-Olla/Configurar Taller Mecánico (Setup Workshop)")]
    public static void Setup()
    {
        // ── 1. Cargar Assets ──────────────────────────────────────────────
        Sprite fondoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MECANICO_FONDO_PATH);
        Sprite salirSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BOTON_SALIR_PATH);
        Sprite boxSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ROUNDED_BOX_PATH);
        Material coronaMat = AssetDatabase.LoadAssetAtPath<Material>(CORONA_MAT_PATH);
        Material ringMat = AssetDatabase.LoadAssetAtPath<Material>(RING_MAT_PATH);

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF.asset");
        if (fontAsset == null)
        {
            fontAsset = TMP_Settings.defaultFontAsset;
        }

        // ── 2. Localizar o Configurar EntradaMecanico ──────────────────────
        GameObject entradaGO = GameObject.Find("EntradaMecanico");
        if (entradaGO == null)
        {
            GameObject mecanicoParent = GameObject.Find("Mecanico");
            if (mecanicoParent != null)
            {
                Transform t = mecanicoParent.transform.Find("EntradaMecanico");
                if (t != null) entradaGO = t.gameObject;
            }
        }

        if (entradaGO == null)
        {
            // Intentar crearlo en base al edificio si no existía
            GameObject building = GameObject.Find("building-o");
            GameObject parent = GameObject.Find("Mecanico");
            if (parent == null) parent = new GameObject("Mecanico");

            entradaGO = new GameObject("EntradaMecanico");
            entradaGO.transform.SetParent(parent.transform, false);

            if (building != null)
            {
                entradaGO.transform.position = building.transform.position + building.transform.forward * 4.5f;
            }
            else
            {
                entradaGO.transform.position = Vector3.zero;
            }
            Debug.Log("[MechanicWorkshopSetup] Se creó nuevo GameObject EntradaMecanico.");
        }

        // Configurar BoxCollider y MechanicWorkshopZone en EntradaMecanico
        BoxCollider boxCol = entradaGO.GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = entradaGO.AddComponent<BoxCollider>();
        boxCol.isTrigger = true;
        boxCol.size = new Vector3(5.5f, 3.2f, 5.5f);
        boxCol.center = new Vector3(0f, 1.6f, 0f);

        MechanicWorkshopZone zone = entradaGO.GetComponent<MechanicWorkshopZone>();
        if (zone == null) zone = entradaGO.AddComponent<MechanicWorkshopZone>();
        zone.markerDiameter = 5.5f;
        zone.cylinderHeight = 2.2f;
        zone.holdDuration = 0.8f;
        zone.coronaMaterial = coronaMat;
        zone.ringMaterial = ringMat;
        zone.blueColor = new Color(0.12f, 0.65f, 1.0f, 0.85f);
        zone.EnsureVisuals();

        EditorUtility.SetDirty(entradaGO);

        // ── 3. Buscar HUDCanvas ────────────────────────────────────────────
        Canvas hudCanvas = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>())
        {
            if (c.name == "HUDCanvas") { hudCanvas = c; break; }
        }

        if (hudCanvas == null)
        {
            Debug.LogError("[MechanicWorkshopSetup] No se encontró HUDCanvas en la escena.");
            return;
        }

        // ── 4. Configurar Panel de UI del Taller (MechanicWorkshopPanel) ───
        Transform oldPanel = hudCanvas.transform.Find("MechanicWorkshopPanel");
        if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);

        GameObject panelRootGO = new GameObject("MechanicWorkshopPanel", typeof(RectTransform));
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

        MechanicWorkshopUI uiController = panelRootGO.AddComponent<MechanicWorkshopUI>();
        uiController.panelGroup = panelGroup;

        // Velo oscuro de fondo
        GameObject bgDimmerGO = new GameObject("Dimmer", typeof(RectTransform));
        bgDimmerGO.transform.SetParent(panelRootGO.transform, false);
        RectTransform dimmerRT = bgDimmerGO.GetComponent<RectTransform>();
        dimmerRT.anchorMin = Vector2.zero;
        dimmerRT.anchorMax = Vector2.one;
        dimmerRT.offsetMin = Vector2.zero;
        dimmerRT.offsetMax = Vector2.zero;
        Image dimmerImg = bgDimmerGO.AddComponent<Image>();
        dimmerImg.color = new Color(0.08f, 0.08f, 0.09f, 0.96f);
        dimmerImg.raycastTarget = true;

        // Imagen de Fondo del Taller (MecanicoFondo.png)
        GameObject fondoGO = new GameObject("FondoTaller", typeof(RectTransform));
        fondoGO.transform.SetParent(panelRootGO.transform, false);
        RectTransform fondoRT = fondoGO.GetComponent<RectTransform>();
        fondoRT.anchorMin = Vector2.zero;
        fondoRT.anchorMax = Vector2.one;
        fondoRT.offsetMin = Vector2.zero;
        fondoRT.offsetMax = Vector2.zero;
        Image fondoImg = fondoGO.AddComponent<Image>();
        fondoImg.sprite = fondoSprite;
        fondoImg.preserveAspect = true;
        fondoImg.raycastTarget = false;
        uiController.backgroundImage = fondoImg;

        // Botón Salir / Volver al juego (esquina inferior derecha)
        GameObject btnSalirGO = new GameObject("Button_SalirTaller", typeof(RectTransform));
        btnSalirGO.transform.SetParent(panelRootGO.transform, false);
        RectTransform btnSalirRT = btnSalirGO.GetComponent<RectTransform>();
        btnSalirRT.anchorMin = new Vector2(1f, 0f);
        btnSalirRT.anchorMax = new Vector2(1f, 0f);
        btnSalirRT.pivot = new Vector2(1f, 0f);
        btnSalirRT.anchoredPosition = new Vector2(-30f, 30f);
        btnSalirRT.sizeDelta = new Vector2(210f, 70f);

        Image btnSalirImg = btnSalirGO.AddComponent<Image>();
        if (salirSprite != null)
        {
            btnSalirImg.sprite = salirSprite;
            btnSalirImg.preserveAspect = true;
        }
        else
        {
            btnSalirImg.color = new Color(0.85f, 0.25f, 0.25f, 1f);
        }

        Button salirBtn = btnSalirGO.AddComponent<Button>();
        salirBtn.targetGraphic = btnSalirImg;
        ColorBlock cb = salirBtn.colors;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        salirBtn.colors = cb;
        uiController.closeButton = salirBtn;

        MenuButtonJuice salirJuice = btnSalirGO.AddComponent<MenuButtonJuice>();
        salirJuice.hoverMultiplier = 1.05f;
        salirJuice.pressedMultiplier = 0.95f;

        // Texto de ayuda en pantalla: [ESC] o [F] para salir
        GameObject hintGO = new GameObject("ExitHintText", typeof(RectTransform));
        hintGO.transform.SetParent(panelRootGO.transform, false);
        RectTransform hintRT = hintGO.GetComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(1f, 0f);
        hintRT.anchorMax = new Vector2(1f, 0f);
        hintRT.pivot = new Vector2(1f, 0f);
        hintRT.anchoredPosition = new Vector2(-255f, 48f);
        hintRT.sizeDelta = new Vector2(300f, 35f);

        TMP_Text hintText = hintGO.AddComponent<TextMeshProUGUI>();
        hintText.font = fontAsset;
        hintText.text = "Presiona [ESC] o [F] para volver";
        hintText.fontSize = 17f;
        hintText.fontStyle = FontStyles.Bold;
        hintText.color = new Color(1f, 1f, 1f, 0.75f);
        hintText.alignment = TextAlignmentOptions.MidlineRight;
        hintText.raycastTarget = false;
        uiController.exitHintText = hintText;

        // Texto de Dinero Real en el Taller (esquina inferior izquierda sobre el billete del fondo)
        GameObject moneyGO = new GameObject("WorkshopMoneyText", typeof(RectTransform));
        moneyGO.transform.SetParent(panelRootGO.transform, false);
        RectTransform moneyRT = moneyGO.GetComponent<RectTransform>();
        moneyRT.anchorMin = new Vector2(0f, 0f);
        moneyRT.anchorMax = new Vector2(0f, 0f);
        moneyRT.pivot = new Vector2(0f, 0.5f);
        moneyRT.anchoredPosition = new Vector2(148f, 72f);
        moneyRT.sizeDelta = new Vector2(280f, 50f);

        TextMeshProUGUI moneyText = moneyGO.AddComponent<TextMeshProUGUI>();
        moneyText.font = fontAsset;
        moneyText.text = "$ 0";
        moneyText.fontSize = 28f;
        moneyText.fontStyle = FontStyles.Bold;
        moneyText.color = new Color(0.98f, 0.91f, 0.70f, 1f);
        moneyText.alignment = TextAlignmentOptions.MidlineLeft;
        moneyText.raycastTarget = false;
        uiController.workshopMoneyText = moneyText;

        // En el editor se deja visible (alpha = 1) para poder posicionar y editar libremente en Scene View.
        // Al iniciar la partida en Play Mode, MechanicWorkshopUI.Awake() lo oculta automáticamente.
        panelGroup.alpha = 1f;
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;
        panelRootGO.SetActive(true);

        // ── 5. Configurar HUD Prompt para Mantener [F] ─────────────────────
        Transform oldPrompt = hudCanvas.transform.Find("MechanicPromptHUD");
        if (oldPrompt != null) Object.DestroyImmediate(oldPrompt.gameObject);

        GameObject promptRootGO = new GameObject("MechanicPromptHUD", typeof(RectTransform));
        promptRootGO.transform.SetParent(hudCanvas.transform, false);
        RectTransform promptRootRT = promptRootGO.GetComponent<RectTransform>();
        promptRootRT.anchorMin = new Vector2(0.5f, 0f);
        promptRootRT.anchorMax = new Vector2(0.5f, 0f);
        promptRootRT.pivot = new Vector2(0.5f, 0.5f);
        promptRootRT.anchoredPosition = new Vector2(0f, 110f);
        promptRootRT.sizeDelta = new Vector2(400f, 54f);

        CanvasGroup promptGroup = promptRootGO.AddComponent<CanvasGroup>();
        promptGroup.alpha = 1f;
        promptGroup.blocksRaycasts = true;

        MechanicWorkshopPromptHUD promptHUD = promptRootGO.AddComponent<MechanicWorkshopPromptHUD>();
        promptHUD.promptContainer = promptRootRT;
        promptHUD.canvasGroup = promptGroup;

        // Fondo oscuro redondeado
        GameObject promptBgGO = new GameObject("PromptBg", typeof(RectTransform));
        promptBgGO.transform.SetParent(promptRootGO.transform, false);
        RectTransform promptBgRT = promptBgGO.GetComponent<RectTransform>();
        promptBgRT.anchorMin = Vector2.zero;
        promptBgRT.anchorMax = Vector2.one;
        promptBgRT.offsetMin = Vector2.zero;
        promptBgRT.offsetMax = Vector2.zero;
        Image promptBgImg = promptBgGO.AddComponent<Image>();
        promptBgImg.sprite = boxSprite;
        promptBgImg.type = Image.Type.Sliced;
        promptBgImg.color = new Color(0.06f, 0.08f, 0.12f, 0.88f); // Negro azulado translúcido
        promptBgImg.raycastTarget = false;

        // Badge para la tecla [F]
        GameObject keyBadgeGO = new GameObject("KeyBadge", typeof(RectTransform));
        keyBadgeGO.transform.SetParent(promptRootGO.transform, false);
        RectTransform keyBadgeRT = keyBadgeGO.GetComponent<RectTransform>();
        keyBadgeRT.anchorMin = new Vector2(0f, 0.5f);
        keyBadgeRT.anchorMax = new Vector2(0f, 0.5f);
        keyBadgeRT.pivot = new Vector2(0.5f, 0.5f);
        keyBadgeRT.anchoredPosition = new Vector2(30f, 0f);
        keyBadgeRT.sizeDelta = new Vector2(36f, 36f);

        Image keyBadgeImg = keyBadgeGO.AddComponent<Image>();
        keyBadgeImg.sprite = boxSprite;
        keyBadgeImg.type = Image.Type.Sliced;
        keyBadgeImg.color = new Color(0.12f, 0.65f, 1f, 0.95f); // Azul eléctrico GTA
        keyBadgeImg.raycastTarget = false;
        promptHUD.keyBadgeBg = keyBadgeImg;

        // Texto 'F' dentro del badge
        GameObject keyTextGO = new GameObject("KeyText", typeof(RectTransform));
        keyTextGO.transform.SetParent(keyBadgeGO.transform, false);
        RectTransform keyTextRT = keyTextGO.GetComponent<RectTransform>();
        keyTextRT.anchorMin = Vector2.zero;
        keyTextRT.anchorMax = Vector2.one;
        keyTextRT.offsetMin = Vector2.zero;
        keyTextRT.offsetMax = Vector2.zero;

        TMP_Text keyTextTMP = keyTextGO.AddComponent<TextMeshProUGUI>();
        keyTextTMP.font = fontAsset;
        keyTextTMP.text = "F";
        keyTextTMP.fontSize = 22f;
        keyTextTMP.fontStyle = FontStyles.Bold;
        keyTextTMP.color = Color.white;
        keyTextTMP.alignment = TextAlignmentOptions.Center;
        keyTextTMP.raycastTarget = false;
        promptHUD.keyText = keyTextTMP;

        // Texto de acción
        GameObject actionTextGO = new GameObject("ActionText", typeof(RectTransform));
        actionTextGO.transform.SetParent(promptRootGO.transform, false);
        RectTransform actionTextRT = actionTextGO.GetComponent<RectTransform>();
        actionTextRT.anchorMin = new Vector2(0f, 0.5f);
        actionTextRT.anchorMax = new Vector2(1f, 0.5f);
        actionTextRT.pivot = new Vector2(0f, 0.5f);
        actionTextRT.anchoredPosition = new Vector2(56f, 0f);
        actionTextRT.sizeDelta = new Vector2(-66f, 36f);

        TMP_Text actionTMP = actionTextGO.AddComponent<TextMeshProUGUI>();
        actionTMP.font = fontAsset;
        actionTMP.text = "Mantén para entrar al Taller Mecánico";
        actionTMP.fontSize = 16f;
        actionTMP.fontStyle = FontStyles.Bold;
        actionTMP.color = Color.white;
        actionTMP.alignment = TextAlignmentOptions.MidlineLeft;
        actionTMP.raycastTarget = false;
        promptHUD.actionText = actionTMP;

        // Barra inferior de progreso de carga (Fill)
        GameObject progressTrackGO = new GameObject("ProgressTrack", typeof(RectTransform));
        progressTrackGO.transform.SetParent(promptRootGO.transform, false);
        RectTransform progressTrackRT = progressTrackGO.GetComponent<RectTransform>();
        progressTrackRT.anchorMin = new Vector2(0f, 0f);
        progressTrackRT.anchorMax = new Vector2(1f, 0f);
        progressTrackRT.pivot = new Vector2(0.5f, 0f);
        progressTrackRT.anchoredPosition = new Vector2(0f, 0f);
        progressTrackRT.sizeDelta = new Vector2(0f, 4f);

        Image progressTrackImg = progressTrackGO.AddComponent<Image>();
        progressTrackImg.color = new Color(1f, 1f, 1f, 0.12f);
        progressTrackImg.raycastTarget = false;

        GameObject progressFillGO = new GameObject("ProgressFill", typeof(RectTransform));
        progressFillGO.transform.SetParent(progressTrackGO.transform, false);
        RectTransform progressFillRT = progressFillGO.GetComponent<RectTransform>();
        progressFillRT.anchorMin = Vector2.zero;
        progressFillRT.anchorMax = Vector2.one;
        progressFillRT.offsetMin = Vector2.zero;
        progressFillRT.offsetMax = Vector2.zero;

        Image progressFillImg = progressFillGO.AddComponent<Image>();
        progressFillImg.type = Image.Type.Filled;
        progressFillImg.fillMethod = Image.FillMethod.Horizontal;
        progressFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFillImg.fillAmount = 0f;
        progressFillImg.color = new Color(0.18f, 0.75f, 1.0f, 1f); // Barra azul cian neón
        progressFillImg.raycastTarget = false;
        promptHUD.progressFill = progressFillImg;

        // Guardar cambios en escena y assets
        EditorUtility.SetDirty(hudCanvas.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[MechanicWorkshopSetup] ¡Configuración del Taller Mecánico completada con éxito!");
    }
}
