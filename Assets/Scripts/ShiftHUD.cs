using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD dedicado para la Jornada Laboral (Shift) en A Toda Olla.
/// Se ubica en la esquina superior derecha y muestra:
/// - El día actual (ej: "DÍA 1").
/// - El reloj digital contrarreloj de la jornada (ej: "⏰ 03:45").
/// - La cuota de entregas requerida con barra de progreso interactiva (ej: "ENTREGAS: 2 / 3").
/// Auto-genera su interfaz si no está preconfigurada en el Canvas.
/// </summary>
public class ShiftHUD : MonoBehaviour
{
    [Header("Referencias de UI (Opcionales - Se autogeneran si están vacías)")]
    public RectTransform hudRoot;
    public TMP_Text dayTitleText;
    public TMP_Text clockText;
    public TMP_Text quotaText;
    public Image quotaFillBar;
    public Image panelBackground;

    [Header("Colores y Estilos")]
    public Color normalClockColor = Color.white;
    public Color warningClockColor = new Color(1f, 0.85f, 0.2f, 1f);     // Amarillo dorado (< 90s)
    public Color criticalClockColor = new Color(1f, 0.28f, 0.28f, 1f);   // Rojo alerta (< 45s)
    public Color quotaNormalColor = new Color(0.9f, 0.9f, 0.95f, 1f);
    public Color quotaCompleteColor = new Color(0.2f, 1f, 0.45f, 1f);    // Verde esmeralda brillante

    [Header("Animación")]
    public float popScale = 1.15f;
    public float popDuration = 0.3f;

    // Estado interno
    private ShiftManager cachedShiftManager;
    private int lastDisplayedDeliveries = -1;
    private Coroutine popCoroutine;
    private Vector3 originalScale = Vector3.one;

    void Awake()
    {
        EnsureUI();
        if (hudRoot != null) originalScale = hudRoot.localScale;
    }

    void Start()
    {
        FindShiftManager();
        RefreshDisplay();
    }

    void Update()
    {
        if (cachedShiftManager == null)
        {
            FindShiftManager();
            if (cachedShiftManager == null) return;
        }

        UpdateClockDisplay();
        UpdateQuotaDisplay();
    }

    private void FindShiftManager()
    {
        if (cachedShiftManager != null) return;

        cachedShiftManager = ShiftManager.Instance ?? Object.FindAnyObjectByType<ShiftManager>();
        if (cachedShiftManager != null && cachedShiftManager.OnQuotaReached != null)
        {
            cachedShiftManager.OnQuotaReached.RemoveListener(OnQuotaReachedHandler);
            cachedShiftManager.OnQuotaReached.AddListener(OnQuotaReachedHandler);
        }
    }

    private void OnDestroy()
    {
        if (cachedShiftManager != null && cachedShiftManager.OnQuotaReached != null)
        {
            cachedShiftManager.OnQuotaReached.RemoveListener(OnQuotaReachedHandler);
        }
    }

    private void EnsureUI()
    {
        if (hudRoot != null && dayTitleText != null && clockText != null && quotaText != null) return;

        // Buscar si ya existe un panel llamado ShiftHUDPanel
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            foreach (var c in canvases)
            {
                if (c.name.Contains("HUD"))
                {
                    canvas = c;
                    break;
                }
            }
            if (canvas == null && canvases.Length > 0) canvas = canvases[0];
        }

        if (canvas == null) return;

        // Crear contenedor raíz en la esquina superior derecha
        GameObject panelGO = new GameObject("ShiftHUDPanel", typeof(RectTransform));
        panelGO.transform.SetParent(canvas.transform, false);

        hudRoot = panelGO.GetComponent<RectTransform>();
        hudRoot.anchorMin = new Vector2(1f, 1f);
        hudRoot.anchorMax = new Vector2(1f, 1f);
        hudRoot.pivot = new Vector2(1f, 1f);
        hudRoot.anchoredPosition = new Vector2(-25f, -25f);
        hudRoot.sizeDelta = new Vector2(290f, 84f);

        // Fondo oscuro translúcido
        panelBackground = panelGO.AddComponent<Image>();
        panelBackground.color = new Color(0.08f, 0.09f, 0.12f, 0.88f);

