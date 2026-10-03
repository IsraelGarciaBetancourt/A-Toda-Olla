using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PotDeliveryValidator
{
    [MenuItem("Tools/A-Toda-Olla/Validar Entrega de Ollas")]
    public static void ValidatePotDelivery()
    {
        Debug.Log("================ INICIANDO VALIDACIÓN DE OLLAS Y ENTREGA ================");

        // 1. Validar Prefab OllaConComida.prefab
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/OllaConComida.prefab");
        if (prefab == null)
        {
            Debug.LogError("❌ No se encontró Assets/Prefabs/Objects/OllaConComida.prefab");
            return;
        }

        PickableItem prefabItem = prefab.GetComponent<PickableItem>();
        if (prefabItem == null)
        {
            Debug.LogError("❌ El prefab OllaConComida no tiene componente PickableItem");
            return;
        }

        Debug.Log($"✅ Prefab OllaConComida validado: itemName='{prefabItem.itemName}', weightKg={prefabItem.weightKg}, isDeliverableFood={prefabItem.isDeliverableFood}");

        if (prefabItem.itemName != "Olla con Comida")
        {
            Debug.LogWarning($"⚠️ El itemName del prefab es '{prefabItem.itemName}' (se esperaba 'Olla con Comida')");
        }

        if (prefabItem.weightKg > 20f)
        {
            Debug.LogWarning($"⚠️ El peso del prefab es {prefabItem.weightKg}kg (muy pesado)");
        }

        // 2. Validar Ollas en la escena Game.unity
        PickableItem[] pots = Object.FindObjectsByType<PickableItem>(FindObjectsInactive.Include);
        Debug.Log($"🔍 Total de PickableItems encontrados en escena: {pots.Length}");

        int validPots = 0;
        FoodDeliveryManager manager = Object.FindAnyObjectByType<FoodDeliveryManager>();
        DeliveryPoint[] deliveryPoints = Object.FindObjectsByType<DeliveryPoint>(FindObjectsInactive.Include);

        foreach (var pot in pots)
        {
            bool isDeliverableByManager = manager != null ? manager.IsDeliverableFoodItem(pot) : pot.name.Contains("Olla");
            Debug.Log($"   🍲 Olla en escena: '{pot.name}' (itemName: '{pot.itemName}', peso: {pot.weightKg}kg) -> Apta para entregar: {isDeliverableByManager}");
            if (isDeliverableByManager) validPots++;
        }

        Debug.Log($"✅ Ollas aptas para entregar en escena: {validPots}/{pots.Length}");
        Debug.Log($"🏠 Casas con DeliveryPoint registradas en escena: {deliveryPoints.Length}");

        if (manager != null)
        {
            Debug.Log($"📦 FoodDeliveryManager estado: CurrentState={manager.CurrentState}, Recompensa=${manager.rewardPerDelivery}, AutoStart={manager.autoStartOnPlay}");
        }

        Debug.Log("================ VALIDACIÓN COMPLETADA CON ÉXITO ================");
    }
}
