using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

/// <summary>
/// Editor tool para crear el RadioPanel en el HUDCanvas de la escena Game.
/// Ejecuta desde: Tools → A-Toda-Olla → Setup Radio HUD
/// </summary>
public static class RadioHUDSetupTool
{
    [MenuItem("Tools/A-Toda-Olla/Setup Radio HUD")]
    public static void Setup()
    {
        // ── 1. Buscar HUDCanvas ───────────────────────────────────────────
        Canvas hudCanvas = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>())
        {
            if (c.name == "HUDCanvas") { hudCanvas = c; break; }
        }

        if (hudCanvas == null)
        {
            Debug.LogError("[RadioHUDSetup] No se encontró HUDCanvas en la escena.");
            return;
        }

        // ── 2. Eliminar RadioPanel previo si existe ───────────────────────
        Transform old = hudCanvas.transform.Find("RadioPanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // ── 3. Crear textura de fondo redondeado (semi-transparente negro) ─
        Texture2D bgTex = new Texture2D(256, 64, TextureFormat.ARGB32, false);
        bgTex.name = "RadioPanel_BG";
        float roundR = 12f;
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                // Esquinas redondeadas
                float cx = Mathf.Clamp(x, roundR, 256f - roundR);
                float cy = Mathf.Clamp(y, roundR, 64f - roundR);
                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= roundR)
                    bgTex.SetPixel(x, y, new Color(0f, 0f, 0f, 0.72f));
                else if (x >= roundR && x <= 256 - roundR && y >= 0 && y <= 64)
                    bgTex.SetPixel(x, y, new Color(0f, 0f, 0f, 0.72f));
                else if (y >= roundR && y <= 64 - roundR)
                    bgTex.SetPixel(x, y, new Color(0f, 0f, 0f, 0.72f));
                else
                    bgTex.SetPixel(x, y, Color.clear);
            }
        }
        bgTex.Apply();
        Sprite bgSprite = Sprite.Create(bgTex, new Rect(0, 0, 256, 64), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.Tight, new Vector4(12f, 12f, 12f, 12f)); // border para 9-slice
        bgSprite.name = "RadioPanel_BG_Sprite";

        // ── 4. RadioPanel ─────────────────────────────────────────────────
        // Posición: top-center (estilo GTA Vice City)
        GameObject panel = new GameObject("RadioPanel", typeof(RectTransform));
        panel.transform.SetParent(hudCanvas.transform, false);
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        // Ancla: centro-superior
        panelRT.anchorMin = new Vector2(0.5f, 1f);
        panelRT.anchorMax = new Vector2(0.5f, 1f);
        panelRT.pivot     = new Vector2(0.5f, 1f);
        panelRT.sizeDelta = new Vector2(520f, 88f);
        panelRT.anchoredPosition = new Vector2(0f, -28f);

        CanvasGroup cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        // Fondo oscuro semi-transparente
        Image bgImg = panel.AddComponent<Image>();
        bgImg.sprite = bgSprite;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.white;
        bgImg.raycastTarget = false;

        // ── 5. Textura de la barra de color de la emisora (lateral izq.) ──
        // Pequeña barra vertical de color a la izquierda (estilo GTA)
        GameObject barGO = new GameObject("StationColorBar", typeof(RectTransform));
        barGO.transform.SetParent(panel.transform, false);
        RectTransform barRT = barGO.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0f, 0f);
        barRT.anchorMax = new Vector2(0f, 1f);
        barRT.pivot     = new Vector2(0f, 0.5f);
        barRT.offsetMin = new Vector2(10f, 10f);
        barRT.offsetMax = new Vector2(16f, -10f);
        barRT.sizeDelta = new Vector2(6f, 0f); // Solo el ancho importa con anclas estiradas

        // Recrear con sizeDelta correcto para barra vertical
        barRT.anchorMin = new Vector2(0f, 0f);
        barRT.anchorMax = new Vector2(0f, 1f);
        barRT.pivot = new Vector2(0f, 0.5f);
        barRT.offsetMin = new Vector2(12f, 8f);
        barRT.offsetMax = new Vector2(18f, -8f);

        Image barImg = barGO.AddComponent<Image>();
        barImg.color = new Color(1f, 0.85f, 0.2f); // Amarillo por defecto; RadioHUD lo cambia
        barImg.raycastTarget = false;

        // ── 6. Texto: Nombre de la Emisora ────────────────────────────────
        GameObject stationNameGO = new GameObject("StationName", typeof(RectTransform));
        stationNameGO.transform.SetParent(panel.transform, false);
        RectTransform stationRT = stationNameGO.GetComponent<RectTransform>();
        stationRT.anchorMin = new Vector2(0f, 0.5f);
        stationRT.anchorMax = new Vector2(1f, 1f);
        stationRT.pivot     = new Vector2(0.5f, 0.5f);
        stationRT.offsetMin = new Vector2(28f, 4f);
        stationRT.offsetMax = new Vector2(-12f, -4f);

        TMP_Text stationTMP = stationNameGO.AddComponent<TextMeshProUGUI>();
        stationTMP.text = "Radio Olla FM";
        stationTMP.fontSize = 22f;
        stationTMP.fontStyle = FontStyles.Bold;
        stationTMP.color = new Color(1f, 0.85f, 0.2f);
        stationTMP.alignment = TextAlignmentOptions.Left;
        stationTMP.overflowMode = TextOverflowModes.Ellipsis;
        stationTMP.raycastTarget = false;

        // ── 7. Texto: Nombre de la Canción ────────────────────────────────
        GameObject trackNameGO = new GameObject("TrackName", typeof(RectTransform));
        trackNameGO.transform.SetParent(panel.transform, false);
        RectTransform trackRT = trackNameGO.GetComponent<RectTransform>();
        trackRT.anchorMin = new Vector2(0f, 0f);
        trackRT.anchorMax = new Vector2(1f, 0.5f);
        trackRT.pivot     = new Vector2(0.5f, 0.5f);
        trackRT.offsetMin = new Vector2(28f, 4f);
        trackRT.offsetMax = new Vector2(-12f, -2f);

        TMP_Text trackTMP = trackNameGO.AddComponent<TextMeshProUGUI>();
        trackTMP.text = "♪  Nombre de la canción";
        trackTMP.fontSize = 16f;
        trackTMP.fontStyle = FontStyles.Normal;
        trackTMP.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        trackTMP.alignment = TextAlignmentOptions.Left;
        trackTMP.overflowMode = TextOverflowModes.Ellipsis;
        trackTMP.raycastTarget = false;

        // ── 8. Agregar RadioHUD al panel ─────────────────────────────────
        RadioHUD radioHUD = panel.AddComponent<RadioHUD>();
        radioHUD.panelGroup     = cg;
        radioHUD.stationNameText = stationTMP;
        radioHUD.trackNameText   = trackTMP;

        // ── 9. RadioPlayer en DeliveryCar ─────────────────────────────────
        // Agregar el componente si no existe
        GameObject car = GameObject.Find("DeliveryCar");
        if (car != null)
        {
            RadioPlayer rp = car.GetComponent<RadioPlayer>();
            if (rp == null)
            {
                rp = car.AddComponent<RadioPlayer>();
                Debug.Log("[RadioHUDSetup] RadioPlayer agregado a DeliveryCar. Asigna las emisoras (RadioStation assets) en el Inspector.");
            }
            else
            {
                Debug.Log("[RadioHUDSetup] RadioPlayer ya existía en DeliveryCar.");
            }
        }
        else
        {
            Debug.LogWarning("[RadioHUDSetup] No se encontró 'DeliveryCar' en la escena. Agrega RadioPlayer manualmente.");
        }

        // ── 10. Marcar escena sucia y guardar ─────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[RadioHUDSetup] RadioPanel creado en HUDCanvas (top-center). ¡Recuerda guardar la escena (Ctrl+S)!");
        EditorUtility.DisplayDialog(
            "Radio HUD",
            "RadioPanel creado en HUDCanvas.\n\n" +
            "Próximos pasos:\n" +
            "1. Crea carpeta: Assets/Audio/Radio/\n" +
            "2. Importa tus MP3/OGG ahí\n" +
            "3. Clic derecho → Create → A-Toda-Olla → Radio Station\n" +
            "4. En cada RadioStation, asigna los AudioClips\n" +
            "5. Arrastra los RadioStation assets al array 'Stations' del RadioPlayer en DeliveryCar\n" +
            "6. (Opcional) Asigna un clip de estática al campo 'Static Clip' del RadioPlayer\n\n" +
            "¡Guarda la escena con Ctrl+S!",
            "OK");
    }
}
