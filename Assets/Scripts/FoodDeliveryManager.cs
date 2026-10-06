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

    [Tooltip("Recompensa en dinero o puntos por entrega completada.")]
    public int rewardPerDelivery = 50;

    [Header("Input Setup")]
    [Tooltip("Acción de interacción para entregar (tecla E). Si está vacía, usa teclado directo E como fallback.")]
    public InputActionReference interactAction;

    [Header("Eventos de Ciclo")]
    public UnityEvent<DeliveryPoint> OnOrderStarted;
    public UnityEvent<DeliveryPoint, PickableItem> OnDeliverySuccess;
    public UnityEvent<int> OnScoreOrMoneyChanged;

    // Estado público
    public DeliveryPoint CurrentDestination { get; private set; }
    public DeliveryState CurrentState { get; private set; } = DeliveryState.Idle;
    public int TotalDeliveriesCompleted { get; private set; } = 0;
    public int TotalMoneyEarned { get; private set; } = 0;

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

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
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

        Debug.Log($"[FoodDeliveryManager] 📦 ¡Nuevo pedido! Entregar a: {CurrentDestination.houseName}");
        OnOrderStarted?.Invoke(CurrentDestination);

        return true;
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
        CurrentState = DeliveryState.Completed;
        TotalDeliveriesCompleted++;
        TotalMoneyEarned += rewardPerDelivery;

        Debug.Log($"[FoodDeliveryManager] 🎉 ¡Entrega completada con éxito en {CurrentDestination.houseName}! Ganancia: +${rewardPerDelivery}. Total: ${TotalMoneyEarned}");

        OnDeliverySuccess?.Invoke(CurrentDestination, deliveredItem);
        OnScoreOrMoneyChanged?.Invoke(TotalMoneyEarned);

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
}
