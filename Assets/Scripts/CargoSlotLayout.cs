using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CargoSlotLayout : MonoBehaviour
{
    [Header("Slots Setup")]
    [Tooltip("Ranuras predefinidas donde se acomodarán los ítems. Si se deja vacío, tomará automáticamente a todos los GameObjects hijos.")]
    public Transform[] cargoSlots;

    [Header("Animation Settings")]
    [Tooltip("Duración del viaje/vuelo del objeto desde las manos hasta el slot.")]
    public float placementDuration = 0.45f;

    [Tooltip("Curva de suavizado del movimiento.")]
    public AnimationCurve placementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Altura del arco parabólico durante la animación (simula lanzar/acomodar hacia arriba y adentro).")]
    public float arcHeight = 0.35f;

    [Header("Audio (Opcional)")]
    public AudioClip placeItemSound;

    // Diccionario de ocupación: Slot -> Item
    private Dictionary<Transform, PickableItem> slotOccupancy = new Dictionary<Transform, PickableItem>();
    private AudioSource audioSource;

    public int TotalSlots => cargoSlots != null ? cargoSlots.Length : 0;
    public int StoredCount => slotOccupancy.Count;
    public bool HasAvailableSlot => GetNextAvailableSlot() != null;

    void Awake()
    {
        // 1. Si no se asignaron slots manualmente, tomar todos los hijos como ranuras
        if (cargoSlots == null || cargoSlots.Length == 0)
        {
            int childCount = transform.childCount;
            if (childCount > 0)
            {
                cargoSlots = new Transform[childCount];
                for (int i = 0; i < childCount; i++)
                {
                    cargoSlots[i] = transform.GetChild(i);
                }
            }
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && placeItemSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
        }
    }

    /// <summary>
    /// Encuentra la primera ranura libre disponible en la van.
    /// </summary>
    public Transform GetNextAvailableSlot()
    {
        if (cargoSlots == null) return null;

        foreach (Transform slot in cargoSlots)
        {
            if (slot != null && !slotOccupancy.ContainsKey(slot))
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>
    /// Inicia el proceso de acomodo automático de un ítem hacia la siguiente ranura disponible.
    /// </summary>
    public bool StoreItem(PickableItem item, System.Action onComplete = null)
    {
        if (item == null) return false;

        Transform targetSlot = GetNextAvailableSlot();
        if (targetSlot == null)
        {
            Debug.LogWarning("[CargoSlotLayout] La van está llena. No quedan ranuras disponibles.");
            return false;
        }

        // Marcar la ranura como ocupada de inmediato para evitar que otro ítem intente usarla
        slotOccupancy[targetSlot] = item;

        StartCoroutine(AnimateItemToSlotCoroutine(item, targetSlot, onComplete));
        return true;
    }

    private IEnumerator AnimateItemToSlotCoroutine(PickableItem item, Transform slot, System.Action onComplete)
    {
        // 1. Inmediatamente preparar el ítem como objeto de carga (kinematic + trigger)
        // para que no genere ninguna fuerza ni contacto físico con el auto
        item.SetInCargo(transform);

        Vector3 startLocalPos = item.transform.localPosition;
        Quaternion startLocalRot = item.transform.localRotation;

        Vector3 endLocalPos = slot.localPosition;
        Quaternion endLocalRot = slot.localRotation;

        float elapsed = 0f;

        while (elapsed < placementDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / placementDuration);
            float curveT = placementCurve.Evaluate(progress);

            // Interpolación horizontal/profundidad
            Vector3 currentPos = Vector3.Lerp(startLocalPos, endLocalPos, curveT);

            // Arco parabólico en Y para dar la sensación de arco de acomodo
            float arcOffset = Mathf.Sin(progress * Mathf.PI) * arcHeight;
            currentPos.y += arcOffset;

            item.transform.localPosition = currentPos;
            item.transform.localRotation = Quaternion.Slerp(startLocalRot, endLocalRot, curveT);

            yield return null;
        }

        // 2. Snap exacto final en el slot
        item.transform.localPosition = endLocalPos;
        item.transform.localRotation = endLocalRot;

        // 3. Fijar el ítem en la van
        item.SetInCargo(transform);

        if (audioSource != null && placeItemSound != null)
        {
            audioSource.PlayOneShot(placeItemSound);
        }

        onComplete?.Invoke();
    }

    /// <summary>
    /// Remueve un ítem de una ranura si se deseara descargar en el futuro.
    /// </summary>
    public bool RemoveItem(PickableItem item)
    {
        Transform foundSlot = null;
        foreach (var kvp in slotOccupancy)
        {
            if (kvp.Value == item)
            {
                foundSlot = kvp.Key;
                break;
            }
        }

        if (foundSlot != null)
        {
            slotOccupancy.Remove(foundSlot);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Lista de todos los ítems almacenados actualmente.
    /// </summary>
    public List<PickableItem> GetStoredItems()
    {
        return new List<PickableItem>(slotOccupancy.Values);
    }

    private void OnDrawGizmosSelected()
    {
        // Visualizar las ranuras en la escena
        Gizmos.color = Color.cyan;

        if (cargoSlots != null)
        {
            foreach (var slot in cargoSlots)
            {
                if (slot != null)
                {
                    Gizmos.matrix = slot.localToWorldMatrix;
                    Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.4f, 0.4f, 0.4f));
                    Gizmos.DrawRay(Vector3.zero, Vector3.forward * 0.3f);
                }
            }
        }
        else
        {
            // Si aún no se asignaron en runtime, dibujar los hijos directamente
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                Gizmos.matrix = child.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.4f, 0.4f, 0.4f));
                Gizmos.DrawRay(Vector3.zero, Vector3.forward * 0.3f);
            }
        }

        Gizmos.matrix = Matrix4x4.identity;
    }
}
