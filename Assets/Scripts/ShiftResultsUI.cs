using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Modal de Resultados de Fin de Jornada (ShiftResultsUI).
/// Se activa automáticamente al completar la jornada con éxito en la base (Victoria)
/// o al agotarse el tiempo del turno sin cumplir la cuota (Derrota).
/// Congela la partida, libera el cursor, presenta un desglose financiero interactivo
/// y ofrece opciones para avanzar de día, visitar el taller o reiniciar.
/// </summary>
public class ShiftResultsUI : MonoBehaviour
{
    public static ShiftResultsUI Instance { get; private set; }

    [Header("Referencias de UI (Se autogeneran si están vacías)")]
    public CanvasGroup modalCanvasGroup;
    public RectTransform windowRect;
    public Image headerBanner;
    public TMP_Text titleText;
    public TMP_Text subtitleText;
    public TMP_Text starsText;
    public TMP_Text messageText;

    [Header("Estadísticas")]
    public TMP_Text deliveriesStatText;
    public TMP_Text boilingStatText;
    public TMP_Text expiredStatText;
    public TMP_Text baseEarningsText;
    public TMP_Text tipsText;
    public TMP_Text penaltiesText;
    public TMP_Text netProfitText;

    [Header("Botones")]
    public Button nextDayButton;
    public Button restartDayButton;
    public Button workshopButton;
    public Button mainMenuButton;

    [Header("Game Over UI Personalizado (Assets/UI/GameOver)")]
    public GameObject gameOverPanel;
    public Image gameOverBackgroundImage;
    public RectTransform statsContainer;
    public TMP_Text gameOverTitleText;
    public TMP_Text gameOverReasonText;
    public TMP_Text gameOverDeliveriesText;
    public TMP_Text gameOverEarningsText;
    public TMP_Text gameOverPenaltiesText;
    public TMP_Text gameOverNetProfitText;

    [Header("Botones Game Over")]
    public Button gameOverRestartButton;
    public Button gameOverMainMenuButton;
    public Button gameOverExitButton;

    [Header("Sprites de Game Over")]
    public Sprite gameOverScreenSprite;
    public Sprite buttonReintentarSprite;
    public Sprite buttonMenuPrincipalSprite;
    public Sprite buttonSalirSprite;

    [Header("Audio")]
    public AudioClip victoryFanfare;
    public AudioClip defeatSound;
    public AudioClip buttonClickSound;

    [Header("Colores")]
    public Color victoryHeaderColor = new Color(0.12f, 0.78f, 0.42f, 1f); // Verde Esmeralda
    public Color defeatHeaderColor = new Color(0.92f, 0.25f, 0.25f, 1f);  // Rojo Carmesí
    public Color starGoldColor = new Color(1f, 0.85f, 0.2f, 1f);

    private AudioSource audioSource;
    private Coroutine fadeCoroutine;
    private bool isOpen = false;

