using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Controlador principal de la vista de mejoras del mecánico (MecanicoFondo.png).
/// Se abre al entrar con la van al cilindro azul y mantener pulsada la tecla [F].
/// Detiene temporalmente los controles del vehículo, muestra el cursor del ratón,
/// y permite salir con [ESC], [F] o botón de interfaz para volver a conducir.
///
/// Ahora también incluye la lógica de compra de mejoras (Ruedas, Motor, Aislamiento).
/// </summary>
public class MechanicWorkshopUI : MonoBehaviour
{
    private static MechanicWorkshopUI instance;
    public static MechanicWorkshopUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindAnyObjectByType<MechanicWorkshopUI>(FindObjectsInactive.Include);
            }
            return instance;
        }
    }

    [Header("Referencias de UI")]
    [Tooltip("CanvasGroup del panel raíz del taller.")]
    public CanvasGroup panelGroup;

    [Tooltip("Imagen que muestra el fondo del taller (MecanicoFondo.png).")]
    public Image backgroundImage;

    [Tooltip("Botón opcional para cerrar o salir del taller.")]
    public Button closeButton;

    [Tooltip("Texto de ayuda de salida (ej: '[ESC] o [F] Volver a la ciudad').")]
    public TMP_Text exitHintText;

    [Header("Dinero del Jugador")]
    [Tooltip("Texto TMP donde se muestra el dinero real del jugador en el taller.")]
    public TextMeshProUGUI workshopMoneyText;

    // ── Sección Ruedas ──────────────────────────────────────────────────
    [Header("Ruedas (Wheels)")]
    [Tooltip("Texto que muestra el nivel actual de Ruedas.")]
    public TMP_Text wheelsLevelText;
    [Tooltip("Texto con el precio del siguiente nivel de Ruedas.")]
    public TMP_Text wheelsPriceText;
    [Tooltip("Texto con la descripción del siguiente nivel de Ruedas.")]
    public TMP_Text wheelsDescText;
    [Tooltip("Botón para comprar la siguiente mejora de Ruedas.")]
    public Button wheelsBuyButton;

    // ── Sección Motor ───────────────────────────────────────────────────
    [Header("Motor (Engine)")]
    [Tooltip("Texto que muestra el nivel actual de Motor.")]
    public TMP_Text engineLevelText;
    [Tooltip("Texto con el precio del siguiente nivel de Motor.")]
    public TMP_Text enginePriceText;
    [Tooltip("Texto con la descripción del siguiente nivel de Motor.")]
    public TMP_Text engineDescText;
    [Tooltip("Botón para comprar la siguiente mejora de Motor.")]
    public Button engineBuyButton;

    // ── Sección Aislamiento Térmico ─────────────────────────────────────
    [Header("Aislamiento Térmico (Thermal)")]
    [Tooltip("Texto que muestra el nivel actual de Aislamiento.")]
    public TMP_Text thermalLevelText;
    [Tooltip("Texto con el precio del siguiente nivel de Aislamiento.")]
    public TMP_Text thermalPriceText;
    [Tooltip("Texto con la descripción del siguiente nivel de Aislamiento.")]
    public TMP_Text thermalDescText;
    [Tooltip("Botón para comprar la siguiente mejora de Aislamiento.")]
    public Button thermalBuyButton;

    // ── Barras de Estrellas ────────────────────────────────────────────
    [Header("Barras de Estrellas (Spritesheet)")]
    [Tooltip("Los 4 sprites correspondientes a 0★, 1★, 2★ y 3★ del StarsBarSpriteSheet.")]
    public Sprite[] starBarSprites = new Sprite[4];

    [Tooltip("Imagen de la barra de estrellas para Ruedas.")]
    public Image wheelsStarBar;
    [Tooltip("Imagen de la barra de estrellas para Motor.")]
    public Image engineStarBar;
    [Tooltip("Imagen de la barra de estrellas para Aislamiento Térmico.")]
    public Image thermalStarBar;

    [Tooltip("Si es true, el botón se deshabilita visualmente si no hay dinero. Si es false, se puede pulsar para escuchar el sonido de error y ver el flash de dinero.")]
    public bool disableButtonWhenNoFunds = false;

    [Header("Audio")]
    public AudioClip openSound;
    public AudioClip closeSound;
    [Tooltip("Sonido al comprar una mejora con éxito.")]
    public AudioClip purchaseSuccessSound;
    [Tooltip("Sonido al intentar comprar sin dinero.")]
    public AudioClip purchaseFailSound;

    [Header("Animación")]
    public float fadeDuration = 0.2f;

    // Estado público
    public bool IsOpen => isOpen;

    private bool isOpen = false;
    private Coroutine fadeCoroutine;
    private AudioSource audioSource;

    private VehicleController currentVehicle;
    private RadioPlayer currentRadioPlayer;
    private float preWorkshopRadioVolume = 1f;
    private float openCooldownTimer = 0f;

    void Awake()
    {
        instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.ignoreListenerPause = true;
        }

        if (panelGroup == null)
        {
            panelGroup = GetComponent<CanvasGroup>();
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseWorkshop);
        }

        // Cablear botones de compra
        if (wheelsBuyButton != null)
            wheelsBuyButton.onClick.AddListener(() => TryPurchase(UpgradeType.Wheels));
        if (engineBuyButton != null)
            engineBuyButton.onClick.AddListener(() => TryPurchase(UpgradeType.Engine));
        if (thermalBuyButton != null)
            thermalBuyButton.onClick.AddListener(() => TryPurchase(UpgradeType.ThermalInsulation));

        // Iniciar completamente transparente y desactivado al arrancar el juego
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
            panelGroup.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        if (FoodDeliveryManager.Instance != null)
        {
            FoodDeliveryManager.Instance.OnScoreOrMoneyChanged.RemoveListener(OnMoneyChanged);
            FoodDeliveryManager.Instance.OnScoreOrMoneyChanged.AddListener(OnMoneyChanged);
        }

        if (VehicleUpgradeManager.Instance != null)
        {
            VehicleUpgradeManager.Instance.OnUpgradeChanged -= RefreshUpgradeUI;
            VehicleUpgradeManager.Instance.OnUpgradeChanged += RefreshUpgradeUI;
        }

        RefreshMoneyDisplay();
        RefreshUpgradeUI();
    }

    void OnDisable()
    {
        if (FoodDeliveryManager.Instance != null)
            FoodDeliveryManager.Instance.OnScoreOrMoneyChanged.RemoveListener(OnMoneyChanged);

        if (VehicleUpgradeManager.Instance != null)
            VehicleUpgradeManager.Instance.OnUpgradeChanged -= RefreshUpgradeUI;
    }

    void Start()
    {
        if (FoodDeliveryManager.Instance != null)
        {
            FoodDeliveryManager.Instance.OnScoreOrMoneyChanged.RemoveListener(OnMoneyChanged);
            FoodDeliveryManager.Instance.OnScoreOrMoneyChanged.AddListener(OnMoneyChanged);
        }

        if (VehicleUpgradeManager.Instance != null)
        {
            VehicleUpgradeManager.Instance.OnUpgradeChanged -= RefreshUpgradeUI;
            VehicleUpgradeManager.Instance.OnUpgradeChanged += RefreshUpgradeUI;
        }

        RefreshMoneyDisplay();
        RefreshUpgradeUI();
    }

    void Update()
    {
        if (openCooldownTimer > 0f)
        {
            openCooldownTimer -= Time.unscaledDeltaTime;
        }

        if (!isOpen) return;

        // Comprobar si se presiona ESC o F para salir del taller
        if (openCooldownTimer <= 0f)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame)
                {
                    CloseWorkshop();
                    return;
                }
            }

            var gp = Gamepad.current;
            if (gp != null && (gp.bButton.wasPressedThisFrame || gp.startButton.wasPressedThisFrame))
            {
                CloseWorkshop();
                return;
            }

            try
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F))
                {
                    CloseWorkshop();
                    return;
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Abre la vista del taller mecánico.
    /// </summary>
    public void OpenWorkshop(VehicleController vehicle = null)
    {
        gameObject.SetActive(true);
        if (isOpen) return;
        isOpen = true;
        openCooldownTimer = 0.35f; // Evita que la misma pulsación de F lo cierre al instante

        currentVehicle = vehicle;
        if (currentVehicle == null)
        {
            currentVehicle = Object.FindAnyObjectByType<VehicleController>();
        }

        // 1. Detener físicas e inhabilitar control del vehículo mientras se está en el menú
        if (currentVehicle != null)
        {
            Rigidbody vRb = currentVehicle.GetComponent<Rigidbody>();
            if (vRb != null)
            {
                vRb.linearVelocity = Vector3.zero;
                vRb.angularVelocity = Vector3.zero;
            }
            currentVehicle.enabled = false;

            currentRadioPlayer = currentVehicle.GetComponent<RadioPlayer>();
            if (currentRadioPlayer != null)
            {
                // Atenuar sutilmente la radio mientras está en el taller
                preWorkshopRadioVolume = currentRadioPlayer.volume;
                currentRadioPlayer.SetVolume(preWorkshopRadioVolume * 0.35f);
            }
        }

        // 2. Liberar el cursor para navegar en la interfaz
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3. Activar panel UI
        if (panelGroup != null)
        {
            panelGroup.gameObject.SetActive(true);
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
        }

        if (openSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(openSound);
        }

        RefreshMoneyDisplay();
        RefreshUpgradeUI();

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(1f, true));

        Debug.Log("[MechanicWorkshop] Vista de taller mecánico abierta.");
    }

    private void OnMoneyChanged(int newTotal)
    {
        RefreshMoneyDisplay();
        RefreshUpgradeUI(); // Actualizar estados de botones al cambiar el dinero
    }

    /// <summary>
    /// Actualiza el texto con el dinero real actual del jugador.
    /// </summary>
    public void RefreshMoneyDisplay()
    {
        if (workshopMoneyText != null)
        {
            int currentMoney = FoodDeliveryManager.Instance != null ? FoodDeliveryManager.Instance.TotalMoneyEarned : 0;
            workshopMoneyText.text = $"$ {currentMoney:N0}";
        }
    }

    /// <summary>
    /// Refresca todos los textos y estados de botones de las 3 categorías de mejoras.
    /// </summary>
    /// <summary>
    /// Refresca todos los textos, estrellas y estados de botones de las 3 categorías de mejoras.
    /// </summary>
    public void RefreshUpgradeUI()
    {
        RefreshCategoryUI(UpgradeType.Wheels,           wheelsLevelText, wheelsPriceText, wheelsDescText, wheelsBuyButton,  wheelsStarBar);
        RefreshCategoryUI(UpgradeType.Engine,           engineLevelText, enginePriceText, engineDescText, engineBuyButton,  engineStarBar);
        RefreshCategoryUI(UpgradeType.ThermalInsulation, thermalLevelText, thermalPriceText, thermalDescText, thermalBuyButton, thermalStarBar);
    }

    private void RefreshCategoryUI(UpgradeType type,
                                   TMP_Text levelText, TMP_Text priceText,
                                   TMP_Text descText,  Button buyButton,
                                   Image starBar)
    {
        if (VehicleUpgradeManager.Instance == null) return;

        int currentLevel = VehicleUpgradeManager.Instance.GetLevel(type);
        bool isMax       = VehicleUpgradeManager.Instance.IsMaxLevel(type);
        var  currentData = VehicleUpgradeManager.Instance.GetCurrentLevelData(type);
        var  nextData    = VehicleUpgradeManager.Instance.GetNextLevelData(type);

        // Actualizar barra de estrellas visual (0★ a 3★)
        if (starBar != null && starBarSprites != null && starBarSprites.Length > 0)
        {
            int spriteIndex = Mathf.Clamp(currentLevel, 0, starBarSprites.Length - 1);
            if (starBarSprites[spriteIndex] != null)
            {
                starBar.sprite = starBarSprites[spriteIndex];
                starBar.enabled = true;
            }
        }

        // Texto del nivel actual con estrellas (si existe)
        if (levelText != null)
            levelText.text = currentData != null ? currentData.levelName : $"Nivel {currentLevel}";

        if (isMax)
        {
            // Nivel máximo alcanzado
            if (priceText != null) priceText.text = "MÁXIMO";
            if (descText  != null) descText.text  = currentData?.description ?? "";
            if (buyButton != null)
            {
                buyButton.interactable = false;
                var btnText = buyButton.GetComponentInChildren<TMP_Text>();
                if (btnText != null) btnText.text = "MAX";
            }
        }
        else
        {
            // Mostrar info del siguiente nivel
            int price = VehicleUpgradeManager.Instance.GetNextUpgradePrice(type);
            bool canAfford = VehicleUpgradeManager.Instance.CanAffordNextUpgrade(type);

            if (priceText != null) priceText.text = $"$ {price:N0}";
            if (descText  != null) descText.text  = nextData?.description ?? "";
            if (buyButton != null)
            {
                buyButton.interactable = disableButtonWhenNoFunds ? canAfford : true;
                var btnText = buyButton.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                    btnText.text = canAfford ? "MEJORAR" : "SIN FONDOS";
            }
        }
    }

    /// <summary>
    /// Intenta comprar la siguiente mejora de la categoría indicada.
    /// </summary>
    private void TryPurchase(UpgradeType type)
    {
        if (VehicleUpgradeManager.Instance == null) return;

        bool success = VehicleUpgradeManager.Instance.PurchaseUpgrade(type);

        if (success)
        {
            if (purchaseSuccessSound != null && audioSource != null)
                audioSource.PlayOneShot(purchaseSuccessSound);
            RefreshMoneyDisplay();
            RefreshUpgradeUI();
        }
        else
        {
            if (purchaseFailSound != null && audioSource != null)
                audioSource.PlayOneShot(purchaseFailSound);

            // Flash de advertencia en el dinero si no alcanzaba la plata
            if (!VehicleUpgradeManager.Instance.IsMaxLevel(type) && !VehicleUpgradeManager.Instance.CanAffordNextUpgrade(type))
            {
                StopCoroutine(nameof(FlashMoneyNoFundsRoutine));
                StartCoroutine(FlashMoneyNoFundsRoutine());
            }
        }
    }

    private IEnumerator FlashMoneyNoFundsRoutine()
    {
        if (workshopMoneyText == null) yield break;
        Color originalColor = workshopMoneyText.color;
        workshopMoneyText.color = new Color(1f, 0.25f, 0.25f, 1f); // Rojo alerta
        yield return new WaitForSecondsRealtime(0.35f);
        if (workshopMoneyText != null)
            workshopMoneyText.color = originalColor;
    }

    /// <summary>
    /// Cierra la vista del taller mecánico y devuelve el control al jugador en la van.
    /// </summary>
    public void CloseWorkshop()
    {
        if (!isOpen) return;
        isOpen = false;

        // 1. Restaurar volumen de la radio si estaba atenuado
        if (currentRadioPlayer != null)
        {
            currentRadioPlayer.SetVolume(preWorkshopRadioVolume);
            currentRadioPlayer = null;
        }

        // 2. Reactivar el vehículo
        if (currentVehicle != null)
        {
            currentVehicle.enabled = true;
            currentVehicle = null;
        }

        // 3. Bloquear de nuevo el cursor para conducir
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (panelGroup != null)
        {
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        if (closeSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(closeSound);
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(0f, false));

        Debug.Log("[MechanicWorkshop] Vista de taller cerrada. Conducción reanudada.");
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool isOpening)
    {
        if (panelGroup == null) yield break;

        float startAlpha = panelGroup.alpha;
        float elapsed = 0f;
        float dur = fadeDuration > 0.01f ? fadeDuration : 0.2f;

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / dur);
            yield return null;
        }

        panelGroup.alpha = targetAlpha;
        panelGroup.interactable = isOpening;
        panelGroup.blocksRaycasts = isOpening;

        if (!isOpening)
        {
            panelGroup.gameObject.SetActive(false);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (starBarSprites == null || starBarSprites.Length != 4 || starBarSprites[0] == null)
        {
            AutoLoadStarSprites();
        }
    }

    [ContextMenu("Auto-cargar Sprites de Estrellas")]
    public void AutoLoadStarSprites()
    {
        var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/UI/Mecanico/StarsBarSpriteSheet.png");
        var list = new System.Collections.Generic.List<Sprite>();
        foreach (var obj in allAssets)
        {
            if (obj is Sprite s)
                list.Add(s);
        }
        list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        if (list.Count >= 4)
        {
            starBarSprites = list.GetRange(0, 4).ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[MechanicWorkshopUI] ✅ Sprites de barra de estrellas cargados exitosamente (4 frames).");
        }
    }
#endif
}

