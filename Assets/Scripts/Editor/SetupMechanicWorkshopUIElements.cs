using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Linq;

/// <summary>
/// Herramienta de Editor para autogenerar y enlazar los elementos funcionales
/// de las 3 mejoras (Ruedas, Aislamiento Térmico, Motor) en el panel del taller.
/// Menú: Tools → A-Toda-Olla → Configurar Filas de Mejoras en Taller (UI)
/// </summary>
public static class SetupMechanicWorkshopUIElements
{
    [MenuItem("Tools/A-Toda-Olla/Configurar Filas de Mejoras en Taller (UI)")]
    public static void SetupUI()
    {
        var ui = Object.FindAnyObjectByType<MechanicWorkshopUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            Debug.LogError("[SetupUI] No se encontró MechanicWorkshopUI en la escena actual.");
            return;
        }

        Transform parentPanel = ui.panelGroup != null ? ui.panelGroup.transform : ui.transform;

        // 1. Cargar sprites
        Sprite botonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Mecanico/BotonMejorar.png");
        var allStarAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/UI/Mecanico/StarsBarSpriteSheet.png");
        var starSpritesList = allStarAssets.OfType<Sprite>().OrderBy(s => s.name).ToList();

        if (starSpritesList.Count >= 4)
        {
            ui.starBarSprites = starSpritesList.Take(4).ToArray();
        }

        // Obtener fuente de un texto existente para mantener el mismo estilo
        TMP_FontAsset fontAsset = null;
        if (ui.workshopMoneyText != null)
            fontAsset = ui.workshopMoneyText.font;
        else if (ui.exitHintText != null)
            fontAsset = ui.exitHintText.font;

        // Posiciones Y aproximadas para las 3 tarjetas de la izquierda del fondo
        // (El usuario luego ajustará las posiciones y escalas exactas a su gusto)
        float[] yPositions = new float[] { 220f, 20f, -180f };

        // 2. Crear o enlazar Ruedas
        CreateOrUpdateCategoryRow(
            parentPanel,
            "Row_Ruedas",
            yPositions[0],
            botonSprite,
            starSpritesList.FirstOrDefault(),
            fontAsset,
            out ui.wheelsStarBar,
            out ui.wheelsBuyButton,
            out ui.wheelsPriceText,
            out ui.wheelsDescText
        );

        // 3. Crear o enlazar Aislamiento Térmico
        CreateOrUpdateCategoryRow(
            parentPanel,
            "Row_AislamientoTermico",
            yPositions[1],
            botonSprite,
            starSpritesList.FirstOrDefault(),
            fontAsset,
            out ui.thermalStarBar,
            out ui.thermalBuyButton,
            out ui.thermalPriceText,
            out ui.thermalDescText
        );

        // 4. Crear o enlazar Motor
        CreateOrUpdateCategoryRow(
            parentPanel,
            "Row_Motor",
            yPositions[2],
            botonSprite,
            starSpritesList.FirstOrDefault(),
            fontAsset,
            out ui.engineStarBar,
            out ui.engineBuyButton,
            out ui.enginePriceText,
            out ui.engineDescText
        );

        // 5. Refrescar estado y marcar escena como modificada
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);

        Debug.Log("[SetupUI] ✅ Filas de mejoras creadas y cableadas a MechanicWorkshopUI exitosamente. Puedes moverlas y escalarlas en la vista de escena.");
    }

    private static void CreateOrUpdateCategoryRow(
        Transform parent,
        string rowName,
        float defaultPosY,
        Sprite buttonSprite,
        Sprite starBarSprite,
        TMP_FontAsset font,
        out Image starBarImg,
        out Button buyBtn,
        out TMP_Text priceText,
        out TMP_Text descText)
    {
        Transform existingRow = parent.Find(rowName);
        GameObject rowGO;

        if (existingRow == null)
        {
            rowGO = new GameObject(rowName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(rowGO, "Crear " + rowName);
            rowGO.transform.SetParent(parent, false);

            var rt = rowGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-460f, defaultPosY);
            rt.sizeDelta = new Vector2(400f, 160f);
        }
        else
        {
            rowGO = existingRow.gameObject;
        }

        // Sub-objeto: Barra de Estrellas
        Transform starT = rowGO.transform.Find("StarsBar");
        if (starT == null)
        {
            var starGO = new GameObject("StarsBar", typeof(RectTransform), typeof(Image));
            starGO.transform.SetParent(rowGO.transform, false);
            var starRt = starGO.GetComponent<RectTransform>();
            starRt.anchoredPosition = new Vector2(60f, 30f);
            starRt.sizeDelta = new Vector2(180f, 50f);
            starT = starGO.transform;
        }
        starBarImg = starT.GetComponent<Image>();
        if (starBarSprite != null)
            starBarImg.sprite = starBarSprite;
        starBarImg.preserveAspect = true;

        // Sub-objeto: Botón MEJORAR
        Transform btnT = rowGO.transform.Find("Btn_Mejorar");
        if (btnT == null)
        {
            var btnGO = new GameObject("Btn_Mejorar", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(rowGO.transform, false);
            var btnRt = btnGO.GetComponent<RectTransform>();
            btnRt.anchoredPosition = new Vector2(100f, -35f);
            btnRt.sizeDelta = new Vector2(150f, 45f);
            btnT = btnGO.transform;
        }
        var btnImg = btnT.GetComponent<Image>();
        if (buttonSprite != null)
            btnImg.sprite = buttonSprite;
        btnImg.preserveAspect = true;
        buyBtn = btnT.GetComponent<Button>();

        // Sub-objeto: Texto de Precio
        Transform priceT = rowGO.transform.Find("Text_Precio");
        if (priceT == null)
        {
            var priceGO = new GameObject("Text_Precio", typeof(RectTransform), typeof(TextMeshProUGUI));
            priceGO.transform.SetParent(rowGO.transform, false);
            var pRt = priceGO.GetComponent<RectTransform>();
            pRt.anchoredPosition = new Vector2(-15f, -35f);
            pRt.sizeDelta = new Vector2(120f, 40f);
            priceT = priceGO.transform;
        }
        var tmpPrice = priceT.GetComponent<TextMeshProUGUI>();
        if (font != null) tmpPrice.font = font;
        tmpPrice.fontSize = 24f;
        tmpPrice.fontStyle = FontStyles.Bold;
        tmpPrice.alignment = TextAlignmentOptions.Right;
        tmpPrice.color = new Color(0.95f, 0.85f, 0.3f); // Tono dorado suave para el precio
        priceText = tmpPrice;

        // Sub-objeto: Texto de Descripción opcional
        Transform descT = rowGO.transform.Find("Text_Descripcion");
        if (descT != null)
        {
            descText = descT.GetComponent<TMP_Text>();
        }
        else
        {
            descText = null;
        }
    }
}