        // ── FILA SUPERIOR: Día (Izquierda) y Reloj (Derecha) ──
        GameObject topRow = new GameObject("TopRow", typeof(RectTransform));
        topRow.transform.SetParent(panelGO.transform, false);
        RectTransform topRect = topRow.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 0.48f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.offsetMin = new Vector2(14f, 0f);
        topRect.offsetMax = new Vector2(-14f, -6f);

        // Texto del Día
        GameObject dayGO = new GameObject("DayText", typeof(RectTransform));
        dayGO.transform.SetParent(topRow.transform, false);
        RectTransform dayRect = dayGO.GetComponent<RectTransform>();
        dayRect.anchorMin = new Vector2(0f, 0f);
        dayRect.anchorMax = new Vector2(0.55f, 1f);
        dayRect.offsetMin = Vector2.zero;
        dayRect.offsetMax = Vector2.zero;

        dayTitleText = dayGO.AddComponent<TextMeshProUGUI>();
        dayTitleText.fontSize = 17;
        dayTitleText.fontStyle = FontStyles.Bold;
        dayTitleText.color = new Color(1f, 0.85f, 0.25f, 1f); // Dorado cálido
        dayTitleText.alignment = TextAlignmentOptions.MidlineLeft;
        dayTitleText.text = "DÍA 1";

        // Texto del Reloj de Turno
        GameObject clockGO = new GameObject("ClockText", typeof(RectTransform));
        clockGO.transform.SetParent(topRow.transform, false);
        RectTransform clockRect = clockGO.GetComponent<RectTransform>();
        clockRect.anchorMin = new Vector2(0.55f, 0f);
        clockRect.anchorMax = new Vector2(1f, 1f);
        clockRect.offsetMin = Vector2.zero;
        clockRect.offsetMax = Vector2.zero;

        clockText = clockGO.AddComponent<TextMeshProUGUI>();
        clockText.fontSize = 20;
        clockText.fontStyle = FontStyles.Bold;
        clockText.color = Color.white;
        clockText.alignment = TextAlignmentOptions.MidlineRight;
        clockText.text = "04:00";

        // ── FILA INFERIOR: Contador de Cuota y Barra ──
        GameObject bottomRow = new GameObject("BottomRow", typeof(RectTransform));
        bottomRow.transform.SetParent(panelGO.transform, false);
        RectTransform botRect = bottomRow.GetComponent<RectTransform>();
        botRect.anchorMin = new Vector2(0f, 0f);
        botRect.anchorMax = new Vector2(1f, 0.48f);
        botRect.offsetMin = new Vector2(14f, 8f);
        botRect.offsetMax = new Vector2(-14f, 0f);

        // Texto de Cuota
        GameObject quotaGO = new GameObject("QuotaText", typeof(RectTransform));
        quotaGO.transform.SetParent(bottomRow.transform, false);
        RectTransform quotaRect = quotaGO.GetComponent<RectTransform>();
        quotaRect.anchorMin = new Vector2(0f, 0.35f);
        quotaRect.anchorMax = new Vector2(1f, 1f);
        quotaRect.offsetMin = Vector2.zero;
        quotaRect.offsetMax = Vector2.zero;

        quotaText = quotaGO.AddComponent<TextMeshProUGUI>();
        quotaText.fontSize = 15;
        quotaText.fontStyle = FontStyles.Bold;
        quotaText.color = quotaNormalColor;
        quotaText.alignment = TextAlignmentOptions.MidlineLeft;
        quotaText.text = "ENTREGAS: 0 / 3";

