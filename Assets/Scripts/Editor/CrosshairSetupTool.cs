using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Herramienta de Editor para agregar el punto de mira (Crosshair) con 1 solo clic.
/// Accesible desde: Tools → A-Toda-Olla → Setup First Person Crosshair
/// </summary>
public static class CrosshairSetupTool
{
    [MenuItem("Tools/A-Toda-Olla/Setup First Person Crosshair")]
    public static void Setup()
    {
        // 1. Buscar HUDCanvas
        Canvas hudCanvas = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>())
        {
            if (c.name == "HUDCanvas")
            {
                hudCanvas = c;
                break;
            }
        }

        if (hudCanvas == null)
        {
            // Fallback a cualquier Canvas en la escena
            hudCanvas = Object.FindAnyObjectByType<Canvas>();
        }

        if (hudCanvas == null)
        {
            Debug.LogError("[CrosshairSetup] No se encontró ningún Canvas en la escena activa.");
            EditorUtility.DisplayDialog("Error", "No se encontró ningún Canvas en la escena. Asegúrate de abrir la escena de juego con HUDCanvas.", "OK");
            return;
        }

        // 2. Comprobar si ya existe el objeto FirstPersonCrosshair
        Transform existing = hudCanvas.transform.Find("FirstPersonCrosshair");
        GameObject crosshairGO;

        if (existing != null)
        {
            crosshairGO = existing.gameObject;
            Undo.RecordObject(crosshairGO, "Update First Person Crosshair");
        }
        else
        {
            crosshairGO = new GameObject("FirstPersonCrosshair", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(Outline), typeof(FirstPersonCrosshair));
            Undo.RegisterCreatedObjectUndo(crosshairGO, "Create First Person Crosshair");
            crosshairGO.transform.SetParent(hudCanvas.transform, false);
        }

        // 3. Configurar RectTransform
        RectTransform rt = crosshairGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(5f, 5f);

        // 4. Configurar Image
        Image img = crosshairGO.GetComponent<Image>();
        if (img == null) img = crosshairGO.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0.95f);

        // Generar y asignar sprite circular suave
        img.sprite = CreateSmoothCircleSprite();

        // 5. Configurar CanvasGroup
        CanvasGroup cg = crosshairGO.GetComponent<CanvasGroup>();
        if (cg == null) cg = crosshairGO.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        cg.alpha = 1f;

        // 6. Configurar Outline (para contraste sobre fondos claros/cielo)
        Outline outline = crosshairGO.GetComponent<Outline>();
        if (outline == null) outline = crosshairGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.enabled = true;

        // 7. Configurar Script
        FirstPersonCrosshair crosshairScript = crosshairGO.GetComponent<FirstPersonCrosshair>();
        if (crosshairScript == null) crosshairScript = crosshairGO.AddComponent<FirstPersonCrosshair>();
        crosshairScript.targetCanvas = hudCanvas;
        crosshairScript.crosshairImage = img;
        crosshairScript.dotSize = 5f;
        crosshairScript.dotColor = new Color(1f, 1f, 1f, 0.95f);
        crosshairScript.useOutline = true;
        crosshairScript.hideWhileDriving = true;
        crosshairScript.highlightOnInteractable = true;
        crosshairScript.targetHoverSize = 8f;
        crosshairScript.hoverColor = new Color(0.35f, 1f, 0.6f, 1f);

        // Colocarlo al final o principio de la jerarquía según convenga
        crosshairGO.transform.SetAsLastSibling();

        Selection.activeGameObject = crosshairGO;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[CrosshairSetup] ¡Punto de mira (FirstPersonCrosshair) configurado exitosamente en HUDCanvas!");
        EditorUtility.DisplayDialog("Éxito", "¡Punto de mira (Crosshair) agregado al centro de la pantalla!\n\n- Se oculta automáticamente al conducir.\n- Reacciona suavemente al apuntar a objetos.\n- Puedes ajustar el tamaño o color en el Inspector del objeto 'FirstPersonCrosshair'.", "Entendido");
    }

    private static Sprite CreateSmoothCircleSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Crosshair_Dot_Sprite";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        float radius = (size * 0.5f) - 1.5f;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f)
        );
    }
}
