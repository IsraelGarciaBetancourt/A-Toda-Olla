using UnityEngine;

/// <summary>
/// Tipos de mejora disponibles en el taller.
/// </summary>
public enum UpgradeType
{
    Wheels,           // Ruedas → afecta turnSpeed
    Engine,           // Motor  → afecta acceleration y maxSpeed
    ThermalInsulation // Aislamiento Térmico → placeholder (sin efecto por ahora)
}

/// <summary>
/// Datos de UN nivel de mejora (0‑3 estrellas).
/// </summary>
[System.Serializable]
public class UpgradeLevelData
{
    [Tooltip("Nombre display del nivel (ej: '★★☆ Ruedas Slick')")]
    public string levelName = "Nivel";

    [Tooltip("Precio para comprar ESTE nivel desde el nivel anterior (0 = nivel base, gratis).")]
    public int price = 0;

    [Tooltip("Bonus de turnSpeed que se SUMA al valor base del VehicleController.")]
    public float turnSpeedBonus = 0f;

    [Tooltip("Bonus de acceleration que se SUMA al valor base del VehicleController.")]
    public float accelerationBonus = 0f;

    [Tooltip("Bonus de maxSpeed que se SUMA al valor base del VehicleController.")]
    public float maxSpeedBonus = 0f;

    [Tooltip("Descripción corta para mostrar en la UI.")]
    [TextArea(1, 3)]
    public string description = "";
}

/// <summary>
/// ScriptableObject que centraliza los 4 niveles (0‑3 ★) de cada categoría de mejora.
/// Crea uno desde Assets → Create → A-Toda-Olla → Vehicle Upgrade Data.
/// </summary>
[CreateAssetMenu(menuName = "A-Toda-Olla/Vehicle Upgrade Data", fileName = "VehicleUpgradeData")]
public class VehicleUpgradeData : ScriptableObject
{
    [Header("Mejoras de Ruedas (turnSpeed)")]
    public UpgradeLevelData[] wheelLevels = new UpgradeLevelData[4];

    [Header("Mejoras de Motor (acceleration + maxSpeed)")]
    public UpgradeLevelData[] engineLevels = new UpgradeLevelData[4];

    [Header("Mejoras de Aislamiento Térmico (placeholder)")]
    public UpgradeLevelData[] thermalLevels = new UpgradeLevelData[4];

    /// <summary>Devuelve el array correcto según el tipo de mejora.</summary>
    public UpgradeLevelData[] GetLevels(UpgradeType type)
    {
        return type switch
        {
            UpgradeType.Wheels           => wheelLevels,
            UpgradeType.Engine           => engineLevels,
            UpgradeType.ThermalInsulation => thermalLevels,
            _                            => null
        };
    }

    /// <summary>
    /// Shortcut: devuelve los datos de un nivel concreto (clampado a 0‑3).
    /// </summary>
    public UpgradeLevelData GetLevel(UpgradeType type, int level)
    {
        var levels = GetLevels(type);
        if (levels == null || levels.Length == 0) return null;
        level = Mathf.Clamp(level, 0, levels.Length - 1);
        return levels[level];
    }
}