        // Barra de progreso de cuota (Fondo)
        GameObject barBgGO = new GameObject("QuotaBarBg", typeof(RectTransform));
        barBgGO.transform.SetParent(bottomRow.transform, false);
        RectTransform barBgRect = barBgGO.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0f, 0f);
        barBgRect.anchorMax = new Vector2(1f, 0.28f);
        barBgRect.offsetMin = Vector2.zero;
        barBgRect.offsetMax = Vector2.zero;

        Image barBgImg = barBgGO.AddComponent<Image>();
        barBgImg.color = new Color(0.2f, 0.22f, 0.28f, 0.9f);

        // Barra de progreso de cuota (Relleno)
        GameObject barFillGO = new GameObject("QuotaBarFill", typeof(RectTransform));
        barFillGO.transform.SetParent(barBgGO.transform, false);
        RectTransform barFillRect = barFillGO.GetComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = Vector2.one;
        barFillRect.offsetMin = Vector2.zero;
        barFillRect.offsetMax = Vector2.zero;

        quotaFillBar = barFillGO.AddComponent<Image>();
        quotaFillBar.type = Image.Type.Filled;
        quotaFillBar.fillMethod = Image.FillMethod.Horizontal;
        quotaFillBar.fillOrigin = 0;
        quotaFillBar.fillAmount = 0f;
        quotaFillBar.color = new Color(1f, 0.85f, 0.25f, 1f);
    }

    private void RefreshDisplay()
    {
        if (cachedShiftManager == null) return;

        if (dayTitleText != null)
        {
            dayTitleText.text = $"DÍA {cachedShiftManager.currentDayIndex}";
        }

        UpdateClockDisplay();
        UpdateQuotaDisplay();
    }

    private void UpdateClockDisplay()
    {
        if (cachedShiftManager == null || clockText == null) return;

        float remaining = cachedShiftManager.ShiftTimeRemaining;
        int totalSecs = Mathf.CeilToInt(remaining);
        int mins = totalSecs / 60;
        int secs = totalSecs % 60;

        clockText.text = $"{mins:00}:{secs:00}";

        // Tensión cromática según tiempo restante
        if (remaining <= 45f)
        {
            // Parpadeo crítico
            bool blink = Mathf.PingPong(Time.time * 5f, 1f) > 0.4f;
            clockText.color = blink ? criticalClockColor : warningClockColor;
        }
        else if (remaining <= 90f)
        {
            clockText.color = warningClockColor;
        }
        else
        {
            clockText.color = normalClockColor;
        }
    }

    private void UpdateQuotaDisplay()
    {
        if (cachedShiftManager == null) return;

        int current = cachedShiftManager.DeliveriesCompletedThisDay;
        int target = cachedShiftManager.CurrentConfig != null ? cachedShiftManager.CurrentConfig.quotaDeliveries : 3;

        // Detectar si aumentó para reproducir juice pop
        if (lastDisplayedDeliveries >= 0 && current > lastDisplayedDeliveries)
        {
            PlayPopJuice();
        }
        lastDisplayedDeliveries = current;

        float progress = target > 0 ? Mathf.Clamp01((float)current / target) : 0f;
        if (quotaFillBar != null)
        {
            quotaFillBar.fillAmount = progress;
            quotaFillBar.color = progress >= 1f ? quotaCompleteColor : new Color(1f, 0.85f, 0.25f, 1f);
        }

        if (quotaText != null)
        {
            if (cachedShiftManager.CurrentState == ShiftManager.ShiftState.ReturningToBase)
            {
                quotaText.text = "[CUOTA COMPLETA] REGRESA A LA BASE";
                quotaText.color = quotaCompleteColor;
            }
            else if (cachedShiftManager.CurrentState == ShiftManager.ShiftState.DayCompleted)
            {
                quotaText.text = "¡JORNADA TERMINADA!";
                quotaText.color = quotaCompleteColor;
            }
            else if (cachedShiftManager.CurrentState == ShiftManager.ShiftState.DayFailed)
            {
                quotaText.text = "[FALLIDA] JORNADA FALLIDA";
                quotaText.color = criticalClockColor;
            }
            else
            {
                quotaText.text = $"ENTREGAS: {current} / {target}";
                quotaText.color = quotaNormalColor;
            }
        }
    }

    private void OnQuotaReachedHandler()
    {
        PlayPopJuice();
    }

    private void PlayPopJuice()
    {
        if (hudRoot == null) return;

        if (popCoroutine != null) StopCoroutine(popCoroutine);
        popCoroutine = StartCoroutine(PopRoutine());
    }

    private IEnumerator PopRoutine()
    {
        float half = popDuration * 0.5f;
        float elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / half;
            hudRoot.localScale = Vector3.Lerp(originalScale, originalScale * popScale, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / half;
            hudRoot.localScale = Vector3.Lerp(originalScale * popScale, originalScale, t);
            yield return null;
        }

        hudRoot.localScale = originalScale;
        popCoroutine = null;
    }
}
