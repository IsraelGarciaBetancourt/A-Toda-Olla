using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Gestor central de misiones de entrega de comida.
/// Registra las casas con DeliveryPoint en la ciudad, selecciona un destino al azar,
/// admite cualquier OllaConComida de la escena y coordina el ciclo de juego.
/// </summary>
public class FoodDeliveryManager : MonoBehaviour
{
    public static FoodDeliveryManager Instance { get; private set; }

    public enum DeliveryState
    {
        Idle,
        WaitingForPickup,  // La comida está en el suelo o en la cocina esperando a ser recogida
        InTransit,         // El jugador la lleva consigo o está en la van en camino a la casa
        Completed          // Pedido entregado con éxito
    }

    public enum DeliveryQuality
    {
        Boiling,  // Hirviendo / Entrega rápida (> 50% tiempo restante) -> Bono de propina
        Hot,      // Caliente estándar (15% - 50% tiempo restante) -> Pago normal
        Warm,     // Tibio (< 15% tiempo restante) -> Pago reducido
        Cold      // Frío (0s restante) -> Expirado / Cancelado con penalización
    }

    [Header("Referencias del Pedido")]
    [Tooltip("Ítem de comida opcional predeterminado. Si está vacío, cualquier OllaConComida no entregada de la escena es válida.")]
    public PickableItem targetFoodItem;

    [Tooltip("Transform raíz de la ciudad donde buscar los DeliveryPoint. Si se deja vacío, buscará en toda la escena.")]
    public Transform cityRoot;

    [Header("Configuración del Ciclo")]
    [Tooltip("¿Iniciar un pedido automáticamente al arrancar la partida?")]
    public bool autoStartOnPlay = true;

    [Tooltip("¿Generar automáticamente el siguiente pedido tras completar una entrega?")]
    public bool autoStartNextOrder = true;

    [Tooltip("Segundos de espera antes de asignar el siguiente pedido tras completar uno.")]
    public float delayBetweenOrders = 3.5f;

    [Tooltip("Recompensa en dinero base por entrega completada.")]
    public int rewardPerDelivery = 50;

    [Header("Temporizador de Pedido")]
    [Tooltip("¿Activar cuenta regresiva contra reloj para cada pedido?")]
    public bool enableOrderTimer = true;

    [Tooltip("Tiempo base garantizado para cualquier pedido (en segundos).")]
    public float baseOrderTime = 50f;

    [Tooltip("Segundos adicionales otorgados por cada 100 metros de distancia a la casa.")]
    public float secondsPer100Meters = 18f;

    [Tooltip("Tiempo mínimo absoluto asignado a un pedido.")]
    public float minOrderTime = 35f;

    [Tooltip("Bono extra de propina si la entrega se hace mientras la comida sigue hirviendo (> 50% tiempo).")]
    public int speedBonusReward = 25;

    [Tooltip("Multiplicador del pago si la comida llega tibia (< 15% tiempo restante).")]
    [Range(0.2f, 1f)]
    public float warmPayoutMultiplier = 0.7f;

    [Tooltip("Penalización económica por pedido expirado (ingredientes desperdiciados).")]
    public int wastedFoodPenalty = 20;

    [Header("Audio y Tensión Contrarreloj")]
    [Tooltip("Clip opcional de tick para los últimos 15 segundos (se sintetiza proceduralmente si está vacío).")]
    public AudioClip criticalTickClip;

    [Tooltip("Clip opcional de propina/monedas al entregar en calidad hirviendo (se sintetiza proceduralmente si está vacío).")]
    public AudioClip speedBonusClip;

    [Tooltip("Clip opcional al expirar el pedido y enfriarse la comida (se sintetiza proceduralmente si está vacío).")]
    public AudioClip orderExpiredClip;

    [Header("Input Setup")]
    [Tooltip("Acción de interacción para entregar (tecla E). Si está vacía, usa teclado directo E como fallback.")]
    public InputActionReference interactAction;

