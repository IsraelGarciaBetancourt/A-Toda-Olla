using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Gestor central de misiones de entrega de comida.
/// Registra las casas con DeliveryPoint en la ciudad, selecciona un destino al azar,
/// rastrea la OllaConComida y coordina el ciclo de juego.
/// </summary>
public class FoodDeliveryManager : MonoBehaviour
{
    public enum DeliveryState
    {
        Idle,
        WaitingForPickup,  // La comida está en el suelo o en la mesa esperando a ser recogida
        InTransit,         // El jugador la lleva consigo o está en la van en camino a la casa
        Completed          // Pedido entregado con éxito
    }

    [Header("Referencias del Pedido")]
    [Tooltip("El ítem de comida a entregar. Si se deja vacío, buscará 'OllaConComida' en la escena automáticamente.")]
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

    // Lista interna de puntos de entrega encontrados
    private List<DeliveryPoint> availableDeliveryPoints = new List<DeliveryPoint>();
    private DeliveryPoint previousDestination = null;
    private PlayerPickup cachedPlayerPickup = null;

    void Start()
    {
        FindPlayerReferences();
        FindFoodItem();
        RegisterAllDeliveryPoints();

        if (autoStartOnPlay)
        {
            StartCoroutine(DelayedStartFirstOrder(1.0f));
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
    }

    private void FindFoodItem()
    {
        if (targetFoodItem == null)
        {
            // 1. Buscar por nombre exacto "OllaConComida"
            GameObject olla = GameObject.Find("OllaConComida");
            if (olla != null)
            {
                targetFoodItem = olla.GetComponent<PickableItem>();
            }

            // 2. Si no se encontró por nombre, buscar cualquier PickableItem en la escena
            if (targetFoodItem == null)
            {
                targetFoodItem = Object.FindAnyObjectByType<PickableItem>();
            }

            if (targetFoodItem != null)
            {
                Debug.Log($"[FoodDeliveryManager] Ítem de comida asignado: {targetFoodItem.itemName} ({targetFoodItem.name})");
            }
            else
            {
                Debug.LogWarning("[FoodDeliveryManager] No se encontró 'OllaConComida' ni ningún PickableItem en la escena.");
            }
        }
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
            // Intentar encontrar GameObject "City"
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

        // Si ya hay un destino activo previo, apagarlo
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

        CurrentState = (targetFoodItem != null && targetFoodItem.IsBeingCarried)
            ? DeliveryState.InTransit
            : DeliveryState.WaitingForPickup;

        Debug.Log($"[FoodDeliveryManager] 📦 ¡Nuevo pedido! Entregar a: {CurrentDestination.houseName}");
        OnOrderStarted?.Invoke(CurrentDestination);

        return true;
    }

    /// <summary>
    /// Monitorea el estado de transporte de la comida.
    /// </summary>
    private void UpdateDeliveryState()
    {
        if (CurrentState == DeliveryState.Completed || CurrentDestination == null) return;

        if (targetFoodItem != null)
        {
            if (targetFoodItem.IsBeingCarried || targetFoodItem.IsStoredInCargo)
            {
                CurrentState = DeliveryState.InTransit;
            }
            else
            {
                CurrentState = DeliveryState.WaitingForPickup;
            }
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
    /// </summary>
    public bool TryDeliverCurrentItem()
    {
        if (CurrentDestination == null || !CurrentDestination.IsPlayerInZone) return false;

        FindPlayerReferences();
        if (cachedPlayerPickup == null) return false;

        PickableItem carriedItem = cachedPlayerPickup.CurrentItem;
        if (carriedItem == null) return false;

        // Validar si es el ítem requerido o cualquier ítem de comida
        if (targetFoodItem == null || carriedItem == targetFoodItem)
        {
            bool success = CurrentDestination.Deliver(carriedItem, cachedPlayerPickup);
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
    /// Útil para la UI de navegación.
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
