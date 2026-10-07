using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PickableItem : MonoBehaviour
{
    [Header("Item Data")]
    [Tooltip("Nombre descriptivo del objeto para la UI.")]
    public string itemName = "Caja de Suministros";

    [Tooltip("Peso en kilogramos. Afectará la velocidad y aceleración de la van.")]
    public float weightKg = 10f;

    [Header("Hold Offset (Posición en Manos)")]
    [Tooltip("Desplazamiento relativo al punto de agarre (HoldPoint) del jugador.")]
    public Vector3 holdOffset = Vector3.zero;

    [Tooltip("Rotación relativa en ángulos Euler cuando el jugador lo lleva en brazos.")]
    public Vector3 holdRotation = Vector3.zero;

    [Header("Food / Delivery Data")]
    [Tooltip("¿Este ítem es apto para entregarse como pedido de comida?")]
    public bool isDeliverableFood = true;

    [Header("Efectos de Comida / Vapor")]
    [Tooltip("Sistema de partículas de vapor caliente. Si está vacío y es comida, se autogenera.")]
    public ParticleSystem steamParticleSystem;

    [Header("State (Solo Lectura)")]
    [SerializeField] private bool isBeingCarried = false;
    [SerializeField] private bool isStoredInCargo = false;
    [SerializeField] private bool isDelivered = false;

    public bool IsBeingCarried => isBeingCarried;
    public bool IsStoredInCargo => isStoredInCargo;
    public bool IsDelivered => isDelivered;
    public bool IsFoodCold { get; private set; } = false;

    private Rigidbody rb;
    private Collider[] itemColliders;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        itemColliders = GetComponentsInChildren<Collider>();
    }

    void Start()
    {
        if (isDeliverableFood && !isDelivered)
        {
            EnsureSteamParticles();
        }
    }

    /// <summary>
    /// Llamado cuando el jugador recoge el ítem con sus manos.
    /// </summary>
    public void PickUp(Transform holdParent)
    {
        isBeingCarried = true;
        isStoredInCargo = false;

        // Desactivar físicas para que no empuje al CharacterController del jugador
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Convertir los colliders en triggers mientras se carga para evitar atascos o colisiones con el cuerpo del jugador
        SetCollidersTrigger(true);

        // Hacerlo hijo del punto de agarre del jugador
        transform.SetParent(holdParent);
        transform.localPosition = holdOffset;
        transform.localRotation = Quaternion.Euler(holdRotation);
    }

    /// <summary>
    /// Llamado cuando el jugador suelta el ítem al suelo.
    /// Si se especifica groundPosition, se planta exactamente en seco en esa posición sin rebote.
    /// </summary>
    public void Drop(Vector3? groundPosition = null, Quaternion? targetRotation = null)
    {
        isBeingCarried = false;
        isStoredInCargo = false;

        // Desvincular del jugador
        transform.SetParent(null);

        // Reactivar colisiones normales
        SetCollidersTrigger(false);

        if (groundPosition.HasValue)
        {
            transform.position = groundPosition.Value;
        }

        if (targetRotation.HasValue)
        {
            transform.rotation = targetRotation.Value;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            // Detener cualquier inercia o velocidad acumulada para que quede en seco
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            // Poner el Rigidbody en reposo instantáneo (Sleep) para que no rebote ni ruede
            rb.Sleep();
        }
    }

    /// <summary>
    /// Marca el ítem como entregado con éxito en una casa.
    /// Fija su posición, apaga las físicas y desactiva el script para que no se pueda volver a recoger.
    /// </summary>
    public void MarkAsDelivered()
    {
        isDelivered = true;
        isBeingCarried = false;
        isStoredInCargo = false;
        SetSteamEmission(false);

        transform.SetParent(null);

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        // Dejar colliders sólidos para que repose en el suelo
        SetCollidersTrigger(false);

        // Desactivar el componente PickableItem para que los raycasts lo ignoren
        this.enabled = false;
    }

    /// <summary>
    /// Llamado cuando el ítem se acomoda dentro de la van.
    /// </summary>
    public void SetInCargo(Transform cargoParent)
    {
        isBeingCarried = false;
        isStoredInCargo = true;

        // Se convierte en hijo de la van (o de su CargoArea) para moverse junto a ella
        transform.SetParent(cargoParent);

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // ¡IMPORTANTE!: En la van los colliders DEBEN ser triggers para no chocar
        // con la carrocería o el piso del auto, lo cual provocaría que el vehículo se voltee por físicas.
        SetCollidersTrigger(true);

        // Ignorar colisiones físicas explícitamente con todos los colisionadores del auto
        IgnoreVehicleCollisions(cargoParent);
    }

    private void IgnoreVehicleCollisions(Transform cargoParent)
    {
        if (cargoParent == null) return;

        Collider[] vehicleColliders = cargoParent.root.GetComponentsInChildren<Collider>();
        if (itemColliders == null || itemColliders.Length == 0)
        {
            itemColliders = GetComponentsInChildren<Collider>();
        }

        foreach (var myCol in itemColliders)
        {
            if (myCol == null) continue;
            foreach (var vehCol in vehicleColliders)
            {
                if (vehCol == null || vehCol == myCol) continue;
                Physics.IgnoreCollision(myCol, vehCol, true);
            }
        }
    }

    private void SetCollidersTrigger(bool isTrigger)
    {
        if (itemColliders == null || itemColliders.Length == 0)
        {
            itemColliders = GetComponentsInChildren<Collider>();
        }

        foreach (var col in itemColliders)
        {
            if (col != null)
            {
                col.isTrigger = isTrigger;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Gizmo para visualizar en el editor el punto de agarre relativo
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + transform.TransformDirection(holdOffset), 0.1f);
    }

    /// <summary>
    /// Asegura que la olla posea un sistema de partículas de vapor visible si es un ítem de comida caliente.
    /// </summary>
    public void EnsureSteamParticles()
    {
        if (steamParticleSystem != null) return;

        steamParticleSystem = GetComponentInChildren<ParticleSystem>();
        if (steamParticleSystem != null) return;

        GameObject steamGO = new GameObject("FoodSteamParticles");
        steamGO.transform.SetParent(transform, false);
        steamGO.transform.localPosition = new Vector3(0f, 0.22f, 0f);

        ParticleSystem ps = steamGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer psRenderer = steamGO.GetComponent<ParticleSystemRenderer>();
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                 ?? Shader.Find("Particles/Standard Unlit")
                 ?? Shader.Find("Sprites/Default");
        if (sh != null)
        {
            Material mat = new Material(sh);
            mat.color = new Color(0.95f, 0.95f, 0.95f, 0.28f);
            psRenderer.material = mat;
        }

        var main = ps.main;
        main.duration = 2.0f;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
        main.startColor = new Color(0.96f, 0.96f, 0.96f, 0.25f);
        main.gravityModifier = -0.05f; // Flotar suavemente hacia arriba
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 8f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.12f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0.5f);
        curve.AddKey(1f, 1.4f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        steamParticleSystem = ps;
        ps.Play();
    }

    /// <summary>
    /// Enciende o apaga la emisión de vapor. Se apaga al enfriarse la comida tras expirar el pedido o al entregarse.
    /// </summary>
    public void SetSteamEmission(bool active)
    {
        IsFoodCold = !active;
        if (steamParticleSystem != null)
        {
            var emission = steamParticleSystem.emission;
            emission.enabled = active;
        }
    }
}