    [Header("Eventos de Ciclo")]
    public UnityEvent<DeliveryPoint> OnOrderStarted = new UnityEvent<DeliveryPoint>();
    public UnityEvent<DeliveryPoint, PickableItem> OnDeliverySuccess = new UnityEvent<DeliveryPoint, PickableItem>();
    public UnityEvent<int> OnScoreOrMoneyChanged = new UnityEvent<int>();
    public UnityEvent<float, float> OnOrderTimerTick = new UnityEvent<float, float>();                 // (tiempoRestante, normalizado01)
    public UnityEvent<DeliveryPoint, int> OnOrderExpired = new UnityEvent<DeliveryPoint, int>();             // (destino, multaEconómica)
    public UnityEvent<DeliveryQuality, int, int> OnDeliveryQualityEvaluated = new UnityEvent<DeliveryQuality, int, int>(); // (calidad, pagoTotal, propina)

    // Estado público
    public DeliveryPoint CurrentDestination { get; private set; }
    public DeliveryState CurrentState { get; private set; } = DeliveryState.Idle;
    public int TotalDeliveriesCompleted { get; private set; } = 0;
    public int TotalMoneyEarned { get; private set; } = 0;

    // Estado público del temporizador de pedido
    public float CurrentOrderTimeRemaining { get; private set; } = 0f;
    public float CurrentOrderDuration { get; private set; } = 0f;
    public bool IsOrderTimerActive { get; private set; } = false;
    public float NormalizedOrderTime => CurrentOrderDuration > 0.001f ? Mathf.Clamp01(CurrentOrderTimeRemaining / CurrentOrderDuration) : 0f;

    /// <summary>
    /// Intenta gastar una cantidad de dinero. Devuelve true si la transacción fue exitosa.
    /// </summary>
    public bool SpendMoney(int amount)
    {
        if (amount <= 0) return true;
        if (TotalMoneyEarned < amount) return false;
        TotalMoneyEarned -= amount;
        OnScoreOrMoneyChanged?.Invoke(TotalMoneyEarned);
        return true;
    }

