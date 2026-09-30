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

    [Header("State (Solo Lectura)")]
    [SerializeField] private bool isBeingCarried = false;
    [SerializeField] private bool isStoredInCargo = false;

    public bool IsBeingCarried => isBeingCarried;
    public bool IsStoredInCargo => isStoredInCargo;

    private Rigidbody rb;
    private Collider[] itemColliders;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        itemColliders = GetComponentsInChildren<Collider>();
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
}
