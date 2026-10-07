#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor tool para construir y vincular automáticamente la UI de GameOver
/// con los sprites de Assets/UI/GameOver en la escena actual.
/// </summary>
public static class GameOverUISetup
{
    [MenuItem("Tools/A-Toda-Olla/Configurar GameOver UI")]
    public static void SetupGameOverUI()
    {
        // 1. Buscar HUDCanvas en la escena
        Canvas hudCanvas = null;
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (var c in canvases)
        {
            if (c.name == "HUDCanvas")
            {
                hudCanvas = c;
                break;
            }
        }

        if (hudCanvas == null && canvases.Length > 0) hudCanvas = canvases[0];
        if (hudCanvas == null)
        {
            Debug.LogError("[GameOverUISetup] No se encontró ningún Canvas en la escena.");
            return;
        }

        // 2. Cargar Sprites desde Assets/UI/GameOver/
        Sprite screenSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/GameOver/GameOverScreen.png");
        Sprite reintentarSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/GameOver/BotonReintentar.png");
        Sprite menuSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/GameOver/BotonMenuPrincipal.png");
        Sprite salirSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/GameOver/BotonSalir.png");

        if (screenSprite == null || reintentarSprite == null || menuSprite == null || salirSprite == null)
        {
            Debug.LogError($"[GameOverUISetup] Faltan sprites en Assets/UI/GameOver/ (screen={screenSprite!=null}, reint={reintentarSprite!=null}, menu={menuSprite!=null}, salir={salirSprite!=null})");
            return;
        }

        // 3. Crear o buscar GameOverPanel
        Transform existingPanel = hudCanvas.transform.Find("GameOverPanel");
        GameObject panelGO;
        if (existingPanel != null)
        {
            panelGO = existingPanel.gameObject;
            Debug.Log("[GameOverUISetup] Actualizando GameOverPanel existente.");
        }
        else
        {
            panelGO = new GameObject("GameOverPanel", typeof(RectTransform), typeof(CanvasGroup));
            panelGO.transform.SetParent(hudCanvas.transform, false);
            Debug.Log("[GameOverUISetup] Creando nuevo GameOverPanel.");
        }

        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelGO.transform.SetAsLastSibling();

        CanvasGroup cg = panelGO.GetComponent<CanvasGroup>();
        if (cg == null) cg = panelGO.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;

        // 4. Imagen de Fondo (GameOverScreen)
        Transform bgTrans = panelGO.transform.Find("GameOverBackground");
        GameObject bgGO = bgTrans != null ? bgTrans.gameObject : new GameObject("GameOverBackground", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(panelGO.transform, false);
        bgGO.transform.SetAsFirstSibling();

        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image bgImg = bgGO.GetComponent<Image>();
        bgImg.sprite = screenSprite;
        bgImg.preserveAspect = true;
        bgImg.color = Color.white;

        // 5. Muro Blanco para Estadísticas (zona centro-derecha de la pantalla)
        Transform statsTrans = panelGO.transform.Find("MuroEstadisticas");
        GameObject statsGO = statsTrans != null ? statsTrans.gameObject : new GameObject("MuroEstadisticas", typeof(RectTransform));
        statsGO.transform.SetParent(panelGO.transform, false);

        RectTransform statsRect = statsGO.GetComponent<RectTransform>();
        // Posicionado sobre el muro blanco visible en la imagen
        statsRect.anchorMin = new Vector2(0.53f, 0.30f);
        statsRect.anchorMax = new Vector2(0.88f, 0.68f);
        statsRect.offsetMin = Vector2.zero;
        statsRect.offsetMax = Vector2.zero;
        statsRect.pivot = new Vector2(0.5f, 0.5f);

        // Textos dentro del Muro Blanco (texto oscuro para contrastar con el fondo blanco)
        TMP_Text titleText = CreateOrUpdateText(statsGO.transform, "TitleText", "JORNADA FALLIDA", 22, FontStyles.Bold, new Color(0.85f, 0.12f, 0.12f), TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(0f, 32f));
        TMP_Text reasonText = CreateOrUpdateText(statsGO.transform, "ReasonText", "¡Se agotó el tiempo de la jornada!", 14, FontStyles.Normal, new Color(0.25f, 0.25f, 0.3f), TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -50f), new Vector2(0f, 24f));
        TMP_Text deliveriesText = CreateOrUpdateText(statsGO.transform, "DeliveriesText", "Ollas entregadas: 0 / 3", 16, FontStyles.Bold, new Color(0.15f, 0.18f, 0.22f), TextAlignmentOptions.Left, new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0f, -90f), new Vector2(0f, 26f));
        TMP_Text earningsText = CreateOrUpdateText(statsGO.transform, "EarningsText", "Ganancias del día: +$0", 16, FontStyles.Normal, new Color(0.12f, 0.55f, 0.2f), TextAlignmentOptions.Left, new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0f, -122f), new Vector2(0f, 26f));
        TMP_Text penaltiesText = CreateOrUpdateText(statsGO.transform, "PenaltiesText", "Multas por desperdicio: -$0", 16, FontStyles.Normal, new Color(0.75f, 0.15f, 0.15f), TextAlignmentOptions.Left, new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0f, -154f), new Vector2(0f, 26f));
        TMP_Text netText = CreateOrUpdateText(statsGO.transform, "NetProfitText", "BALANCE FINAL: $0", 18, FontStyles.Bold, new Color(0.08f, 0.1f, 0.15f), TextAlignmentOptions.Left, new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0f, -192f), new Vector2(0f, 28f));

        // 6. Contenedor de Botones
        Transform buttonsTrans = panelGO.transform.Find("ContenedorBotones");
        GameObject buttonsGO = buttonsTrans != null ? buttonsTrans.gameObject : new GameObject("ContenedorBotones", typeof(RectTransform));
        buttonsGO.transform.SetParent(panelGO.transform, false);

        RectTransform btnsRect = buttonsGO.GetComponent<RectTransform>();
        btnsRect.anchorMin = new Vector2(0f, 0f);
        btnsRect.anchorMax = new Vector2(1f, 0.22f);
        btnsRect.offsetMin = Vector2.zero;
        btnsRect.offsetMax = Vector2.zero;

        // Botón 1: Reintentar
        Button btnReintentar = CreateOrUpdateButton(buttonsGO.transform, "BotonReintentar", reintentarSprite, new Vector2(-310f, 35f), new Vector2(280f, 75f));
        // Botón 2: Menú Principal
        Button btnMenu = CreateOrUpdateButton(buttonsGO.transform, "BotonMenuPrincipal", menuSprite, new Vector2(0f, 35f), new Vector2(280f, 75f));
        // Botón 3: Salir
        Button btnSalir = CreateOrUpdateButton(buttonsGO.transform, "BotonSalir", salirSprite, new Vector2(310f, 35f), new Vector2(280f, 75f));

        // 7. Enlazar referencias en ShiftResultsUI
        FoodDeliveryManager fdm = Object.FindAnyObjectByType<FoodDeliveryManager>();
        ShiftResultsUI resultsUI = Object.FindAnyObjectByType<ShiftResultsUI>();
        if (resultsUI == null && fdm != null)
        {
            resultsUI = fdm.GetComponent<ShiftResultsUI>();
            if (resultsUI == null) resultsUI = fdm.gameObject.AddComponent<ShiftResultsUI>();
        }

        if (resultsUI != null)
        {
            resultsUI.gameOverPanel = panelGO;
            resultsUI.gameOverBackgroundImage = bgImg;
            resultsUI.statsContainer = statsRect;
            resultsUI.gameOverTitleText = titleText;
            resultsUI.gameOverReasonText = reasonText;
            resultsUI.gameOverDeliveriesText = deliveriesText;
            resultsUI.gameOverEarningsText = earningsText;
            resultsUI.gameOverPenaltiesText = penaltiesText;
            resultsUI.gameOverNetProfitText = netText;

            resultsUI.gameOverRestartButton = btnReintentar;
            resultsUI.gameOverMainMenuButton = btnMenu;
            resultsUI.gameOverExitButton = btnSalir;

            resultsUI.gameOverScreenSprite = screenSprite;
            resultsUI.buttonReintentarSprite = reintentarSprite;
            resultsUI.buttonMenuPrincipalSprite = menuSprite;
            resultsUI.buttonSalirSprite = salirSprite;

            EditorUtility.SetDirty(resultsUI);
            Debug.Log("✅ [GameOverUISetup] Referencias vinculadas en ShiftResultsUI.");
        }

        // Dejar visible para que el usuario pueda acomodar posiciones y escalas en el editor
        panelGO.SetActive(true);
        Selection.activeGameObject = panelGO;

        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(hudCanvas.gameObject.scene);
            EditorSceneManager.SaveScene(hudCanvas.gameObject.scene);
            Debug.Log("🎉 [GameOverUISetup] ¡UI de GameOver creada y escena guardada!");
        }
    }

    private static TMP_Text CreateOrUpdateText(Transform parent, string name, string defaultText, float fontSize, FontStyles style, Color color, TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        Transform t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;

        TMP_Text tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        return tmp;
    }

    private static Button CreateOrUpdateButton(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
    {
        Transform t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0f);
        r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.color = Color.white;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        btn.colors = cb;

        if (go.GetComponent<MenuButtonJuice>() == null)
        {
            go.AddComponent<MenuButtonJuice>();
        }

        return btn;
    }
}
#endif
