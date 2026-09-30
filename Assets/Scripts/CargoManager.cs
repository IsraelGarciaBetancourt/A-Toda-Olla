using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(VehicleController))]
public class CargoManager : MonoBehaviour
{
    [Header("Cargo Capacity")]
    [Tooltip("Capacidad máxima de carga en kilogramos (ej. 300kg).")]
    public float maxCargoWeight = 300f;

    [Header("Weight Penalties")]
    [Tooltip("Porcentaje máximo de reducción de velocidad punta al 100% de carga (0.35 = -35% de velocidad).")]
    [Range(0f, 0.8f)]
    public float maxSpeedPenaltyPercent = 0.35f;

    [Tooltip("Porcentaje máximo de reducción de aceleración al 100% de carga (0.40 = -40% de aceleración).")]
    [Range(0f, 0.8f)]
    public float accelerationPenaltyPercent = 0.40f;

    [Tooltip("Porcentaje de reducción de potencia de frenado con carga completa (el vehículo tarda más en frenar).")]
    [Range(0f, 0.6f)]
    public float brakePenaltyPercent = 0.25f;

    [Tooltip("¿Sumar el peso de los ítems a la masa del Rigidbody del vehículo para aumentar la inercia real?")]
    public bool addWeightToVehicleMass = true;

    [Header("Debug / State (Solo Lectura)")]
    [SerializeField] private float currentCargoWeight = 0f;
    [SerializeField] private int loadedItemCount = 0;

    // Propiedades públicas para consultar desde la UI o HUD
    public float CurrentWeight => currentCargoWeight;
    public float MaxWeight => maxCargoWeight;
    public float WeightRatio => maxCargoWeight > 0f ? Mathf.Clamp01(currentCargoWeight / maxCargoWeight) : 0f;
    public float WeightPercentage => WeightRatio * 100f;
    public int LoadedCount => loadedItemCount;
    public bool IsFull => currentCargoWeight >= maxCargoWeight;

    // Evento para el HUD: envía (pesoActual, pesoMaximo)
    public event System.Action<float, float> OnWeightChanged;

    private VehicleController vehicleController;
    private Rigidbody vehicleRb;
    private CargoZone cargoZone;

    private float baseAcceleration;
    private float baseMaxSpeed;
    private float baseBrakePower;
    private float baseVehicleMass;

    private List<PickableItem> loadedItems = new List<PickableItem>();

    void Awake()
    {
        vehicleController = GetComponent<VehicleController>();
        vehicleRb = GetComponent<Rigidbody>();

        if (vehicleController != null)
        {
            baseAcceleration = vehicleController.acceleration;
            baseMaxSpeed = vehicleController.maxSpeed;
            baseBrakePower = vehicleController.brakePower;
        }

        if (vehicleRb != null)
        {
            baseVehicleMass = vehicleRb.mass;
        }

        cargoZone = GetComponentInChildren<CargoZone>();
        if (cargoZone == null && transform.parent != null)
        {
            cargoZone = transform.parent.GetComponentInChildren<CargoZone>();
        }
    }

    void OnEnable()
    {
        if (cargoZone != null)
        {
            cargoZone.OnItemLoadedIntoCargo += RegisterItem;
            cargoZone.OnItemUnloadedFromCargo += UnregisterItem;
        }
    }

    void OnDisable()
    {
        if (cargoZone != null)
        {
            cargoZone.OnItemLoadedIntoCargo -= RegisterItem;
            cargoZone.OnItemUnloadedFromCargo -= UnregisterItem;
        }
    }

    /// <summary>
    /// Registra un nuevo ítem cargado en la van y recalcula las penalizaciones de peso.
    /// </summary>
    public void RegisterItem(PickableItem item)
    {
        if (item == null || loadedItems.Contains(item)) return;

        loadedItems.Add(item);
        currentCargoWeight += item.weightKg;
        loadedItemCount = loadedItems.Count;

        ApplyWeightPenalties();
        OnWeightChanged?.Invoke(currentCargoWeight, maxCargoWeight);

        Debug.Log($"[CargoManager] Ítem cargado: {item.itemName} (+{item.weightKg}kg). Peso total: {currentCargoWeight}/{maxCargoWeight}kg");
    }

    /// <summary>
    /// Remueve un ítem de la van (para cuando se descarguen productos) y restaura físicas.
    /// </summary>
    public void UnregisterItem(PickableItem item)
    {
        if (item == null || !loadedItems.Contains(item)) return;

        loadedItems.Remove(item);
        currentCargoWeight = Mathf.Max(0f, currentCargoWeight - item.weightKg);
        loadedItemCount = loadedItems.Count;

        ApplyWeightPenalties();
        OnWeightChanged?.Invoke(currentCargoWeight, maxCargoWeight);

        Debug.Log($"[CargoManager] Ítem descargado: {item.itemName} (-{item.weightKg}kg). Peso total: {currentCargoWeight}/{maxCargoWeight}kg");
    }

    /// <summary>
    /// Aplica las penalizaciones dinámicas de peso a la camioneta.
    /// A mayor peso cargado:
    /// - Menor aceleración (le cuesta más arrancar).
    /// - Menor velocidad máxima.
    /// - Mayor distancia de frenado (menor eficacia de freno).
    /// - Mayor inercia si addWeightToVehicleMass está activado.
    /// </summary>
    private void ApplyWeightPenalties()
    {
        if (vehicleController == null) return;

        float ratio = WeightRatio;

        // 1. Penalización de Velocidad Máxima
        float targetMaxSpeed = Mathf.Lerp(baseMaxSpeed, baseMaxSpeed * (1f - maxSpeedPenaltyPercent), ratio);
        vehicleController.maxSpeed = targetMaxSpeed;

        // 2. Penalización de Aceleración (arranque más pesado)
        float targetAcceleration = Mathf.Lerp(baseAcceleration, baseAcceleration * (1f - accelerationPenaltyPercent), ratio);
        vehicleController.acceleration = targetAcceleration;

        // 3. Penalización de Frenado (tarda más en detenerse debido a la carga)
        float targetBrake = Mathf.Lerp(baseBrakePower, baseBrakePower * (1f - brakePenaltyPercent), ratio);
        vehicleController.brakePower = targetBrake;

        // 4. Modificación de masa física del Rigidbody
        if (addWeightToVehicleMass && vehicleRb != null)
        {
            vehicleRb.mass = baseVehicleMass + currentCargoWeight;
        }
    }

    /// <summary>
    /// Reinicia todos los valores base de fábrica si se requiere calibrar en tiempo de ejecución.
    /// </summary>
    public void ResetBaseStats()
    {
        if (vehicleController != null)
        {
            vehicleController.acceleration = baseAcceleration;
            vehicleController.maxSpeed = baseMaxSpeed;
            vehicleController.brakePower = baseBrakePower;
        }

        if (vehicleRb != null)
        {
            vehicleRb.mass = baseVehicleMass;
        }

        currentCargoWeight = 0f;
        loadedItemCount = 0;
        loadedItems.Clear();

        OnWeightChanged?.Invoke(currentCargoWeight, maxCargoWeight);
    }
}