    public bool IsOpen => isOpen;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.ignoreListenerPause = true; // Sonar con Time.timeScale = 0
        }

        EnsureUI();
        EnsureGameOverUI();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (modalCanvasGroup != null)
        {
            modalCanvasGroup.alpha = 0f;
            modalCanvasGroup.interactable = false;
            modalCanvasGroup.blocksRaycasts = false;
            modalCanvasGroup.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        EnsureGameOverUI();
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        SubscribeToShiftManager();
        SetupButtons();
        SetupGameOverButtons();
    }

    void OnDestroy()
    {
        if (ShiftManager.Instance != null)
        {
            if (ShiftManager.Instance.OnDayCompleted != null)
                ShiftManager.Instance.OnDayCompleted.RemoveListener(HandleDayCompleted);
            if (ShiftManager.Instance.OnDayFailed != null)
                ShiftManager.Instance.OnDayFailed.RemoveListener(HandleDayFailed);
        }
    }

    private void SubscribeToShiftManager()
    {
        if (ShiftManager.Instance != null)
        {
            if (ShiftManager.Instance.OnDayCompleted != null)
            {
                ShiftManager.Instance.OnDayCompleted.RemoveListener(HandleDayCompleted);
                ShiftManager.Instance.OnDayCompleted.AddListener(HandleDayCompleted);
            }
            if (ShiftManager.Instance.OnDayFailed != null)
            {
                ShiftManager.Instance.OnDayFailed.RemoveListener(HandleDayFailed);
                ShiftManager.Instance.OnDayFailed.AddListener(HandleDayFailed);
            }
        }
        else
        {
            StartCoroutine(LateSubscribeRoutine());
        }
    }

    private IEnumerator LateSubscribeRoutine()
    {
        yield return null;
        if (ShiftManager.Instance != null)
        {
            if (ShiftManager.Instance.OnDayCompleted != null)
            {
                ShiftManager.Instance.OnDayCompleted.RemoveListener(HandleDayCompleted);
                ShiftManager.Instance.OnDayCompleted.AddListener(HandleDayCompleted);
            }
            if (ShiftManager.Instance.OnDayFailed != null)
            {
                ShiftManager.Instance.OnDayFailed.RemoveListener(HandleDayFailed);
                ShiftManager.Instance.OnDayFailed.AddListener(HandleDayFailed);
            }
        }
    }

    private void SetupButtons()
    {
        if (nextDayButton != null)
        {
            nextDayButton.onClick.RemoveAllListeners();
            nextDayButton.onClick.AddListener(OnNextDayClicked);
        }

        if (restartDayButton != null)
        {
            restartDayButton.onClick.RemoveAllListeners();
            restartDayButton.onClick.AddListener(OnRestartDayClicked);
        }

        if (workshopButton != null)
        {
            workshopButton.onClick.RemoveAllListeners();
            workshopButton.onClick.AddListener(OnWorkshopClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }
    }

    public void HandleDayCompleted(ShiftManager.ShiftSummaryData summary)
    {
        ShowModal(true, summary, null);
    }

    public void HandleDayFailed(string failReason)
    {
        ShowModal(false, null, failReason);
    }

    private void ShowModal(bool isVictory, ShiftManager.ShiftSummaryData summary, string failReason)
    {
        if (isOpen) return;
        isOpen = true;

        EnsureUI();
        EnsureGameOverUI();

        // 1. Pausar juego y liberar cursor
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 2. Audio
        if (isVictory)
        {
            if (victoryFanfare != null && audioSource != null)
                audioSource.PlayOneShot(victoryFanfare, 0.9f);
        }
        else
        {
            if (defeatSound != null && audioSource != null)
                audioSource.PlayOneShot(defeatSound, 0.9f);
        }

        // 3. Si es DERROTA y tenemos GameOverPanel personalizado configurado
        if (!isVictory && gameOverPanel != null)
        {
            // Ocultar modal genérico
            if (modalCanvasGroup != null) modalCanvasGroup.gameObject.SetActive(false);

            int currentDay = ShiftManager.Instance != null ? ShiftManager.Instance.currentDayIndex : 1;
            int done = ShiftManager.Instance != null ? ShiftManager.Instance.DeliveriesCompletedThisDay : 0;
            int quota = ShiftManager.Instance != null && ShiftManager.Instance.CurrentConfig != null ? ShiftManager.Instance.CurrentConfig.quotaDeliveries : 3;
            int penalties = ShiftManager.Instance != null ? ShiftManager.Instance.TotalPenaltiesThisDay : 0;
            int earned = ShiftManager.Instance != null ? ShiftManager.Instance.TotalEarningsThisDay : 0;
            int net = earned - penalties;
            string reason = !string.IsNullOrEmpty(failReason) ? failReason : "¡Se agotó el tiempo de la jornada!";

            if (gameOverTitleText != null) gameOverTitleText.text = $"DÍA {currentDay} FALLIDO";
            if (gameOverReasonText != null) gameOverReasonText.text = reason;
            if (gameOverDeliveriesText != null) gameOverDeliveriesText.text = $"Ollas entregadas: {done} / {quota}";
            if (gameOverEarningsText != null) gameOverEarningsText.text = $"Ganancias del día: +${earned}";
            if (gameOverPenaltiesText != null) gameOverPenaltiesText.text = $"Multas por comida fría: -${penalties}";
            if (gameOverNetProfitText != null) gameOverNetProfitText.text = $"BALANCE FINAL: ${(net >= 0 ? "+" : "")}${net}";

            SetupGameOverButtons();

            gameOverPanel.transform.SetAsLastSibling();
            gameOverPanel.SetActive(true);

            CanvasGroup cg = gameOverPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
            return;
        }

        // Si es Victoria, asegurar que GameOverPanel esté apagado
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        // 4. Poblar datos visuales de modal de victoria (o fallback)
        if (isVictory && summary != null)
        {
            if (headerBanner != null) headerBanner.color = victoryHeaderColor;
            if (titleText != null) titleText.text = "¡JORNADA COMPLETADA CON ÉXITO!";
            if (subtitleText != null) subtitleText.text = summary.dayTitle;

            // Calificación
            if (starsText != null)
            {
                starsText.text = summary.starRating == 3 
                    ? "CALIFICACIÓN: EXCELENTE (3/3)" 
                    : (summary.starRating == 2 ? "CALIFICACIÓN: BUENA (2/3)" : "CALIFICACIÓN: ACEPTABLE (1/3)");
                starsText.color = starGoldColor;
            }

            if (messageText != null) messageText.text = summary.summaryMessage;

            if (deliveriesStatText != null) deliveriesStatText.text = $"{summary.deliveriesCompleted} / {summary.quotaTarget}";
            if (boilingStatText != null) boilingStatText.text = $"{summary.deliveriesBoiling}";
            if (expiredStatText != null) expiredStatText.text = $"{summary.ordersExpired}";

            if (baseEarningsText != null) baseEarningsText.text = $"+${summary.totalEarnings - summary.totalTips}";
            if (tipsText != null) tipsText.text = $"+${summary.totalTips}";
            if (penaltiesText != null) penaltiesText.text = $"-${summary.totalPenalties}";
            if (netProfitText != null) netProfitText.text = $"+${summary.netProfit}";

            // Botones visibles en victoria
            if (nextDayButton != null) nextDayButton.gameObject.SetActive(true);
            if (workshopButton != null) workshopButton.gameObject.SetActive(true);
            if (restartDayButton != null) restartDayButton.gameObject.SetActive(false);
        }
        else
        {
            // Derrota fallback (si no hay GameOverPanel)
            if (headerBanner != null) headerBanner.color = defeatHeaderColor;
            if (titleText != null) titleText.text = "¡JORNADA FALLIDA!";

            int currentDay = ShiftManager.Instance != null ? ShiftManager.Instance.currentDayIndex : 1;
            if (subtitleText != null) subtitleText.text = $"Día {currentDay}";

            if (starsText != null)
            {
                starsText.text = "DESEMPEÑO: FALLIDO (0/3)";
                starsText.color = defeatHeaderColor;
            }

            if (messageText != null) messageText.text = !string.IsNullOrEmpty(failReason) ? failReason : "Se agotó el tiempo antes de cumplir la meta.";

            int done = ShiftManager.Instance != null ? ShiftManager.Instance.DeliveriesCompletedThisDay : 0;
            int quota = ShiftManager.Instance != null && ShiftManager.Instance.CurrentConfig != null ? ShiftManager.Instance.CurrentConfig.quotaDeliveries : 3;
            int penalties = ShiftManager.Instance != null ? ShiftManager.Instance.TotalPenaltiesThisDay : 0;
            int earned = ShiftManager.Instance != null ? ShiftManager.Instance.TotalEarningsThisDay : 0;

            if (deliveriesStatText != null) deliveriesStatText.text = $"{done} / {quota}";
            if (boilingStatText != null) boilingStatText.text = "-";
            if (expiredStatText != null) expiredStatText.text = $"{ShiftManager.Instance?.OrdersExpiredThisDay ?? 0}";

            if (baseEarningsText != null) baseEarningsText.text = $"+${earned}";
            if (tipsText != null) tipsText.text = "+$0";
            if (penaltiesText != null) penaltiesText.text = $"-${penalties}";
            if (netProfitText != null) netProfitText.text = $"${earned - penalties}";

            // Botones visibles en derrota
            if (nextDayButton != null) nextDayButton.gameObject.SetActive(false);
            if (workshopButton != null) workshopButton.gameObject.SetActive(false);
            if (restartDayButton != null) restartDayButton.gameObject.SetActive(true);
        }

        // 5. Mostrar panel en pantalla al frente
        if (modalCanvasGroup != null)
        {
            modalCanvasGroup.transform.SetAsLastSibling();
            modalCanvasGroup.gameObject.SetActive(true);
            modalCanvasGroup.interactable = true;
            modalCanvasGroup.blocksRaycasts = true;
            modalCanvasGroup.alpha = 1f;
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(AnimateModal(true));
    }

    private void HideModal()
    {
        isOpen = false;
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (modalCanvasGroup != null)
        {
            modalCanvasGroup.interactable = false;
            modalCanvasGroup.blocksRaycasts = false;
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(AnimateModal(false));
    }

    private IEnumerator AnimateModal(bool show)
    {
        if (modalCanvasGroup == null) yield break;

        float startAlpha = modalCanvasGroup.alpha;
        float targetAlpha = show ? 1f : 0f;
        Vector3 startScale = show ? Vector3.one * 0.9f : Vector3.one;
        Vector3 targetScale = show ? Vector3.one : Vector3.one * 0.9f;

        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            modalCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            if (windowRect != null)
            {
                windowRect.localScale = Vector3.Lerp(startScale, targetScale, t);
            }
            yield return null;
        }

        modalCanvasGroup.alpha = targetAlpha;
        if (!show)
        {
            modalCanvasGroup.gameObject.SetActive(false);
        }
    }

    private void OnNextDayClicked()
    {
        PlayClickSound();
        HideModal();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (ShiftManager.Instance != null)
        {
            ShiftManager.Instance.AdvanceToNextDay();
        }
    }

    private void OnRestartDayClicked()
    {
        PlayClickSound();
        HideModal();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (ShiftManager.Instance != null)
        {
            ShiftManager.Instance.RestartCurrentDay();
        }
    }

    private void OnWorkshopClicked()
    {
        PlayClickSound();
        HideModal();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("[ShiftResultsUI] ¡Turno cerrado! Conduce la van al taller mecánico en la ciudad para comprar mejoras.");
    }

    private void OnMainMenuClicked()
    {
        PlayClickSound();
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void OnExitClicked()
    {
        PlayClickSound();
        Time.timeScale = 1f;
        Debug.Log("[ShiftResultsUI] Saliendo del juego...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void EnsureGameOverUI()
    {
        if (gameOverPanel != null)
        {
            BindGameOverPanelReferences(gameOverPanel);
            return;
        }

        // 1. Buscar si ya existe GameOverPanel en la escena (bajo HUDCanvas o cualquier Canvas)
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
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

        if (canvas != null)
        {
            Transform found = canvas.transform.Find("GameOverPanel");
            if (found != null)
            {
                gameOverPanel = found.gameObject;
                BindGameOverPanelReferences(gameOverPanel);
                return;
            }
        }
    }

    public void BindGameOverPanelReferences(GameObject panel)
    {
        if (panel == null) return;
        gameOverPanel = panel;

        if (gameOverBackgroundImage == null)
            gameOverBackgroundImage = panel.transform.Find("GameOverBackground")?.GetComponent<Image>() ?? panel.GetComponentInChildren<Image>();

        if (statsContainer == null)
            statsContainer = panel.transform.Find("MuroEstadisticas") as RectTransform ?? panel.transform.Find("StatsArea") as RectTransform;

        if (statsContainer != null)
        {
            if (gameOverTitleText == null)
                gameOverTitleText = statsContainer.transform.Find("TitleText")?.GetComponent<TMP_Text>();
            if (gameOverReasonText == null)
                gameOverReasonText = statsContainer.transform.Find("ReasonText")?.GetComponent<TMP_Text>();
            if (gameOverDeliveriesText == null)
                gameOverDeliveriesText = statsContainer.transform.Find("DeliveriesText")?.GetComponent<TMP_Text>();
            if (gameOverEarningsText == null)
                gameOverEarningsText = statsContainer.transform.Find("EarningsText")?.GetComponent<TMP_Text>();
            if (gameOverPenaltiesText == null)
                gameOverPenaltiesText = statsContainer.transform.Find("PenaltiesText")?.GetComponent<TMP_Text>();
            if (gameOverNetProfitText == null)
                gameOverNetProfitText = statsContainer.transform.Find("NetProfitText")?.GetComponent<TMP_Text>();
        }

        Transform btnContainer = panel.transform.Find("ContenedorBotones") ?? panel.transform.Find("ButtonsContainer") ?? panel.transform;

        if (gameOverRestartButton == null)
        {
            Transform tr = btnContainer.Find("BotonReintentar");
            if (tr != null) gameOverRestartButton = tr.GetComponent<Button>();
        }

        if (gameOverMainMenuButton == null)
        {
            Transform tr = btnContainer.Find("BotonMenuPrincipal");
            if (tr != null) gameOverMainMenuButton = tr.GetComponent<Button>();
        }

        if (gameOverExitButton == null)
        {
            Transform tr = btnContainer.Find("BotonSalir");
            if (tr != null) gameOverExitButton = tr.GetComponent<Button>();
        }

        SetupGameOverButtons();
    }

    public void SetupGameOverButtons()
    {
        if (gameOverRestartButton != null)
        {
            gameOverRestartButton.onClick.RemoveAllListeners();
            gameOverRestartButton.onClick.AddListener(OnRestartDayClicked);
        }

        if (gameOverMainMenuButton != null)
        {
            gameOverMainMenuButton.onClick.RemoveAllListeners();
            gameOverMainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        if (gameOverExitButton != null)
        {
            gameOverExitButton.onClick.RemoveAllListeners();
            gameOverExitButton.onClick.AddListener(OnExitClicked);
        }
    }

    private void PlayClickSound()
    {
        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.PlayOneShot(buttonClickSound, 0.9f);
        }
    }

    private void EnsureUI()
    {
        if (modalCanvasGroup != null) return;

        // Asegurar que exista un EventSystem en la escena para recibir clics de UI
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

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

        // Overlay raíz a pantalla completa
        GameObject overlayGO = new GameObject("ShiftResultsOverlay", typeof(RectTransform));
        overlayGO.transform.SetParent(canvas.transform, false);
        overlayGO.transform.SetAsLastSibling();

        RectTransform overRect = overlayGO.GetComponent<RectTransform>();
        overRect.anchorMin = Vector2.zero;
        overRect.anchorMax = Vector2.one;
        overRect.offsetMin = Vector2.zero;
        overRect.offsetMax = Vector2.zero;

        Image overBg = overlayGO.AddComponent<Image>();
        overBg.color = new Color(0.04f, 0.05f, 0.08f, 0.88f);

        modalCanvasGroup = overlayGO.AddComponent<CanvasGroup>();

        // Ventana central (Modal Window)
        GameObject windowGO = new GameObject("ResultsWindow", typeof(RectTransform));
        windowGO.transform.SetParent(overlayGO.transform, false);
        windowRect = windowGO.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(500f, 540f);

        Image winBg = windowGO.AddComponent<Image>();
        winBg.color = new Color(0.09f, 0.10f, 0.14f, 0.98f);

        // Banner superior
        GameObject bannerGO = new GameObject("HeaderBanner", typeof(RectTransform));
        bannerGO.transform.SetParent(windowGO.transform, false);
        RectTransform banRect = bannerGO.GetComponent<RectTransform>();
        banRect.anchorMin = new Vector2(0f, 1f);
        banRect.anchorMax = new Vector2(1f, 1f);
        banRect.pivot = new Vector2(0.5f, 1f);
        banRect.sizeDelta = new Vector2(0f, 60f);

        headerBanner = bannerGO.AddComponent<Image>();
        headerBanner.color = victoryHeaderColor;

        // Título en Banner
        GameObject titleGO = new GameObject("TitleText", typeof(RectTransform));
        titleGO.transform.SetParent(bannerGO.transform, false);
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(10f, 0f);
        titleRect.offsetMax = new Vector2(-10f, 0f);

        titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 20;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.text = "¡JORNADA COMPLETADA CON ÉXITO!";

        // Subtítulo del Día
        GameObject subGO = new GameObject("SubtitleText", typeof(RectTransform));
        subGO.transform.SetParent(windowGO.transform, false);
        RectTransform subRect = subGO.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 1f);
        subRect.anchorMax = new Vector2(1f, 1f);
        subRect.pivot = new Vector2(0.5f, 1f);
        subRect.anchoredPosition = new Vector2(0f, -70f);
        subRect.sizeDelta = new Vector2(-40f, 28f);

        subtitleText = subGO.AddComponent<TextMeshProUGUI>();
        subtitleText.fontSize = 18;
        subtitleText.fontStyle = FontStyles.Bold;
        subtitleText.color = new Color(1f, 0.85f, 0.25f, 1f);
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.text = "DÍA 1: PRIMEROS ENCARGOS";

        // Estrellas / Calificación
        GameObject starsGO = new GameObject("StarsText", typeof(RectTransform));
        starsGO.transform.SetParent(windowGO.transform, false);
        RectTransform starsRect = starsGO.GetComponent<RectTransform>();
        starsRect.anchorMin = new Vector2(0f, 1f);
        starsRect.anchorMax = new Vector2(1f, 1f);
        starsRect.pivot = new Vector2(0.5f, 1f);
        starsRect.anchoredPosition = new Vector2(0f, -100f);
        starsRect.sizeDelta = new Vector2(-40f, 36f);

        starsText = starsGO.AddComponent<TextMeshProUGUI>();
        starsText.fontSize = 20;
        starsText.fontStyle = FontStyles.Bold;
        starsText.color = starGoldColor;
        starsText.alignment = TextAlignmentOptions.Center;
        starsText.text = "CALIFICACIÓN: 3 / 3";

        // Mensaje descriptivo
        GameObject msgGO = new GameObject("MessageText", typeof(RectTransform));
        msgGO.transform.SetParent(windowGO.transform, false);
        RectTransform msgRect = msgGO.GetComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0f, 1f);
        msgRect.anchorMax = new Vector2(1f, 1f);
        msgRect.pivot = new Vector2(0.5f, 1f);
        msgRect.anchoredPosition = new Vector2(0f, -140f);
        msgRect.sizeDelta = new Vector2(-40f, 36f);

        messageText = msgGO.AddComponent<TextMeshProUGUI>();
        messageText.fontSize = 13;
        messageText.color = new Color(0.85f, 0.85f, 0.9f, 1f);
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.text = "Todos los clientes quedaron satisfechos.";

        // Contenedor de Estadísticas (Tabla central)
        GameObject statsGO = new GameObject("StatsCard", typeof(RectTransform));
        statsGO.transform.SetParent(windowGO.transform, false);
        RectTransform statsRect = statsGO.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0f, 0f);
        statsRect.anchorMax = new Vector2(1f, 1f);
        statsRect.offsetMin = new Vector2(30f, 100f);
        statsRect.offsetMax = new Vector2(-30f, -185f);

        Image statsBg = statsGO.AddComponent<Image>();
        statsBg.color = new Color(0.13f, 0.15f, 0.20f, 0.95f);

        // Desglose de textos dentro de statsCard
        deliveriesStatText = CreateStatRow(statsGO.transform, "Ollas Entregadas:", "0 / 0", -15f);
        boilingStatText = CreateStatRow(statsGO.transform, "Entregas Rápidas:", "0", -45f);
        expiredStatText = CreateStatRow(statsGO.transform, "Pedidos Expirados:", "0", -75f);
        baseEarningsText = CreateStatRow(statsGO.transform, "Ganancia Base:", "+$0", -105f);
        tipsText = CreateStatRow(statsGO.transform, "Propinas por Velocidad:", "+$0", -135f);
        penaltiesText = CreateStatRow(statsGO.transform, "Multas Descontadas:", "-$0", -165f);
        netProfitText = CreateStatRow(statsGO.transform, "BENEFICIO NETO TOTAL:", "+$0", -200f, true);

        // Contenedor de Botones Inferiores
        GameObject buttonsRow = new GameObject("ButtonsRow", typeof(RectTransform));
        buttonsRow.transform.SetParent(windowGO.transform, false);
        RectTransform btnRowRect = buttonsRow.GetComponent<RectTransform>();
        btnRowRect.anchorMin = new Vector2(0f, 0f);
        btnRowRect.anchorMax = new Vector2(1f, 0f);
        btnRowRect.pivot = new Vector2(0.5f, 0f);
        btnRowRect.anchoredPosition = new Vector2(0f, 25f);
        btnRowRect.sizeDelta = new Vector2(-40f, 50f);

        // Botón Siguiente Día
        nextDayButton = CreateButton(buttonsRow.transform, "NextDayButton", "SIGUIENTE DÍA  >", new Color(0.15f, 0.75f, 0.35f, 1f), new Vector2(-155f, 0f), new Vector2(150f, 44f));
        // Botón Reintentar Día
        restartDayButton = CreateButton(buttonsRow.transform, "RestartButton", "REINTENTAR DÍA", new Color(0.9f, 0.45f, 0.15f, 1f), new Vector2(-155f, 0f), new Vector2(150f, 44f));
        // Botón Taller Mecánico
        workshopButton = CreateButton(buttonsRow.transform, "WorkshopButton", "IR AL TALLER", new Color(0.2f, 0.55f, 0.95f, 1f), new Vector2(5f, 0f), new Vector2(140f, 44f));
        // Botón Menú Principal
        mainMenuButton = CreateButton(buttonsRow.transform, "MainMenuButton", "SALIR AL MENÚ", new Color(0.35f, 0.38f, 0.45f, 1f), new Vector2(165f, 0f), new Vector2(140f, 44f));

        SetupButtons();
    }

    private TMP_Text CreateStatRow(Transform parent, string label, string defaultValue, float yOffset, bool isTotal = false)
    {
        GameObject row = new GameObject("Row_" + label.Replace(" ", "_"), typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform r = row.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0f, yOffset);
        r.sizeDelta = new Vector2(-24f, 24f);

        // Label Izquierda
        GameObject lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(row.transform, false);
        RectTransform lblRect = lblGO.GetComponent<RectTransform>();
        lblRect.anchorMin = new Vector2(0f, 0f);
        lblRect.anchorMax = new Vector2(0.65f, 1f);
        lblRect.offsetMin = Vector2.zero;
        lblRect.offsetMax = Vector2.zero;

        TMP_Text lbl = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.fontSize = isTotal ? 16 : 14;
        lbl.fontStyle = isTotal ? FontStyles.Bold : FontStyles.Normal;
        lbl.color = isTotal ? new Color(1f, 0.85f, 0.25f, 1f) : new Color(0.85f, 0.85f, 0.9f, 1f);
        lbl.text = label;

        // Valor Derecha
        GameObject valGO = new GameObject("Value", typeof(RectTransform));
        valGO.transform.SetParent(row.transform, false);
        RectTransform valRect = valGO.GetComponent<RectTransform>();
        valRect.anchorMin = new Vector2(0.65f, 0f);
        valRect.anchorMax = new Vector2(1f, 1f);
        valRect.offsetMin = Vector2.zero;
        valRect.offsetMax = Vector2.zero;

        TMP_Text val = valGO.AddComponent<TextMeshProUGUI>();
        val.fontSize = isTotal ? 17 : 14;
        val.fontStyle = FontStyles.Bold;
        val.alignment = TextAlignmentOptions.Right;
        val.color = isTotal ? new Color(0.2f, 1f, 0.5f, 1f) : Color.white;
        val.text = defaultValue;

        return val;
    }

    private Button CreateButton(Transform parent, string name, string text, Color color, Vector2 anchoredPos, Vector2 size)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform));
        btnGO.transform.SetParent(parent, false);
        RectTransform btnRect = btnGO.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = anchoredPos;
        btnRect.sizeDelta = size;

        Image img = btnGO.AddComponent<Image>();
        img.color = color;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = color;
        cb.highlightedColor = color * 1.15f;
        cb.pressedColor = color * 0.85f;
        btn.colors = cb;

        // Animación hover juice si existe el componente
        btnGO.AddComponent<MenuButtonJuice>();

        GameObject txtGO = new GameObject("BtnText", typeof(RectTransform));
        txtGO.transform.SetParent(btnGO.transform, false);
        RectTransform txtRect = txtGO.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        TMP_Text t = txtGO.AddComponent<TextMeshProUGUI>();
        t.fontSize = 14;
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.text = text;

        return btn;
    }
}
