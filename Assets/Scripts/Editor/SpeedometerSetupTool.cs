using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

/// <summary>
/// Herramienta de Editor para crear y configurar la jerarquía del Velocímetro (SpeedometerHUD)
/// en HUDCanvas con pivots centrados, componentes y referencias listos para que el usuario
/// pueda moverlo y escalarlo visualmente en la Scene view.
/// 
/// Se ejecuta desde: Tools → A-Toda-Olla → Setup Speedometer HUD
/// </summary>
public static class SpeedometerSetupTool
{
    private const string BASE_PATH = "Assets/UI/Speedometer/Speedometer_Base.png";
    private const string NEEDLE_PATH = "Assets/UI/Speedometer/Speedometer_Needle.png";
    private const string FUEL_PATH = "Assets/UI/Speedometer/Speedometer_FuelFill.png";

    [MenuItem("Tools/A-Toda-Olla/Setup Speedometer HUD")]
    public static void Setup()
    {
        // 1. Buscar HUDCanvas
        Canvas hudCanvas = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.name == "HUDCanvas") { hudCanvas = c; break; }
        }

        if (hudCanvas == null)
        {
            Debug.LogError("[SpeedometerSetup] No se encontró HUDCanvas en la escena.");
            return;
        }

        // Eliminar instancia previa si existe para reconstruir limpiamente
        Transform old = hudCanvas.transform.Find("SpeedometerHUD");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // Cargar sprites si existen
        Sprite baseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BASE_PATH);
        Sprite needleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(NEEDLE_PATH);
        Sprite fuelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FUEL_PATH);

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF.asset");
        if (fontAsset == null) fontAsset = TMP_Settings.defaultFontAsset;

        // 2. Contenedor Raíz (SpeedometerHUD)
        // Anclado a la esquina inferior izquierda por defecto, con tamaño de proporciones 958x617 ≈ 1.55 (ancho 360, alto 232)
        GameObject rootGO = new GameObject("SpeedometerHUD", typeof(RectTransform));
        rootGO.transform.SetParent(hudCanvas.transform, false);
        RectTransform rootRT = rootGO.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0f, 0f);
        rootRT.anchorMax = new Vector2(0f, 0f);
        rootRT.pivot = new Vector2(0f, 0f);
        rootRT.anchoredPosition = new Vector2(40f, 40f);
        rootRT.sizeDelta = new Vector2(360f, 232f);

        CanvasGroup cg = rootGO.AddComponent<CanvasGroup>();
        cg.alpha = 1f; // Visible en editor
        cg.interactable = false;
        cg.blocksRaycasts = false;

        SpeedometerHUD speedometer = rootGO.AddComponent<SpeedometerHUD>();
        speedometer.canvasGroup = cg;
        speedometer.visibleInEditor = true;

        // 3. Imagen Base (Fondo apernado, dial y medidores)
        GameObject baseGO = new GameObject("Speedometer_Base", typeof(RectTransform));
        baseGO.transform.SetParent(rootGO.transform, false);
        RectTransform baseRT = baseGO.GetComponent<RectTransform>();
        baseRT.anchorMin = Vector2.zero;
        baseRT.anchorMax = Vector2.one;
        baseRT.offsetMin = Vector2.zero;
        baseRT.offsetMax = Vector2.zero;

        Image baseImg = baseGO.AddComponent<Image>();
        if (baseSprite != null) baseImg.sprite = baseSprite;
        baseImg.color = Color.white;
        baseImg.preserveAspect = true;
        baseImg.raycastTarget = false;

        // 4. Centro del dial y Aguja (NeedleHub)
        // En la proporción original (958x617), el centro de la aguja está en:
        // X = (417 - 33) / 958 ≈ 0.401
        // Y = 1.0 - ((353 - 64) / 617) ≈ 0.532
        GameObject needleHubGO = new GameObject("NeedleHub", typeof(RectTransform));
        needleHubGO.transform.SetParent(rootGO.transform, false);
        RectTransform needleHubRT = needleHubGO.GetComponent<RectTransform>();
        needleHubRT.anchorMin = new Vector2(0.401f, 0.532f);
        needleHubRT.anchorMax = new Vector2(0.401f, 0.532f);
        needleHubRT.pivot = new Vector2(0.5f, 0.5f); // ¡Pivote exacto en el eje de rotación!
        needleHubRT.anchoredPosition = Vector2.zero;
        needleHubRT.sizeDelta = new Vector2(28f, 28f);

        // Aguja hija dentro del hub (apunta hacia arriba)
        GameObject needleGO = new GameObject("Needle", typeof(RectTransform));
        needleGO.transform.SetParent(needleHubGO.transform, false);
        RectTransform needleRT = needleGO.GetComponent<RectTransform>();
        needleRT.anchorMin = new Vector2(0.5f, 0.5f);
        needleRT.anchorMax = new Vector2(0.5f, 0.5f);
        needleRT.pivot = new Vector2(0.5f, 0.12f); // Pivote en la base circular de la aguja
        needleRT.anchoredPosition = Vector2.zero;
        needleRT.sizeDelta = new Vector2(22f, 95f);

        Image needleImg = needleGO.AddComponent<Image>();
        if (needleSprite != null) needleImg.sprite = needleSprite;
        else needleImg.color = new Color(0.95f, 0.2f, 0.1f, 1f); // Rojo aguja si no hay sprite
        needleImg.preserveAspect = true;
        needleImg.raycastTarget = false;

        speedometer.needleRect = needleHubRT;

        // 5. Plaquita Digital de Velocidad (TextMeshPro)
        // Ubicada en la parte inferior del dial circular
        GameObject digitalPlateGO = new GameObject("DigitalPlate", typeof(RectTransform));
        digitalPlateGO.transform.SetParent(rootGO.transform, false);
        RectTransform digitalPlateRT = digitalPlateGO.GetComponent<RectTransform>();
        digitalPlateRT.anchorMin = new Vector2(0.26f, 0.16f);
        digitalPlateRT.anchorMax = new Vector2(0.54f, 0.36f);
        digitalPlateRT.offsetMin = Vector2.zero;
        digitalPlateRT.offsetMax = Vector2.zero;

        // Texto del número (ej: 0)
        GameObject speedTextGO = new GameObject("SpeedNumber", typeof(RectTransform));
        speedTextGO.transform.SetParent(digitalPlateGO.transform, false);
        RectTransform speedTextRT = speedTextGO.GetComponent<RectTransform>();
        speedTextRT.anchorMin = new Vector2(0f, 0.25f);
        speedTextRT.anchorMax = new Vector2(1f, 1f);
        speedTextRT.offsetMin = Vector2.zero;
        speedTextRT.offsetMax = Vector2.zero;

        TMP_Text speedTMP = speedTextGO.AddComponent<TextMeshProUGUI>();
        speedTMP.font = fontAsset;
        speedTMP.text = "0";
        speedTMP.fontSize = 24f;
        speedTMP.fontStyle = FontStyles.Bold;
        speedTMP.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        speedTMP.alignment = TextAlignmentOptions.Center;
        speedTMP.raycastTarget = false;

        speedometer.speedText = speedTMP;

        // Texto inferior "km/h"
        GameObject kmhGO = new GameObject("KmhLabel", typeof(RectTransform));
        kmhGO.transform.SetParent(digitalPlateGO.transform, false);
        RectTransform kmhRT = kmhGO.GetComponent<RectTransform>();
        kmhRT.anchorMin = new Vector2(0f, 0f);
        kmhRT.anchorMax = new Vector2(1f, 0.35f);
        kmhRT.offsetMin = Vector2.zero;
        kmhRT.offsetMax = Vector2.zero;

        TMP_Text kmhTMP = kmhGO.AddComponent<TextMeshProUGUI>();
        kmhTMP.font = fontAsset;
        kmhTMP.text = "km/h";
        kmhTMP.fontSize = 11f;
        kmhTMP.fontStyle = FontStyles.Bold;
        kmhTMP.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        kmhTMP.alignment = TextAlignmentOptions.Center;
        kmhTMP.raycastTarget = false;

        // 6. Medidor de Gasolina
        // Ubicado en la sección roja derecha
        GameObject fuelSectionGO = new GameObject("FuelSection", typeof(RectTransform));
        fuelSectionGO.transform.SetParent(rootGO.transform, false);
        RectTransform fuelSectionRT = fuelSectionGO.GetComponent<RectTransform>();
        fuelSectionRT.anchorMin = new Vector2(0.72f, 0.22f);
        fuelSectionRT.anchorMax = new Vector2(0.92f, 0.40f);
        fuelSectionRT.offsetMin = Vector2.zero;
        fuelSectionRT.offsetMax = Vector2.zero;

        // Relleno de las barritas (Filled Horizontal)
        GameObject fuelFillGO = new GameObject("FuelFill", typeof(RectTransform));
        fuelFillGO.transform.SetParent(fuelSectionGO.transform, false);
        RectTransform fuelFillRT = fuelFillGO.GetComponent<RectTransform>();
        fuelFillRT.anchorMin = Vector2.zero;
        fuelFillRT.anchorMax = Vector2.one;
        fuelFillRT.offsetMin = Vector2.zero;
        fuelFillRT.offsetMax = Vector2.zero;

        Image fuelFillImg = fuelFillGO.AddComponent<Image>();
        if (fuelSprite != null) fuelFillImg.sprite = fuelSprite;
        else fuelFillImg.color = new Color(0.2f, 0.85f, 0.2f, 1f); // Verde por defecto
        fuelFillImg.type = Image.Type.Filled;
        fuelFillImg.fillMethod = Image.FillMethod.Horizontal;
        fuelFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fuelFillImg.fillAmount = 0.85f;
        fuelFillImg.raycastTarget = false;

        speedometer.fuelFillImage = fuelFillImg;

        // Guardar cambios en la escena
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log("[SpeedometerSetup] ¡SpeedometerHUD creado y configurado con éxito en HUDCanvas!");
    }
}
