using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gestor central de la Jornada Laboral (Shift) en A Toda Olla.
/// Controla la progresión día por día (Día 1, 2, 3...), el reloj global de turno,
/// la cuota de entregas requerida y la mecánica de retorno a la cocina para liquidar la jornada.
/// </summary>
public class ShiftManager : MonoBehaviour
{
    public static ShiftManager Instance { get; private set; }

    public enum ShiftState
    {
        NotStarted,
        InProgress,        // Realizando entregas para cumplir la cuota
        ReturningToBase,   // ¡Cuota cumplida! Regresando a la cocina/base con la furgoneta
        DayCompleted,      // Jornada finalizada con éxito en la base (Victoria)
        DayFailed          // Tiempo agotado sin cumplir la cuota (Derrota)
    }

    [System.Serializable]
    public class DayConfig
    {
        public string dayTitle = "Día 1: Primeros Encargos";
        [Tooltip("Entregas exitosas necesarias para cumplir la cuota de hoy.")]
        public int quotaDeliveries = 3;
        [Tooltip("Duración total de la jornada en segundos (ej: 240s = 4 minutos).")]
        public float shiftDurationSeconds = 240f;
        [Tooltip("Recompensa base por cada entrega exitosa.")]
        public int baseReward = 50;
        [Tooltip("Bono de propina al entregar mientras la comida esté hirviendo.")]
        public int speedBonus = 25;
        [Tooltip("Penalización por comida fría / pedido expirado.")]
        public int wastedFoodPenalty = 20;
        [TextArea(2, 3)]
        public string description = "Entrega 3 ollas calientes y regresa a la cocina con la van antes de que termine el turno.";
    }

    [System.Serializable]
    public class ShiftSummaryData
    {
        public int dayIndex;
        public string dayTitle;
        public bool isVictory;
        public int deliveriesCompleted;
        public int quotaTarget;
        public int deliveriesBoiling;
        public int deliveriesHot;
        public int deliveriesWarm;
        public int ordersExpired;
        public int totalEarnings;
        public int totalTips;
        public int totalPenalties;
        public int netProfit;
        public float timeRemaining;
        public int starRating; // 1 a 3 estrellas
        public string summaryMessage;
    }

    [Header("Configuración de Progresión")]
    [Tooltip("Día actual (inicia en 1).")]
    public int currentDayIndex = 1;

    [Tooltip("¿Iniciar automáticamente la jornada al arrancar la escena?")]
    public bool autoStartShiftOnPlay = true;

    [Tooltip("Lista de configuraciones por día. Si se superan, se escala la dificultad dinámicamente.")]
    public List<DayConfig> dayConfigs = new List<DayConfig>();

    [Header("Base / Cocina (Retorno)")]
    [Tooltip("Punto de retorno a la cocina. Si está vacío, se guardará automáticamente la posición inicial.")]
    public Transform kitchenBaseTransform;

    [Tooltip("Radio de proximidad para considerar que el jugador/van llegó a la cocina (en metros).")]
    public float returnBaseRadius = 8.5f;

    [Header("Audio")]
    [Tooltip("Sonido opcional cuando se alcanza la cuota y toca regresar.")]
    public AudioClip quotaReachedSound;
    [Tooltip("Sonido opcional al completar la jornada con éxito.")]
    public AudioClip victorySound;
    [Tooltip("Sonido opcional de derrota.")]
    public AudioClip defeatSound;

    [Header("Eventos de Ciclo")]
    public UnityEvent<int, DayConfig> OnShiftStarted = new UnityEvent<int, DayConfig>();
    public UnityEvent<float, float> OnShiftTimerTick = new UnityEvent<float, float>(); // (segundosRestantes, normalizado01)
    public UnityEvent OnQuotaReached = new UnityEvent();                 // Disparado al cumplir la cuota (pasar a ReturningToBase)
    public UnityEvent<ShiftSummaryData> OnDayCompleted = new UnityEvent<ShiftSummaryData>(); // Victoria
    public UnityEvent<string> OnDayFailed = new UnityEvent<string>();             // Derrota

    // Estado público
    public ShiftState CurrentState { get; private set; } = ShiftState.NotStarted;
    public float ShiftTimeRemaining { get; private set; } = 0f;
    public float ShiftDuration { get; private set; } = 0f;
    public int DeliveriesCompletedThisDay { get; private set; } = 0;
    public int OrdersExpiredThisDay { get; private set; } = 0;
    public int TotalEarningsThisDay { get; private set; } = 0;
    public int TotalTipsThisDay { get; private set; } = 0;
    public int TotalPenaltiesThisDay { get; private set; } = 0;
    public DayConfig CurrentConfig { get; private set; }

