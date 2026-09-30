using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Representa el punto de entrega de comida en una casa.
/// Contiene el área de interacción (Trigger), el punto de depósito (DropSpot)
/// y los marcadores visuales que se activan cuando la casa es el destino de la orden.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DeliveryPoint : MonoBehaviour
{
    [Header("Identificación")]
    [Tooltip("Nombre o identificador descriptivo de la casa (ej: Casa A-01). Si está vacío toma el nombre del GameObject.")]
    public string houseName = "";

    [Header("Punto de Depósito")]
    [Tooltip("Transform exacto donde descansará la olla/comida entregada. Si se deja vacío, se usa este mismo Transform.")]
    public Transform dropSpot;

    [Header("Marcadores Visuales (Destino Activo)")]
    [Tooltip("Baliza o pilar de luz visible desde lejos cuando esta casa es el objetivo actual.")]
    public GameObject beaconVisual;

    [Tooltip("Aro o indicador en el suelo frente a la puerta.")]
    public GameObject groundMarker;

    [Tooltip("Color del indicador visual cuando está activo.")]
    public Color activeColor = new Color(0.2f, 0.9f, 0.3f, 0.8f);

    [Header("Audio / Efectos")]
    [Tooltip("Efecto de sonido al completar la entrega con éxito.")]
    public AudioClip deliverySuccessSound;

    [Tooltip("Efecto de partículas opcional que se reproduce al entregar.")]
    public ParticleSystem deliveryParticles;

    [Header("Eventos")]
    public UnityEvent<PickableItem> OnItemDelivered;

    // Estado interno
    private bool isCurrentDestination = false;
    private bool isPlayerInZone = false;
    private PlayerPickup playerInZoneRef = null;
    private Collider triggerCollider;
    private AudioSource audioSource;

    public bool IsCurrentDestination => isCurrentDestination;
    public bool IsPlayerInZone => isPlayerInZone;
    public PlayerPickup PlayerInZoneRef => playerInZoneRef;

    void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && deliverySuccessSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D Audio
        }

        if (string.IsNullOrEmpty(houseName))
        {
            houseName = transform.parent != null ? transform.parent.name : gameObject.name;
        }

        if (dropSpot == null)
        {
            dropSpot = transform;
        }

        // Iniciar apagado hasta que el FoodDeliveryManager lo asigne
        SetActiveDestination(false);
    }

    /// <summary>
    /// Activa o desactiva este punto como el destino actual de la misión.
    /// </summary>
    public void SetActiveDestination(bool active)
    {
        isCurrentDestination = active;

        if (beaconVisual != null)
        {
            beaconVisual.SetActive(active);
        }

        if (groundMarker != null)
        {
            groundMarker.SetActive(active);
        }
    }

    /// <summary>
    /// Valida si el ítem que trae el jugador es apto para entregarse.
    /// </summary>
    public bool CanDeliverItem(PickableItem item)
    {
        if (!isCurrentDestination || item == null) return false;

        // Por ahora acepta cualquier PickableItem o específicamente OllaConComida
        return true;
    }

    /// <summary>
    /// Ejecuta la entrega del ítem en este punto.
    /// Desvincula el ítem del jugador, lo sitúa en el dropSpot y reproduce los efectos.
    /// </summary>
    public bool Deliver(PickableItem item, PlayerPickup player = null)
    {
        if (!CanDeliverItem(item)) return false;

        // Si el jugador lo tiene en manos, soltarlo
        if (player != null && player.CurrentItem == item)
        {
            player.ReleaseItemToCargo(); // Libera la referencia del jugador y restaura su velocidad
        }

        // Posicionar el ítem exactamente en el dropSpot
        Vector3 targetPos = dropSpot != null ? dropSpot.position : transform.position;
        Quaternion targetRot = dropSpot != null ? dropSpot.rotation : transform.rotation;

        item.Drop(targetPos, targetRot);

        // Dejarlo estático (kinematic) en el porche para que no ruede por físicas
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // Feedback audiovisual
        PlayFeedback();

        // Notificar eventos
        OnItemDelivered?.Invoke(item);

        // Desactivar el destino activo
        SetActiveDestination(false);

        return true;
    }

    private void PlayFeedback()
    {
        if (deliveryParticles != null)
        {
            deliveryParticles.Play();
        }

        if (audioSource != null && deliverySuccessSound != null)
        {
            audioSource.PlayOneShot(deliverySuccessSound);
        }
        else if (deliverySuccessSound != null)
        {
            AudioSource.PlayClipAtPoint(deliverySuccessSound, transform.position);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerPickup player = other.GetComponent<PlayerPickup>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerPickup>();
        }

        if (player != null)
        {
            isPlayerInZone = true;
            playerInZoneRef = player;
        }
    }

    void OnTriggerExit(Collider other)
    {
        PlayerPickup player = other.GetComponent<PlayerPickup>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerPickup>();
        }

        if (player != null && player == playerInZoneRef)
        {
            isPlayerInZone = false;
            playerInZoneRef = null;
        }
    }

    private void OnDrawGizmos()
    {
        // Gizmo visible siempre en el editor para ubicar las zonas de entrega
        Gizmos.color = isCurrentDestination ? Color.green : new Color(1f, 0.9f, 0.2f, 0.4f);
        Vector3 center = dropSpot != null ? dropSpot.position : transform.position;
        Gizmos.DrawWireSphere(center, 0.5f);

        // Línea vertical que simula la baliza
        Gizmos.color = isCurrentDestination ? Color.green : Color.yellow;
        Gizmos.DrawLine(center, center + Vector3.up * 3f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z));
        }
    }
}
