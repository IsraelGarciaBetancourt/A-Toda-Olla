using UnityEngine;

/// <summary>
/// Gestor central de mejoras del vehículo.
/// • Persiste los niveles actuales durante la sesión (en este mismo objeto).
/// • Aplica los bonuses reales al VehicleController cada vez que se compra una mejora.
/// • Expone métodos simples para la UI: CanAfford, PurchaseUpgrade, GetCurrentLevel.
///
/// Colocarlo en la escena junto al VehicleController o en un GameObject raíz de managers.
/// </summary>
public class VehicleUpgradeManager : MonoBehaviour
{
    public static VehicleUpgradeManager Instance { get; private set; }

    [Header("Datos de Mejoras")]
    [Tooltip("ScriptableObject con los 4 niveles de cada categoría. Créalo desde el menú Assets.")]
    public VehicleUpgradeData upgradeData;

    [Header("Referencias")]
    [Tooltip("VehicleController al que se aplican las mejoras. Se busca automáticamente si está vacío.")]
    public VehicleController vehicleController;

    // ── Claves de persistencia ──────────────────────────────────────────
    private const string PREF_KEY_WHEELS  = "UpgradeLevel_Wheels";
    private const string PREF_KEY_ENGINE  = "UpgradeLevel_Engine";
    private const string PREF_KEY_THERMAL = "UpgradeLevel_Thermal";

    // ── Niveles actuales (0 = stock, 3 = full upgrade) ──────────────────
    private int wheelLevel   = 0;
    private int engineLevel  = 0;
    private int thermalLevel = 0;

    // ── Valores BASE del vehículo (guardados al inicio para poder sumar bonuses) ──
    private float baseTurnSpeed    = 0f;
    private float baseAcceleration = 0f;
    private float baseMaxSpeed     = 0f;

    // ── Evento que la UI puede suscribir para refrescarse ────────────────
    public event System.Action OnUpgradeChanged;

    // ────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (vehicleController == null)
            vehicleController = Object.FindAnyObjectByType<VehicleController>();

        if (vehicleController != null)
        {
            // Guardar valores base UNA VEZ al inicio (nivel 0 = sin mejoras)
            baseTurnSpeed    = vehicleController.turnSpeed;
            baseAcceleration = vehicleController.acceleration;
            baseMaxSpeed     = vehicleController.maxSpeed;
        }
        else
        {
            Debug.LogWarning("[VehicleUpgradeManager] No se encontró VehicleController en la escena.");
        }

        // Cargar progreso guardado desde PlayerPrefs
        LoadUpgrades();

