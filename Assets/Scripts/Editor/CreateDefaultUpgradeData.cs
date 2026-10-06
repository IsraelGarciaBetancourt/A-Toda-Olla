using UnityEngine;
using UnityEditor;

/// <summary>
/// Herramienta de editor para crear el ScriptableObject de datos de mejoras
/// con valores predefinidos y balanceados.
/// Menu: Tools → A-Toda-Olla → Create Default Upgrade Data
/// </summary>
public static class CreateDefaultUpgradeData
{
    [MenuItem("Tools/A-Toda-Olla/Crear VehicleUpgradeData por Defecto")]
    public static void CreateAsset()
    {
        var asset = ScriptableObject.CreateInstance<VehicleUpgradeData>();

        // ── RUEDAS (4 niveles: 0★ a 3★) ─────────────────────────────
        // Nivel base (0★): sin bonus
        // Nivel 1★: +15 turnSpeed  → se siente algo más ágil
        // Nivel 2★: +35 turnSpeed  → giro notablemente mejor
        // Nivel 3★: +60 turnSpeed  → ágil al máximo
        asset.wheelLevels = new UpgradeLevelData[]
        {
            new UpgradeLevelData
            {
                levelName      = "☆☆☆ Stock",
                price          = 0,
                turnSpeedBonus = 0f,
                description    = "Ruedas de fábrica. Giro básico."
            },
            new UpgradeLevelData
            {
                levelName      = "★☆☆ Radiales",
                price          = 300,
                turnSpeedBonus = 15f,
                description    = "Llantas radiales. Respuesta de dirección mejorada."
            },
            new UpgradeLevelData
            {
                levelName      = "★★☆ Performance",
                price          = 700,
                turnSpeedBonus = 35f,
                description    = "Compuesto performance. Giro notablemente más rápido."
            },
            new UpgradeLevelData
            {
                levelName      = "★★★ Slick Pro",
                price          = 1400,
                turnSpeedBonus = 60f,
                description    = "Slicks de competición. Máxima agilidad en curvas."
            }
        };

        // ── MOTOR (4 niveles: 0★ a 3★) ───────────────────────────────
        // Base: acceleration=30, maxSpeed=20
        // Nivel 1★: +8 accel / +3 speed  → se nota pero no exagerado
        // Nivel 2★: +18 accel / +7 speed → 50%+ aceleración, velocidad punta clara
        // Nivel 3★: +32 accel / +12 speed → bestia
        asset.engineLevels = new UpgradeLevelData[]
        {
            new UpgradeLevelData
            {
                levelName          = "☆☆☆ Motor Stock",
                price              = 0,
                accelerationBonus  = 0f,
                maxSpeedBonus      = 0f,
                description        = "Motor de serie. Suficiente para hacer entregas."
            },
            new UpgradeLevelData
            {
                levelName          = "★☆☆ Filtro & Escape",
                price              = 400,
                accelerationBonus  = 8f,
                maxSpeedBonus      = 3f,
                description        = "Filtro de aire y escape libre. Más respuesta al gas."
            },
            new UpgradeLevelData
            {
                levelName          = "★★☆ Turbo Kit",
                price              = 900,
                accelerationBonus  = 18f,
                maxSpeedBonus      = 7f,
                description        = "Turbocompresor instalado. Aceleración y punta superiores."
            },
            new UpgradeLevelData
            {
                levelName          = "★★★ Motor Racing",
                price              = 1800,
                accelerationBonus  = 32f,
                maxSpeedBonus      = 12f,
                description        = "Bloque de competición. Velocidad máxima brutal."
            }
        };

        // ── AISLAMIENTO TÉRMICO (4 niveles: 0★ a 3★) — PLACEHOLDER ──
        // Sin bonus mecánico hasta que se implemente la mecánica de temperatura
        asset.thermalLevels = new UpgradeLevelData[]
        {
            new UpgradeLevelData
            {
                levelName   = "☆☆☆ Sin Aislamiento",
                price       = 0,
                description = "La comida se enfría rápido. ¡Date prisa!"
            },
            new UpgradeLevelData
            {
                levelName   = "★☆☆ Bolsa Térmica",
                price       = 250,
                description = "Bolsa térmica básica. Algo más de tiempo. (próximamente)"
            },
            new UpgradeLevelData
            {
                levelName   = "★★☆ Caja Aislante",
                price       = 600,
                description = "Caja con aislante de poliestireno. Buen margen de temperatura. (próximamente)"
            },
            new UpgradeLevelData
            {
                levelName   = "★★★ Termo Profesional",
                price       = 1200,
                description = "Contenedor térmico profesional. La comida llega caliente siempre. (próximamente)"
            }
        };

        // Guardar el asset en la carpeta Resources para poder cargarlo en runtime
        string folderPath = "Assets/Resources";
        if (!System.IO.Directory.Exists(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        string assetPath = $"{folderPath}/VehicleUpgradeData.asset";
        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;

        Debug.Log($"[CreateDefaultUpgradeData] ✅ VehicleUpgradeData creado en: {assetPath}");
    }
}