    /// <summary>
    /// Añade dinero a la cuenta del jugador y notifica a la interfaz.
    /// </summary>
    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        TotalMoneyEarned += amount;
        OnScoreOrMoneyChanged?.Invoke(TotalMoneyEarned);
    }

    // Lista interna de puntos de entrega encontrados
    private List<DeliveryPoint> availableDeliveryPoints = new List<DeliveryPoint>();
    private DeliveryPoint previousDestination = null;
    private PlayerPickup cachedPlayerPickup = null;
    private CargoManager cachedCargoManager = null;

    private AudioSource audioSource;
    private float tickTimer = 0f;
    private static AudioClip proceduralTickClip = null;
    private static AudioClip proceduralBonusClip = null;
    private static AudioClip proceduralExpiredClip = null;

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
        if (OnOrderStarted == null) OnOrderStarted = new UnityEvent<DeliveryPoint>();
        if (OnDeliverySuccess == null) OnDeliverySuccess = new UnityEvent<DeliveryPoint, PickableItem>();
        if (OnScoreOrMoneyChanged == null) OnScoreOrMoneyChanged = new UnityEvent<int>();
        if (OnOrderTimerTick == null) OnOrderTimerTick = new UnityEvent<float, float>();
        if (OnOrderExpired == null) OnOrderExpired = new UnityEvent<DeliveryPoint, int>();
        if (OnDeliveryQualityEvaluated == null) OnDeliveryQualityEvaluated = new UnityEvent<DeliveryQuality, int, int>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Auto-asegurar ShiftManager en este GameObject si no existe uno en la escena
        if (ShiftManager.Instance == null && Object.FindAnyObjectByType<ShiftManager>() == null)
        {
            gameObject.AddComponent<ShiftManager>();
        }
    }

    void Start()
    {
        FindPlayerReferences();
        FindFoodItem();
        RegisterAllDeliveryPoints();

        if (autoStartOnPlay)
        {
            StartNewDeliveryOrder();
        }
    }

    void OnEnable()
    {
        if (interactAction != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += OnInteractPerformed;
        }
    }

    void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.action.performed -= OnInteractPerformed;
        }
    }

    void Update()
    {
        UpdateDeliveryState();
        UpdateOrderTimer();
        CheckDirectKeyboardFallback();
    }

    private void FindPlayerReferences()
    {
        if (cachedPlayerPickup == null)
        {
            cachedPlayerPickup = Object.FindAnyObjectByType<PlayerPickup>();
        }

        if (cachedCargoManager == null)
        {
            cachedCargoManager = Object.FindAnyObjectByType<CargoManager>();
        }
    }

    private void FindFoodItem()
    {
        if (targetFoodItem == null)
        {
            // Buscar la olla de comida disponible más cercana
            targetFoodItem = GetNearestAvailableFoodItem(Vector3.zero);

            if (targetFoodItem != null)
            {
                Debug.Log($"[FoodDeliveryManager] Olla de comida inicial asignada: {targetFoodItem.itemName} ({targetFoodItem.name})");
            }
        }
    }

    /// <summary>
    /// Determina si un objeto es una olla de comida válida para entregar.
    /// Acepta cualquier OllaConComida que no haya sido entregada previamente.
    /// </summary>
    public bool IsDeliverableFoodItem(PickableItem item)
    {
        if (item == null || item.IsDelivered) return false;

        // Coincidencia con target manual opcional
        if (targetFoodItem != null && item == targetFoodItem) return true;

        // Propiedad del ítem
        if (item.isDeliverableFood) return true;

        // Por nombre de ítem o de GameObject (OllaConComida, Olla, etc.)
        if (!string.IsNullOrEmpty(item.itemName) && item.itemName.IndexOf("Olla", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (item.name.IndexOf("Olla", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    /// <summary>
    /// Comprueba si el jugador o la van transportan una olla de comida lista para entregarse.
    /// </summary>
    public bool IsAnyFoodInTransit()
    {
        FindPlayerReferences();

        // 1. ¿El jugador lleva una olla en sus manos?
        if (cachedPlayerPickup != null && cachedPlayerPickup.CurrentItem != null)
        {
            if (IsDeliverableFoodItem(cachedPlayerPickup.CurrentItem))
                return true;
        }

        // 2. ¿Hay alguna olla en la van (CargoManager)?
        if (cachedCargoManager != null && cachedCargoManager.LoadedItems != null)
        {
            for (int i = 0; i < cachedCargoManager.LoadedItems.Count; i++)
            {
                PickableItem item = cachedCargoManager.LoadedItems[i];
                if (item != null && IsDeliverableFoodItem(item))
                    return true;
            }
        }
        else
        {
            // Búsqueda de respaldo por estado IsStoredInCargo
            PickableItem[] allItems = Object.FindObjectsByType<PickableItem>();
            for (int i = 0; i < allItems.Length; i++)
            {
                if (allItems[i].IsStoredInCargo && IsDeliverableFoodItem(allItems[i]))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Encuentra la olla de comida no entregada más cercana a una posición dada.
    /// </summary>
    public PickableItem GetNearestAvailableFoodItem(Vector3 fromPosition)
    {
        FindPlayerReferences();

        if (cachedPlayerPickup != null && cachedPlayerPickup.CurrentItem != null && IsDeliverableFoodItem(cachedPlayerPickup.CurrentItem))
        {
            return cachedPlayerPickup.CurrentItem;
        }

        PickableItem[] allItems = Object.FindObjectsByType<PickableItem>();
        PickableItem closest = null;
        float minDistanceSq = float.MaxValue;

        for (int i = 0; i < allItems.Length; i++)
        {
            PickableItem it = allItems[i];
            if (it == null || !it.gameObject.activeInHierarchy || it.IsDelivered) continue;
            if (!IsDeliverableFoodItem(it)) continue;

            float distSq = (it.transform.position - fromPosition).sqrMagnitude;
            if (distSq < minDistanceSq)
            {
                minDistanceSq = distSq;
                closest = it;
            }
        }

        return closest;
    }

    /// <summary>
    /// Escanea la ciudad o la escena para registrar todos los DeliveryPoints en las casas.
    /// </summary>
    public void RegisterAllDeliveryPoints()
    {
        availableDeliveryPoints.Clear();

        DeliveryPoint[] foundPoints;
        if (cityRoot != null)
        {
            foundPoints = cityRoot.GetComponentsInChildren<DeliveryPoint>(true);
        }
        else
        {
            GameObject city = GameObject.Find("City");
            if (city != null)
            {
                cityRoot = city.transform;
                foundPoints = city.GetComponentsInChildren<DeliveryPoint>(true);
            }
            else
            {
                foundPoints = Object.FindObjectsByType<DeliveryPoint>(FindObjectsInactive.Include);
            }
        }

        if (foundPoints != null && foundPoints.Length > 0)
        {
            availableDeliveryPoints.AddRange(foundPoints);
            Debug.Log($"[FoodDeliveryManager] Se registraron {availableDeliveryPoints.Count} puntos de entrega en las casas.");
        }
        else
        {
            Debug.LogWarning("[FoodDeliveryManager] No se encontraron componentes DeliveryPoint en las casas de la ciudad.");
        }
    }

    private IEnumerator DelayedStartFirstOrder(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartNewDeliveryOrder();
    }

    /// <summary>
    /// Selecciona aleatoriamente una casa de la lista y la asigna como destino activo.
    /// </summary>
    public bool StartNewDeliveryOrder()
    {
        // Si ShiftManager está activo y la cuota ya se cumplió o no acepta más pedidos
        if (ShiftManager.Instance != null && !ShiftManager.Instance.CanAcceptNewOrders())
        {
            return false;
        }

        if (availableDeliveryPoints == null || availableDeliveryPoints.Count == 0)
        {
            RegisterAllDeliveryPoints();
            if (availableDeliveryPoints.Count == 0)
            {
                Debug.LogError("[FoodDeliveryManager] No hay casas registradas para asignar un pedido.");
                return false;
            }
        }

        // Si ya hay un destino previo activo, apagarlo
        if (CurrentDestination != null)
        {
            CurrentDestination.SetActiveDestination(false);
            CurrentDestination.OnItemDelivered.RemoveListener(HandleItemDelivered);
        }

        // Elegir una casa al azar distinta a la anterior si hay más de 1
        DeliveryPoint nextDestination = null;
        if (availableDeliveryPoints.Count == 1)
        {
            nextDestination = availableDeliveryPoints[0];
        }
        else
        {
            List<DeliveryPoint> candidates = new List<DeliveryPoint>(availableDeliveryPoints);
            if (previousDestination != null && candidates.Contains(previousDestination))
            {
                candidates.Remove(previousDestination);
            }

            int randomIndex = Random.Range(0, candidates.Count);
            nextDestination = candidates[randomIndex];
        }

        CurrentDestination = nextDestination;
        previousDestination = CurrentDestination;

        // Activar la casa seleccionada
        CurrentDestination.SetActiveDestination(true);
        CurrentDestination.OnItemDelivered.AddListener(HandleItemDelivered);

        CurrentState = IsAnyFoodInTransit()
            ? DeliveryState.InTransit
            : DeliveryState.WaitingForPickup;

        // Iniciar temporizador del pedido si está activado
        if (enableOrderTimer)
        {
            float dist = GetDistanceToDestination();
            if (dist < 10f) dist = 80f;
            CurrentOrderDuration = CalculateOrderDuration(dist);
            CurrentOrderTimeRemaining = CurrentOrderDuration;
            IsOrderTimerActive = true;
            tickTimer = 0f;
            RestoreFoodSteam();
        }
        else
        {
            IsOrderTimerActive = false;
            CurrentOrderTimeRemaining = 0f;
            CurrentOrderDuration = 0f;
        }

        Debug.Log($"[FoodDeliveryManager] 📦 ¡Nuevo pedido! Entregar a: {CurrentDestination.houseName} (Tiempo límite: {CurrentOrderDuration:F0}s)");
        OnOrderStarted?.Invoke(CurrentDestination);

        return true;
    }

    /// <summary>
    /// Calcula el tiempo asignado a un pedido en base a la distancia hasta la casa destino.
    /// </summary>
    public float CalculateOrderDuration(float distanceMeters)
    {
        float calculated = baseOrderTime + (distanceMeters / 100f) * secondsPer100Meters;
        return Mathf.Max(minOrderTime, calculated);
    }

    private void UpdateOrderTimer()
    {
        if (!enableOrderTimer || !IsOrderTimerActive || CurrentDestination == null || CurrentState == DeliveryState.Completed)
            return;

        CurrentOrderTimeRemaining -= Time.deltaTime;

        if (CurrentOrderTimeRemaining <= 0f)
        {
            CurrentOrderTimeRemaining = 0f;
            IsOrderTimerActive = false;
            HandleOrderExpired();
        }
        else
        {
            // Feedback rítmico de tensión en los últimos 15 segundos
            if (CurrentOrderTimeRemaining <= 15f)
            {
                float tickInterval = CurrentOrderTimeRemaining < 5f ? 0.5f : 1.0f;
                tickTimer += Time.deltaTime;
                if (tickTimer >= tickInterval)
                {
                    tickTimer = 0f;
                    PlayCriticalTickSound();
                }
            }
            else
            {
                tickTimer = 0f;
            }

            OnOrderTimerTick?.Invoke(CurrentOrderTimeRemaining, NormalizedOrderTime);
        }
    }

    private void HandleOrderExpired()
    {
        if (CurrentDestination == null) return;

        DeliveryPoint expiredDestination = CurrentDestination;
        Debug.LogWarning($"[FoodDeliveryManager] ❄️ ¡Pedido expirado! La comida para {expiredDestination.houseName} se enfrió. Multa por desperdicio: -${wastedFoodPenalty}");

        // Apagar el vapor de las ollas (comida fría) y sonido de fallo
        ExtinguishFoodSteam();
        PlayOrderExpiredSound();

        // Desactivar la casa activa
        expiredDestination.SetActiveDestination(false);
        expiredDestination.OnItemDelivered.RemoveListener(HandleItemDelivered);
        CurrentDestination = null;
        CurrentState = DeliveryState.Idle;

        // Descontar penalización económica (sin permitir saldo negativo)
        SpendMoney(wastedFoodPenalty);

        OnOrderExpired?.Invoke(expiredDestination, wastedFoodPenalty);

        // Notificar al ShiftManager
        if (ShiftManager.Instance != null)
        {
            ShiftManager.Instance.RecordExpiredOrder(wastedFoodPenalty);
        }

        // Programar siguiente pedido si el turno lo permite
        if (autoStartNextOrder)
        {
            if (ShiftManager.Instance == null || ShiftManager.Instance.CanAcceptNewOrders())
            {
                StartCoroutine(ScheduleNextOrderRoutine());
            }
        }
    }

    /// <summary>
    /// Monitorea el estado de transporte de la comida en tiempo real.
    /// </summary>
    private void UpdateDeliveryState()
    {
        if (CurrentState == DeliveryState.Completed || CurrentDestination == null) return;

        if (IsAnyFoodInTransit())
        {
            CurrentState = DeliveryState.InTransit;
        }
        else
        {
            CurrentState = DeliveryState.WaitingForPickup;
        }
    }

    private void CheckDirectKeyboardFallback()
    {
        if (Keyboard.current == null) return;

        // Fallback tecla E si no está configurada la InputAction
        if (interactAction == null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryDeliverCurrentItem();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        TryDeliverCurrentItem();
    }

    /// <summary>
    /// Intenta entregar el ítem si el jugador está en la zona de entrega de la casa activa.
    /// Admite tanto la olla sostenida en brazos como una olla depositada en el área de la puerta.
    /// </summary>
    public bool TryDeliverCurrentItem()
    {
        if (CurrentDestination == null || !CurrentDestination.IsPlayerInZone) return false;

        FindPlayerReferences();

        // 1. Prioridad: Olla en manos del jugador
        if (cachedPlayerPickup != null && cachedPlayerPickup.CurrentItem != null)
        {
            PickableItem carriedItem = cachedPlayerPickup.CurrentItem;
            if (IsDeliverableFoodItem(carriedItem))
            {
                bool success = CurrentDestination.Deliver(carriedItem, cachedPlayerPickup);
                return success;
            }
        }

        // 2. Si el jugador no la tiene en manos, buscar si soltó una olla dentro de la zona de entrega
        PickableItem groundPot = CurrentDestination.FindFoodItemInDeliveryZone();
        if (groundPot != null && IsDeliverableFoodItem(groundPot))
        {
            bool success = CurrentDestination.Deliver(groundPot, null);
            return success;
        }

        return false;
    }

    private void HandleItemDelivered(PickableItem deliveredItem)
    {
        IsOrderTimerActive = false;
        CurrentState = DeliveryState.Completed;
        TotalDeliveriesCompleted++;

        DeliveryQuality quality = DeliveryQuality.Hot;
        int tipEarned = 0;
        int payout = rewardPerDelivery;

        if (enableOrderTimer && CurrentOrderDuration > 0.01f)
        {
            float ratio = NormalizedOrderTime;
            if (ratio >= 0.5f)
            {
                quality = DeliveryQuality.Boiling;
                tipEarned = speedBonusReward;
                payout = rewardPerDelivery + tipEarned;
            }
            else if (ratio >= 0.15f)
            {
                quality = DeliveryQuality.Hot;
                tipEarned = 0;
                payout = rewardPerDelivery;
            }
            else
            {
                quality = DeliveryQuality.Warm;
                tipEarned = 0;
                payout = Mathf.Max(10, Mathf.RoundToInt(rewardPerDelivery * warmPayoutMultiplier));
            }
        }

        if (quality == DeliveryQuality.Boiling)
        {
            PlaySpeedBonusSound();
        }

        TotalMoneyEarned += payout;

        Debug.Log($"[FoodDeliveryManager] 🎉 ¡Entrega completada con éxito en {CurrentDestination.houseName}! Calidad: {quality}. Ganancia: +${payout} (Propina: +${tipEarned}). Total acumulado: ${TotalMoneyEarned}");

        OnDeliverySuccess?.Invoke(CurrentDestination, deliveredItem);
        OnDeliveryQualityEvaluated?.Invoke(quality, payout, tipEarned);
        OnScoreOrMoneyChanged?.Invoke(TotalMoneyEarned);

        if (ShiftManager.Instance != null)
        {
            ShiftManager.Instance.RecordDelivery(quality, payout, tipEarned);
        }

        // Si la cuota ya se cumplió o no podemos aceptar pedidos nuevos, no iniciar siguiente
        if (ShiftManager.Instance != null && !ShiftManager.Instance.CanAcceptNewOrders())
        {
            return;
        }

        if (autoStartNextOrder)
        {
            StartCoroutine(ScheduleNextOrderRoutine());
        }
    }

    private IEnumerator ScheduleNextOrderRoutine()
    {
        yield return new WaitForSeconds(delayBetweenOrders);
        StartNewDeliveryOrder();
    }

    /// <summary>
    /// Devuelve la distancia en metros entre el jugador/cámara y la casa destino actual.
    /// </summary>
    public float GetDistanceToDestination()
    {
        if (CurrentDestination == null) return -1f;

        Vector3 playerPos = cachedPlayerPickup != null
            ? cachedPlayerPickup.transform.position
            : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);

        return Vector3.Distance(playerPos, CurrentDestination.transform.position);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  GESTIÓN DE VAPOR DE COMIDA (GAME FEEL & ESTADO VISUAL)
    // ──────────────────────────────────────────────────────────────────────

    public void ExtinguishFoodSteam()
    {
        PickableItem[] items = Object.FindObjectsByType<PickableItem>();
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].isDeliverableFood && !items[i].IsDelivered)
            {
                items[i].SetSteamEmission(false);
            }
        }
    }

    public void RestoreFoodSteam()
    {
        PickableItem[] items = Object.FindObjectsByType<PickableItem>();
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].isDeliverableFood && !items[i].IsDelivered)
            {
                items[i].SetSteamEmission(true);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  AUDIO PROCEDURAL Y EFECTOS DE TENSIÓN CONTRARRELOJ
    // ──────────────────────────────────────────────────────────────────────

    public void PlayCriticalTickSound()
    {
        if (audioSource == null) return;
        AudioClip clip = criticalTickClip != null ? criticalTickClip : GetOrCreateProceduralTickClip();
        audioSource.PlayOneShot(clip, 0.75f);
    }

    public void PlaySpeedBonusSound()
    {
        if (audioSource == null) return;
        AudioClip clip = speedBonusClip != null ? speedBonusClip : GetOrCreateProceduralBonusClip();
        audioSource.PlayOneShot(clip, 0.9f);
    }

    public void PlayOrderExpiredSound()
    {
        if (audioSource == null) return;
        AudioClip clip = orderExpiredClip != null ? orderExpiredClip : GetOrCreateProceduralExpiredClip();
        audioSource.PlayOneShot(clip, 0.85f);
    }

    private static AudioClip GetOrCreateProceduralTickClip()
    {
        if (proceduralTickClip != null) return proceduralTickClip;

        int sampleRate = 44100;
        int sampleCount = Mathf.RoundToInt(sampleRate * 0.045f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 90f);
            float wave = Mathf.Sin(2f * Mathf.PI * 1350f * t);
            samples[i] = wave * envelope * 0.45f;
        }

        proceduralTickClip = AudioClip.Create("TickProcedural", sampleCount, 1, sampleRate, false);
        proceduralTickClip.SetData(samples, 0);
        return proceduralTickClip;
    }

    private static AudioClip GetOrCreateProceduralBonusClip()
    {
        if (proceduralBonusClip != null) return proceduralBonusClip;

        int sampleRate = 44100;
        float duration = 0.32f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        float[] freqs = { 784f, 1046.5f, 1318.5f }; // G5, C6, E6
        float noteDur = duration / freqs.Length;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            int idx = Mathf.Clamp(Mathf.FloorToInt(t / noteDur), 0, freqs.Length - 1);
            float noteT = t - (idx * noteDur);
            float env = Mathf.Exp(-noteT * 18f);
            float wave = Mathf.Sin(2f * Mathf.PI * freqs[idx] * t) * 0.6f + Mathf.Sin(4f * Mathf.PI * freqs[idx] * t) * 0.2f;
            samples[i] = wave * env * 0.45f;
        }

        proceduralBonusClip = AudioClip.Create("BonusProcedural", sampleCount, 1, sampleRate, false);
        proceduralBonusClip.SetData(samples, 0);
        return proceduralBonusClip;
    }

    private static AudioClip GetOrCreateProceduralExpiredClip()
    {
        if (proceduralExpiredClip != null) return proceduralExpiredClip;

        int sampleRate = 44100;
        float duration = 0.38f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        float[] freqs = { 311.13f, 246.94f }; // Eb4 a B3 triste
        float noteDur = duration / freqs.Length;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            int idx = Mathf.Clamp(Mathf.FloorToInt(t / noteDur), 0, freqs.Length - 1);
            float noteT = t - (idx * noteDur);
            float env = Mathf.Exp(-noteT * 8f);
            float wave = Mathf.Sin(2f * Mathf.PI * freqs[idx] * t) * 0.7f;
            samples[i] = wave * env * 0.45f;
        }

        proceduralExpiredClip = AudioClip.Create("ExpiredProcedural", sampleCount, 1, sampleRate, false);
        proceduralExpiredClip.SetData(samples, 0);
        return proceduralExpiredClip;
    }
}