        // Aplicar el nivel actual para que los stats reflejen las mejoras guardadas
        ApplyAllUpgrades();
    }

    private void LoadUpgrades()
    {
        wheelLevel   = Mathf.Clamp(PlayerPrefs.GetInt(PREF_KEY_WHEELS, 0), 0, 3);
        engineLevel  = Mathf.Clamp(PlayerPrefs.GetInt(PREF_KEY_ENGINE, 0), 0, 3);
        thermalLevel = Mathf.Clamp(PlayerPrefs.GetInt(PREF_KEY_THERMAL, 0), 0, 3);
    }

    private void SaveUpgrades()
    {
        PlayerPrefs.SetInt(PREF_KEY_WHEELS, wheelLevel);
        PlayerPrefs.SetInt(PREF_KEY_ENGINE, engineLevel);
        PlayerPrefs.SetInt(PREF_KEY_THERMAL, thermalLevel);
        PlayerPrefs.Save();
    }

    [ContextMenu("Reset All Upgrades (Debug)")]
    public void ResetAllUpgrades()
    {
        wheelLevel   = 0;
        engineLevel  = 0;
        thermalLevel = 0;
        SaveUpgrades();
        ApplyAllUpgrades();
        OnUpgradeChanged?.Invoke();
        Debug.Log("[VehicleUpgradeManager] 🔄 Mejoras reiniciadas a nivel 0.");
    }

    // ────────────────────────────────────────────────────────────────────
    //  Consultas públicas
    // ────────────────────────────────────────────────────────────────────

    /// <summary>Nivel actual de la mejora (0‑3).</summary>
    public int GetLevel(UpgradeType type)
    {
        return type switch
        {
            UpgradeType.Wheels           => wheelLevel,
            UpgradeType.Engine           => engineLevel,
            UpgradeType.ThermalInsulation => thermalLevel,
            _                            => 0
        };
    }

    /// <summary>¿Tiene el jugador dinero suficiente para el siguiente nivel?</summary>
    public bool CanAffordNextUpgrade(UpgradeType type)
    {
        int nextLevel = GetLevel(type) + 1;
        if (nextLevel > 3) return false; // Ya está al máximo

        if (upgradeData == null) return false;
        var data = upgradeData.GetLevel(type, nextLevel);
        if (data == null) return false;

        int money = FoodDeliveryManager.Instance != null ? FoodDeliveryManager.Instance.TotalMoneyEarned : 0;
        return money >= data.price;
    }

    /// <summary>¿Está la mejora en su nivel máximo?</summary>
    public bool IsMaxLevel(UpgradeType type) => GetLevel(type) >= 3;

    /// <summary>Precio del siguiente nivel. Devuelve ‑1 si ya es máximo o no hay datos.</summary>
    public int GetNextUpgradePrice(UpgradeType type)
    {
        int nextLevel = GetLevel(type) + 1;
        if (nextLevel > 3 || upgradeData == null) return -1;
        var data = upgradeData.GetLevel(type, nextLevel);
        return data != null ? data.price : -1;
    }

    /// <summary>Datos del nivel actual de una mejora.</summary>
    public UpgradeLevelData GetCurrentLevelData(UpgradeType type)
    {
        if (upgradeData == null) return null;
        return upgradeData.GetLevel(type, GetLevel(type));
    }

    /// <summary>Datos del siguiente nivel de una mejora (null si ya es máximo).</summary>
    public UpgradeLevelData GetNextLevelData(UpgradeType type)
    {
        int next = GetLevel(type) + 1;
        if (next > 3 || upgradeData == null) return null;
        return upgradeData.GetLevel(type, next);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Compra de mejoras
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Intenta comprar el siguiente nivel de una mejora.
    /// Devuelve true si la compra fue exitosa.
    /// </summary>
    public bool PurchaseUpgrade(UpgradeType type)
    {
        if (IsMaxLevel(type))
        {
            Debug.Log($"[VehicleUpgradeManager] {type} ya está al nivel máximo.");
            return false;
        }

        int nextLevel = GetLevel(type) + 1;
        if (upgradeData == null)
        {
            Debug.LogError("[VehicleUpgradeManager] upgradeData es null. Asigna el ScriptableObject en el Inspector.");
            return false;
        }

        var data = upgradeData.GetLevel(type, nextLevel);
        if (data == null) return false;

        // Intentar cobrar el precio
        if (FoodDeliveryManager.Instance == null || !FoodDeliveryManager.Instance.SpendMoney(data.price))
        {
            Debug.Log($"[VehicleUpgradeManager] Dinero insuficiente para {type} nivel {nextLevel}. Precio: ${data.price}");
            return false;
        }

        // Actualizar nivel
        switch (type)
        {
            case UpgradeType.Wheels:           wheelLevel   = nextLevel; break;
            case UpgradeType.Engine:           engineLevel  = nextLevel; break;
            case UpgradeType.ThermalInsulation: thermalLevel = nextLevel; break;
        }

        // Guardar persistencia
        SaveUpgrades();

        // Aplicar al vehículo
        ApplyAllUpgrades();

        Debug.Log($"[VehicleUpgradeManager] ✅ {type} mejorado a nivel {nextLevel} ({data.levelName}). Precio: ${data.price}");

        // Notificar a la UI
        OnUpgradeChanged?.Invoke();
        return true;
    }

    // ────────────────────────────────────────────────────────────────────
    //  Aplicación real de mejoras al VehicleController
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Recalcula y aplica TODOS los bonuses al VehicleController
    /// a partir de los niveles actuales.
    /// </summary>
    private void ApplyAllUpgrades()
    {
        if (vehicleController == null || upgradeData == null) return;

        // — Ruedas (turnSpeed) —
        var wheelData = upgradeData.GetLevel(UpgradeType.Wheels, wheelLevel);
        if (wheelData != null)
            vehicleController.turnSpeed = baseTurnSpeed + wheelData.turnSpeedBonus;

        // — Motor (acceleration + maxSpeed) —
        var engineData = upgradeData.GetLevel(UpgradeType.Engine, engineLevel);
        if (engineData != null)
        {
            vehicleController.acceleration = baseAcceleration + engineData.accelerationBonus;
            vehicleController.maxSpeed     = baseMaxSpeed     + engineData.maxSpeedBonus;
        }

        // — Aislamiento Térmico: PLACEHOLDER — no afecta ningún parámetro todavía —

        Debug.Log($"[VehicleUpgradeManager] Valores aplicados → " +
                  $"turnSpeed={vehicleController.turnSpeed:F1}  " +
                  $"accel={vehicleController.acceleration:F1}  " +
                  $"maxSpeed={vehicleController.maxSpeed:F1}");
    }
}
