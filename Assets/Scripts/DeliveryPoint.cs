using System;
using System.Collections;
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
    public Color activeColor = new Color(1.0f, 0.78f, 0.18f, 1f);

    [Header("Audio / Efectos")]
    [Tooltip("Efecto de sonido al completar la entrega con éxito (si está vacío, se sintetiza una melodía de victoria).")]
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

    private static AudioClip proceduralChimeClip = null;

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
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f;
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
        if (!isCurrentDestination || item == null || item.IsDelivered) return false;

        FoodDeliveryManager manager = FoodDeliveryManager.Instance;
        if (manager != null)
        {
            return manager.IsDeliverableFoodItem(item);
        }

        return item.isDeliverableFood || item.name.StartsWith("Olla") || item.itemName.Contains("Olla");
    }

    /// <summary>
    /// Ejecuta la entrega del ítem en este punto.
    /// Desvincula el ítem del jugador, lo sitúa en el dropSpot, reproduce los efectos y lo fija.
    /// </summary>
    public bool Deliver(PickableItem item, PlayerPickup player = null)
    {
        if (!CanDeliverItem(item)) return false;

        // Si el jugador lo tiene en manos, soltarlo y restaurar velocidad
        if (player != null && player.CurrentItem == item)
        {
            player.ReleaseItemToCargo();
        }

        // Posicionar el ítem exactamente en el dropSpot, perfectamente erguido
        Vector3 targetPos = dropSpot != null ? dropSpot.position : transform.position;
        float rotY = dropSpot != null ? dropSpot.eulerAngles.y : transform.eulerAngles.y;
        Quaternion targetRot = Quaternion.Euler(0f, rotY, 0f);

        // Desvincular del jugador o van y asentar
        item.Drop(targetPos, targetRot);

        // Marcar definitivamente como entregado
        item.MarkAsDelivered();

        // Animación suave de rebote táctil en la olla
        StartCoroutine(AnimateDeliveredPot(item.transform));

        // Feedback audiovisual de celebración
        PlayFeedback(targetPos);

        // Notificar eventos
        OnItemDelivered?.Invoke(item);

        // Desactivar el destino activo
        SetActiveDestination(false);

        return true;
    }

    /// <summary>
    /// Busca si hay alguna olla/comida dentro del área de este punto de entrega.
    /// </summary>
    public PickableItem FindFoodItemInDeliveryZone()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null) return null;

        Bounds bounds = triggerCollider.bounds;
        Collider[] hits = Physics.OverlapBox(bounds.center, bounds.extents, transform.rotation);
        for (int i = 0; i < hits.Length; i++)
        {
            PickableItem item = hits[i].GetComponentInParent<PickableItem>();
            if (item != null && !item.IsDelivered && !item.IsBeingCarried && CanDeliverItem(item))
            {
                return item;
            }
        }
        return null;
    }

    private IEnumerator AnimateDeliveredPot(Transform potTransform)
    {
        if (potTransform == null) yield break;

        Vector3 originalScale = potTransform.localScale;
        Vector3 popScale = originalScale * 1.22f;
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration && potTransform != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Curva de rebote amortiguado
            float bounce = Mathf.Sin(t * Mathf.PI);
            potTransform.localScale = Vector3.Lerp(originalScale, popScale, bounce);
            yield return null;
        }

        if (potTransform != null)
        {
            potTransform.localScale = originalScale;
        }
    }

    private void PlayFeedback(Vector3 position)
    {
        // 1. Efecto de partículas
        if (deliveryParticles != null)
        {
            deliveryParticles.Play();
        }
        else
        {
            SpawnCelebrationParticles(position + Vector3.up * 0.25f);
        }

        // 2. Audio de éxito
        AudioClip clipToPlay = deliverySuccessSound;
        if (clipToPlay == null)
        {
            clipToPlay = GetOrCreateDeliveryChime();
        }

        if (clipToPlay != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(clipToPlay, 1.0f);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clipToPlay, position, 1.0f);
            }
        }
    }

    /// <summary>
    /// Genera procedimentalmente una agradable fanfarria/chime de 4 notas (Do, Mi, Sol, Do agudo)
    /// garantizando que la entrega siempre tenga feedback sonoro de alta calidad.
    /// </summary>
    private static AudioClip GetOrCreateDeliveryChime()
    {
        if (proceduralChimeClip != null) return proceduralChimeClip;

        int sampleRate = 44100;
        float duration = 0.55f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // 4 notas con arpegio rápido: C5 (523.25), E5 (659.25), G5 (783.99), C6 (1046.5)
        float[] frequencies = { 523.25f, 659.25f, 783.99f, 1046.50f };
        float noteDuration = duration / 4f;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / sampleRate;
            int noteIndex = Mathf.Clamp(Mathf.FloorToInt(time / noteDuration), 0, 3);
            float freq = frequencies[noteIndex];
            float noteTime = time - (noteIndex * noteDuration);

            // Envolvente de decaimiento exponencial por nota
            float envelope = Mathf.Exp(-noteTime * 12f);
            // Tono principal + armónico suave de campana
            float wave = Mathf.Sin(2f * Mathf.PI * freq * time) * 0.7f
                       + Mathf.Sin(4f * Mathf.PI * freq * time) * 0.25f
                       + Mathf.Sin(6f * Mathf.PI * freq * time) * 0.05f;

            samples[i] = wave * envelope * 0.45f;
        }

        proceduralChimeClip = AudioClip.Create("DeliveryChime_Procedural", sampleCount, 1, sampleRate, false);
        proceduralChimeClip.SetData(samples, 0);
        return proceduralChimeClip;
    }

    /// <summary>
    /// Spawnea una explosión festiva de estrellas y chispas doradas al entregar.
    /// </summary>
    private void SpawnCelebrationParticles(Vector3 position)
    {
        GameObject fxGO = new GameObject("DeliveryCelebrationFX");
        fxGO.transform.position = position;

        ParticleSystem ps = fxGO.AddComponent<ParticleSystem>();

        // CRÍTICO: detener inmediatamente el auto-play que Unity inicia al crear el componente.
        // Si no se hace esto, configurar main.duration lanza una excepción en runtime.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer psRenderer = fxGO.GetComponent<ParticleSystemRenderer>();

        // Material por defecto brillante
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                             ?? Shader.Find("Particles/Standard Unlit")
                             ?? Shader.Find("Hidden/InternalErrorShader");
        if (particleShader != null)
        {
            Material particleMat = new Material(particleShader);
            particleMat.color = new Color(1f, 0.85f, 0.25f, 1f);
            psRenderer.material = particleMat;
        }

        // Ahora ya es seguro configurar el módulo main
        var main = ps.main;
        main.duration = 1.0f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.9f, 0.2f, 1f),
            new Color(1f, 0.6f, 0.1f, 1f)
        );
        main.gravityModifier = 0.35f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 35) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.2f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // Disparar hacia arriba

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(0.7f, 0.8f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ps.Play();
        Destroy(fxGO, 2.5f);
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerPickup player = other.GetComponent<PlayerPickup>() ?? other.GetComponentInParent<PlayerPickup>();
        if (player != null)
        {
            isPlayerInZone = true;
            playerInZoneRef = player;
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (!isCurrentDestination) return;

        // Si una olla fue soltada dentro de la zona de entrega, procesar entrega automáticamente
        PickableItem item = other.GetComponent<PickableItem>() ?? other.GetComponentInParent<PickableItem>();
        if (item != null && !item.IsBeingCarried && !item.IsDelivered && CanDeliverItem(item))
        {
            Deliver(item, null);
        }
    }

    void OnTriggerExit(Collider other)
    {
        PlayerPickup player = other.GetComponent<PlayerPickup>() ?? other.GetComponentInParent<PlayerPickup>();
        if (player != null && player == playerInZoneRef)
        {
            isPlayerInZone = false;
            playerInZoneRef = null;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isCurrentDestination ? Color.green : new Color(1f, 0.9f, 0.2f, 0.4f);
        Vector3 center = dropSpot != null ? dropSpot.position : transform.position;
        Gizmos.DrawWireSphere(center, 0.5f);

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
