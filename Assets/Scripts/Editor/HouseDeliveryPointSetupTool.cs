using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de Editor para configurar automáticamente los puntos de entrega (DeliveryPoint)
/// en los 7 prefabs de casas de Nivel 1 (CasaTipo-A a CasaTipo-T).
/// </summary>
public static class HouseDeliveryPointSetupTool
{
    private static readonly string HOUSES_DIR = "Assets/Prefabs/Houses/Nivel1";

    private static readonly string[] HOUSE_PREFABS = new string[]
    {
        "CasaTipo-A.prefab",
        "CasaTipo-F.prefab",
        "CasaTipo-I.prefab",
        "CasaTipo-K.prefab",
        "CasaTipo-M.prefab",
        "CasaTipo-R.prefab",
        "CasaTipo-T.prefab"
    };

    [MenuItem("Tools/Delivery/1. Setup Delivery Points on House Prefabs")]
    public static void SetupDeliveryPoints()
    {
        int configuredCount = 0;

        foreach (string prefabName in HOUSE_PREFABS)
        {
            string prefabPath = Path.Combine(HOUSES_DIR, prefabName);
            if (!File.Exists(prefabPath))
            {
                Debug.LogWarning($"[DeliverySetup] No se encontró el prefab: {prefabPath}");
                continue;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[DeliverySetup] Error al cargar: {prefabPath}");
                continue;
            }

            try
            {
                ConfigurePrefabDeliveryPoint(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                configuredCount++;
                Debug.Log($"[DeliverySetup] ✅ Configurado DeliveryPoint en: {prefabName}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[DeliverySetup] 🏁 Proceso finalizado con éxito: {configuredCount}/{HOUSE_PREFABS.Length} prefabs configurados.");
    }

    private static void ConfigurePrefabDeliveryPoint(GameObject prefabRoot)
    {
        // 1. Si ya existe un hijo DeliveryPoint, lo reutilizamos o limpiamos
        Transform existing = prefabRoot.transform.Find("DeliveryPoint");
        GameObject deliveryGO;
        if (existing != null)
        {
            deliveryGO = existing.gameObject;
        }
        else
        {
            deliveryGO = new GameObject("DeliveryPoint");
            deliveryGO.transform.SetParent(prefabRoot.transform, false);
        }

        // 2. Localizar el modelo del edificio (building-type-*)
        Transform buildingModel = null;
        foreach (Transform child in prefabRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child != prefabRoot.transform && child.name.StartsWith("building-type-", System.StringComparison.OrdinalIgnoreCase))
            {
                buildingModel = child;
                break;
            }
        }

        // 3. Calcular la posición frontal (puerta / porche)
        Vector3 doorPosition = Vector3.zero;
        if (buildingModel != null)
        {
            BoxCollider box = buildingModel.GetComponent<BoxCollider>();
            float forwardDistance = 4.2f;

            if (box != null)
            {
                // En espacio local del modelo, la profundidad es box.size.z * scale.z
                float halfDepth = (box.size.z * 0.5f) * Mathf.Abs(buildingModel.localScale.z);
                forwardDistance = halfDepth + 1.2f;
            }

            // El modelo mira en la dirección de buildingModel.forward
            Vector3 worldFront = buildingModel.position + buildingModel.forward * forwardDistance;
            doorPosition = prefabRoot.transform.InverseTransformPoint(worldFront);
            doorPosition.y = 0.05f; // A nivel del suelo
        }
        else
        {
            doorPosition = new Vector3(0f, 0.05f, -3.5f);
        }

        deliveryGO.transform.localPosition = doorPosition;
        deliveryGO.transform.localRotation = buildingModel != null ? buildingModel.localRotation : Quaternion.identity;

        // 4. Configurar componente DeliveryPoint
        DeliveryPoint dp = deliveryGO.GetComponent<DeliveryPoint>();
        if (dp == null) dp = deliveryGO.AddComponent<DeliveryPoint>();
        dp.houseName = prefabRoot.name;

        // 5. Configurar Collider Trigger
        BoxCollider trigger = deliveryGO.GetComponent<BoxCollider>();
        if (trigger == null) trigger = deliveryGO.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.0f, 0f);
        trigger.size = new Vector3(2.5f, 2.0f, 2.5f);

        // 6. Configurar DropSpot
        Transform dropSpot = deliveryGO.transform.Find("DropSpot");
        if (dropSpot == null)
        {
            GameObject dsGO = new GameObject("DropSpot");
            dsGO.transform.SetParent(deliveryGO.transform, false);
            dsGO.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            dropSpot = dsGO.transform;
        }
        dp.dropSpot = dropSpot;

        // 7 & 8. Configurar Visuales GTA (Corona en el Suelo + Haz de Luz en el Cielo)
        Material coronaMat = GTADeliveryVisualsSetup.GetOrCreateCoronaMaterial();
        Material ringMat = GTADeliveryVisualsSetup.GetOrCreateRingMaterial();
        Material skyBeaconMat = GTADeliveryVisualsSetup.GetOrCreateSkyBeaconMaterial();

        GTADeliveryVisualsSetup.ConfigureDeliveryPointVisuals(deliveryGO.transform, dp, coronaMat, ringMat, skyBeaconMat);
    }

    [MenuItem("Tools/Delivery/2. Remove Delivery Points from House Prefabs")]
    public static void RemoveDeliveryPoints()
    {
        int removedCount = 0;

        foreach (string prefabName in HOUSE_PREFABS)
        {
            string prefabPath = Path.Combine(HOUSES_DIR, prefabName);
            if (!File.Exists(prefabPath)) continue;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null) continue;

            try
            {
                Transform existing = prefabRoot.transform.Find("DeliveryPoint");
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    removedCount++;
                    Debug.Log($"[DeliverySetup] 🗑️ Eliminado DeliveryPoint de: {prefabName}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[DeliverySetup] Eliminados {removedCount} puntos de entrega.");
    }
}