    public bool HasMetQuota => CurrentConfig != null && DeliveriesCompletedThisDay >= CurrentConfig.quotaDeliveries;
    public float NormalizedShiftTime => ShiftDuration > 0.001f ? Mathf.Clamp01(ShiftTimeRemaining / ShiftDuration) : 0f;
    public Vector3 KitchenBasePosition => kitchenBaseTransform != null ? kitchenBaseTransform.position : fallbackBasePosition;

    // Estado interno
    private Vector3 fallbackBasePosition = new Vector3(-222.15f, 0.56f, 95.01f);
    private bool isShiftTimerActive = false;
    private int boilingCount = 0;
    private int hotCount = 0;
    private int warmCount = 0;
    private GameObject baseReturnMarkerVisual = null;
    private AudioSource audioSource;

    private Transform cachedPlayerTransform;
    private VehicleController cachedVehicle;

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

        // Asegurar que los eventos de ciclo nunca sean nulos
        if (OnShiftStarted == null) OnShiftStarted = new UnityEvent<int, DayConfig>();
        if (OnShiftTimerTick == null) OnShiftTimerTick = new UnityEvent<float, float>();
        if (OnQuotaReached == null) OnQuotaReached = new UnityEvent();
        if (OnDayCompleted == null) OnDayCompleted = new UnityEvent<ShiftSummaryData>();
        if (OnDayFailed == null) OnDayFailed = new UnityEvent<string>();

        // Cargar progreso del día guardado
        currentDayIndex = PlayerPrefs.GetInt("ATodaOlla_CurrentDay", 1);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        InitializeDefaultDayConfigs();
    }

    void Start()
    {
        CacheStartingBasePosition();

        // Asegurar que el ShiftHUD esté presente en la escena para mostrar el turno y la cuota
        if (UnityEngine.Object.FindAnyObjectByType<ShiftHUD>() == null)
        {
            gameObject.AddComponent<ShiftHUD>();
        }

        // Asegurar que el modal de resultados ShiftResultsUI esté presente en la escena
        if (UnityEngine.Object.FindAnyObjectByType<ShiftResultsUI>() == null)
        {
            gameObject.AddComponent<ShiftResultsUI>();
        }

        if (autoStartShiftOnPlay)
        {
            StartShift(currentDayIndex);
        }
    }

    void Update()
    {
        UpdateShiftClock();
        CheckReturnToBaseZone();
    }

    private void InitializeDefaultDayConfigs()
    {
        if (dayConfigs != null && dayConfigs.Count > 0) return;

        dayConfigs = new List<DayConfig>
        {
            new DayConfig
            {
                dayTitle = "Día 1: Primeros Pedidos",
                quotaDeliveries = 3,
                shiftDurationSeconds = 240f, // 4 minutos
                baseReward = 50,
                speedBonus = 25,
                wastedFoodPenalty = 20,
                description = "Entrega 3 ollas de comida caliente y regresa a la cocina antes de que acabe el tiempo."
            },
            new DayConfig
            {
                dayTitle = "Día 2: La Clientela Crece",
                quotaDeliveries = 4,
                shiftDurationSeconds = 270f, // 4.5 minutos
                baseReward = 55,
                speedBonus = 30,
                wastedFoodPenalty = 25,
                description = "4 entregas requeridas. ¡Conduce con agilidad para conseguir propinas extra!"
            },
            new DayConfig
            {
                dayTitle = "Día 3: Hora Pico en la Ciudad",
                quotaDeliveries = 5,
                shiftDurationSeconds = 300f, // 5 minutos
                baseReward = 60,
                speedBonus = 35,
                wastedFoodPenalty = 30,
                description = "5 entregas exigentes. Mantén la comida caliente para ganar la jornada."
            }
        };
    }

    private void CacheStartingBasePosition()
    {
        if (kitchenBaseTransform != null)
        {
            fallbackBasePosition = kitchenBaseTransform.position;
            return;
        }

        // Si no está asignado, intentar encontrar el punto de la Van o del Player
        VehicleController vehicle = UnityEngine.Object.FindAnyObjectByType<VehicleController>();
        if (vehicle != null)
        {
            fallbackBasePosition = vehicle.transform.position;
            return;
        }

        PlayerPickup player = UnityEngine.Object.FindAnyObjectByType<PlayerPickup>();
        if (player != null)
        {
            fallbackBasePosition = player.transform.position;
        }
    }

    public DayConfig GetConfigForDay(int day)
    {
        if (dayConfigs == null || dayConfigs.Count == 0) InitializeDefaultDayConfigs();

        int zeroIndexed = Mathf.Max(0, day - 1);
        if (zeroIndexed < dayConfigs.Count)
        {
            return dayConfigs[zeroIndexed];
        }

        // Generación dinámica proporcional para días avanzados
        int extraDays = zeroIndexed - dayConfigs.Count + 1;
        DayConfig last = dayConfigs[dayConfigs.Count - 1];
        return new DayConfig
        {
            dayTitle = $"Día {day}: Jornada Extrema",
            quotaDeliveries = last.quotaDeliveries + extraDays,
            shiftDurationSeconds = last.shiftDurationSeconds + (extraDays * 30f),
            baseReward = last.baseReward + (extraDays * 5),
            speedBonus = last.speedBonus + (extraDays * 5),
            wastedFoodPenalty = last.wastedFoodPenalty + (extraDays * 5),
            description = $"¡Completa {last.quotaDeliveries + extraDays} entregas en tiempo récord y regresa a la base!"
        };
    }

    /// <summary>
    /// Inicia un nuevo día de trabajo.
    /// </summary>
    public void StartShift(int dayNumber)
    {
        currentDayIndex = Mathf.Max(1, dayNumber);
        CurrentConfig = GetConfigForDay(currentDayIndex);

        ShiftDuration = CurrentConfig.shiftDurationSeconds;
        ShiftTimeRemaining = ShiftDuration;
        DeliveriesCompletedThisDay = 0;
        OrdersExpiredThisDay = 0;
        TotalEarningsThisDay = 0;
        TotalTipsThisDay = 0;
        TotalPenaltiesThisDay = 0;
        boilingCount = 0;
        hotCount = 0;
        warmCount = 0;

        CurrentState = ShiftState.InProgress;
        isShiftTimerActive = true;

        SetBaseReturnMarkerActive(false);

        // Sincronizar recompensas en FoodDeliveryManager si está presente
        if (FoodDeliveryManager.Instance != null)
        {
            FoodDeliveryManager.Instance.rewardPerDelivery = CurrentConfig.baseReward;
            FoodDeliveryManager.Instance.speedBonusReward = CurrentConfig.speedBonus;
            FoodDeliveryManager.Instance.wastedFoodPenalty = CurrentConfig.wastedFoodPenalty;

            // Iniciar primer pedido si no hay uno activo
            if (FoodDeliveryManager.Instance.CurrentDestination == null)
            {
                FoodDeliveryManager.Instance.StartNewDeliveryOrder();
            }
        }

        Debug.Log($"[ShiftManager] 🚀 ¡Iniciando {CurrentConfig.dayTitle}! Cuota: {CurrentConfig.quotaDeliveries} entregas. Tiempo: {ShiftDuration:F0}s");
        OnShiftStarted?.Invoke(currentDayIndex, CurrentConfig);
        OnShiftTimerTick?.Invoke(ShiftTimeRemaining, 1f);
    }

    private void UpdateShiftClock()
    {
        if (!isShiftTimerActive || CurrentState == ShiftState.NotStarted || CurrentState == ShiftState.DayCompleted || CurrentState == ShiftState.DayFailed)
            return;

        ShiftTimeRemaining -= Time.deltaTime;

        if (ShiftTimeRemaining <= 0f)
        {
            ShiftTimeRemaining = 0f;
            isShiftTimerActive = false;
            HandleShiftTimeExpired();
        }
        else
        {
            OnShiftTimerTick?.Invoke(ShiftTimeRemaining, NormalizedShiftTime);
        }
    }

    private void HandleShiftTimeExpired()
    {
        Debug.LogWarning("[ShiftManager] ⏰ ¡Tiempo agotado de la jornada!");

        if (CurrentState == ShiftState.ReturningToBase)
        {
            // Cumplió la cuota pero no alcanzó a estacionar la van en la cocina a tiempo
            FailShift("Se agotó el tiempo antes de regresar la furgoneta a la cocina para liquidar el turno.");
        }
        else
        {
            // No cumplió la cuota de entregas
            FailShift($"No se alcanzó la cuota de entregas del día ({DeliveriesCompletedThisDay}/{CurrentConfig.quotaDeliveries}).");
        }
    }

    /// <summary>
    /// Llamado por FoodDeliveryManager cuando una entrega es exitosa.
    /// </summary>
    public void RecordDelivery(FoodDeliveryManager.DeliveryQuality quality, int totalEarned, int tipEarned)
    {
        if (CurrentState != ShiftState.InProgress) return;

        DeliveriesCompletedThisDay++;
        TotalEarningsThisDay += totalEarned;
        TotalTipsThisDay += tipEarned;

        switch (quality)
        {
            case FoodDeliveryManager.DeliveryQuality.Boiling: boilingCount++; break;
            case FoodDeliveryManager.DeliveryQuality.Hot: hotCount++; break;
            case FoodDeliveryManager.DeliveryQuality.Warm: warmCount++; break;
        }

        Debug.Log($"[ShiftManager] 📦 Progreso de cuota: {DeliveriesCompletedThisDay} / {CurrentConfig.quotaDeliveries}");

        if (HasMetQuota)
        {
            TransitionToReturningToBase();
        }
    }

    /// <summary>
    /// Llamado por FoodDeliveryManager cuando un pedido se enfría y expira.
    /// </summary>
    public void RecordExpiredOrder(int penaltyDeducted)
    {
        OrdersExpiredThisDay++;
        TotalPenaltiesThisDay += penaltyDeducted;
        Debug.LogWarning($"[ShiftManager] ❄️ Pedido expirado registrado hoy ({OrdersExpiredThisDay} en total). Multa: ${penaltyDeducted}");
    }

    /// <summary>
    /// Determina si FoodDeliveryManager puede asignar un nuevo pedido.
    /// Si la cuota ya se cumplió o el turno finalizó, rechaza nuevos pedidos.
    /// </summary>
    public bool CanAcceptNewOrders()
    {
        return CurrentState == ShiftState.InProgress && !HasMetQuota;
    }

    private void TransitionToReturningToBase()
    {
        CurrentState = ShiftState.ReturningToBase;
        Debug.Log("[ShiftManager] 🎉 ¡CUOTA CUMPLIDA! Misión actualizada: Regresa a la cocina/base para liquidar el turno.");

        if (quotaReachedSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(quotaReachedSound, 0.9f);
        }

        SetBaseReturnMarkerActive(true);
        OnQuotaReached?.Invoke();
    }

    private void CheckReturnToBaseZone()
    {
        if (CurrentState != ShiftState.ReturningToBase) return;

        Vector3 basePos = KitchenBasePosition;
        Vector3 targetPos = GetCurrentPlayerOrVehiclePosition();

        float dist = Vector3.Distance(new Vector3(targetPos.x, basePos.y, targetPos.z), basePos);

        if (dist <= returnBaseRadius)
        {
            CompleteDaySuccess();
        }
    }

    private Vector3 GetCurrentPlayerOrVehiclePosition()
    {
        if (cachedVehicle == null)
            cachedVehicle = UnityEngine.Object.FindAnyObjectByType<VehicleController>();

        if (cachedVehicle != null && cachedVehicle.gameObject.activeInHierarchy)
            return cachedVehicle.transform.position;

        if (cachedPlayerTransform == null)
        {
            PlayerPickup player = UnityEngine.Object.FindAnyObjectByType<PlayerPickup>();
            if (player != null) cachedPlayerTransform = player.transform;
            else if (Camera.main != null) cachedPlayerTransform = Camera.main.transform;
        }

        return cachedPlayerTransform != null ? cachedPlayerTransform.position : Vector3.zero;
    }

    private void CompleteDaySuccess()
    {
        CurrentState = ShiftState.DayCompleted;
        isShiftTimerActive = false;
        SetBaseReturnMarkerActive(false);

        if (victorySound != null && audioSource != null)
        {
            audioSource.PlayOneShot(victorySound, 1f);
        }

        int stars = CalculateStarRating();
        ShiftSummaryData summary = new ShiftSummaryData
        {
            dayIndex = currentDayIndex,
            dayTitle = CurrentConfig.dayTitle,
            isVictory = true,
            deliveriesCompleted = DeliveriesCompletedThisDay,
            quotaTarget = CurrentConfig.quotaDeliveries,
            deliveriesBoiling = boilingCount,
            deliveriesHot = hotCount,
            deliveriesWarm = warmCount,
            ordersExpired = OrdersExpiredThisDay,
            totalEarnings = TotalEarningsThisDay,
            totalTips = TotalTipsThisDay,
            totalPenalties = TotalPenaltiesThisDay,
            netProfit = TotalEarningsThisDay - TotalPenaltiesThisDay,
            timeRemaining = ShiftTimeRemaining,
            starRating = stars,
            summaryMessage = GetVictoryMessage(stars)
        };

        Debug.Log($"[ShiftManager] 🏆 ¡JORNADA COMPLETADA CON ÉXITO! Estrellas: {stars}/3. Ganancias netas: ${summary.netProfit}");
        OnDayCompleted?.Invoke(summary);
    }

    private void FailShift(string reason)
    {
        CurrentState = ShiftState.DayFailed;
        isShiftTimerActive = false;
        SetBaseReturnMarkerActive(false);

        if (defeatSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(defeatSound, 1f);
        }

        Debug.LogWarning($"[ShiftManager] ❌ JORNADA FALLIDA: {reason}");
        OnDayFailed?.Invoke(reason);
    }

    private int CalculateStarRating()
    {
        if (OrdersExpiredThisDay == 0 && boilingCount >= Mathf.CeilToInt(CurrentConfig.quotaDeliveries * 0.5f))
        {
            return 3; // Excelente: comida hirviendo y cero quejas
        }
        else if (OrdersExpiredThisDay <= 1)
        {
            return 2; // Bueno: cuota cumplida con margen
        }
        else
        {
            return 1; // Aceptable: cumplió pero con retrasos y comida fría
        }
    }

    private string GetVictoryMessage(int stars)
    {
        switch (stars)
        {
            case 3: return "¡Servicio impecable! Todos los vecinos recibieron la comida hirviendo.";
            case 2: return "¡Buen trabajo! La clientela quedó satisfecha y el negocio crece.";
            default: return "Jornada cumplida, aunque algunos pedidos se retrasaron. ¡Mañana lo harás mejor!";
        }
    }

    /// <summary>
    /// Avanza al siguiente día y lo inicia recargando el turno limpiamente.
    /// </summary>
    public void AdvanceToNextDay()
    {
        int nextDay = currentDayIndex + 1;
        PlayerPrefs.SetInt("ATodaOlla_CurrentDay", nextDay);
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Reinicia el día actual tras una derrota o por decisión del jugador.
    /// </summary>
    public void RestartCurrentDay()
    {
        PlayerPrefs.SetInt("ATodaOlla_CurrentDay", currentDayIndex);
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    public void PauseShift(bool pause)
    {
        isShiftTimerActive = !pause;
    }

    /// <summary>
    /// Activa o desactiva la baliza visual en la base que guía al jugador cuando la cuota está cumplida.
    /// </summary>
    private void SetBaseReturnMarkerActive(bool active)
    {
        if (active)
        {
            if (baseReturnMarkerVisual == null)
            {
                CreateProceduralBaseMarker();
            }
            if (baseReturnMarkerVisual != null)
            {
                baseReturnMarkerVisual.transform.position = KitchenBasePosition;
                baseReturnMarkerVisual.SetActive(true);
            }
        }
        else
        {
            if (baseReturnMarkerVisual != null)
            {
                baseReturnMarkerVisual.SetActive(false);
            }
        }
    }

    private void CreateProceduralBaseMarker()
    {
        baseReturnMarkerVisual = new GameObject("KitchenBaseReturnMarker");
        baseReturnMarkerVisual.transform.position = KitchenBasePosition;

        // Cilindro translúcido tipo pilar de luz dorado
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = "LightColumn";
        cylinder.transform.SetParent(baseReturnMarkerVisual.transform, false);
        cylinder.transform.localScale = new Vector3(returnBaseRadius * 1.5f, 18f, returnBaseRadius * 1.5f);
        cylinder.transform.localPosition = new Vector3(0f, 9f, 0f);

        Collider col = cylinder.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = cylinder.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            if (sh != null)
            {
                Material mat = new Material(sh);
                Color goldColor = new Color(1f, 0.85f, 0.2f, 0.45f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", goldColor);
                else if (mat.HasProperty("_Color")) mat.SetColor("_Color", goldColor);
                rend.material = mat;
            }
        }

        // Luz puntual dorada en el suelo
        GameObject lightGO = new GameObject("BaseMarkerLight");
        lightGO.transform.SetParent(baseReturnMarkerVisual.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 2f, 0f);
        Light l = lightGO.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.85f, 0.2f);
        l.range = returnBaseRadius * 2f;
        l.intensity = 3.5f;
    }
}
