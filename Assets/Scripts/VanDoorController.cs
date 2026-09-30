using UnityEngine;
using UnityEngine.InputSystem;

public class VanDoorController : MonoBehaviour
{
    [Header("Door Reference")]
    [Tooltip("El GameObject de la puerta ('door'). Si se deja vacío, se buscará automáticamente en los hijos.")]
    public GameObject doorObject;

    [Header("Interaction Settings")]
    [Tooltip("Acción de interacción del New Input System (por defecto Interact / tecla E).")]
    public InputActionReference interactAction;

    public enum DetectionShape { Box, Sphere }

    [Header("Detection Zone Transform & Shape")]
    [Tooltip("Forma de la zona de detección. 'Box' es la más recomendada para evitar activaciones desde los costados.")]
    public DetectionShape detectionShape = DetectionShape.Box;

    [Tooltip("Desplazamiento local (X, Y, Z) respecto al centro del vehículo.")]
    public Vector3 zoneOffset = new Vector3(0f, 0.7f, -1.9f);

    [Tooltip("Dimensiones de la caja (X = Ancho, Y = Alto, Z = Profundidad hacia atrás).")]
    public Vector3 boxDimensions = new Vector3(1.2f, 1.8f, 1.4f);

    [Tooltip("Radio si se utiliza la forma Sphere.")]
    public float sphereRadius = 1.6f;

    [Tooltip("Transform opcional (ej. un GameObject hijo vacío). Si lo arrastras aquí, puedes mover la zona visualmente con las flechas de Unity en la Escena.")]
    public Transform rearPoint;

    [Header("State")]
    [Tooltip("¿La puerta comienza abierta o cerrada? (Cerrada = puerta visible, Abierta = puerta oculta).")]
    [SerializeField] private bool isOpen = false;

    public bool IsOpen => isOpen;
    public bool PlayerInRearZone => playerInRearZone;
    public bool IsPlayerCarryingItem => playerPickup != null && playerPickup.IsCarryingItem;

    [Header("UI & Feedback (Opcional)")]
    [Tooltip("UI o texto flotante que aparece cuando el jugador está en la parte trasera.")]
    public GameObject doorPromptUI;

    [Tooltip("Sonido opcional al abrir la puerta.")]
    public AudioClip openSound;

    [Tooltip("Sonido opcional al cerrar la puerta.")]
    public AudioClip closeSound;

    // Evento para notificar a otros sistemas (como CargoZone en la Fase 4)
    public event System.Action<bool> OnDoorStateChanged;

    private Transform playerTransform;
    private PlayerPickup playerPickup;
    private CargoZone cargoZone;
    private AudioSource audioSource;
    private bool playerInRearZone = false;
    private float lastToggleTime = -1f;
    private float toggleCooldown = 0.35f;

    void Start()
    {
        // 1. Buscar automáticamente el GameObject 'door' si no se asignó en el Inspector
        if (doorObject == null)
        {
            Transform found = FindChildRecursive(transform, "door");
            if (found != null)
            {
                doorObject = found.gameObject;
            }
            else
            {
                Debug.LogWarning("[VanDoorController] No se encontró ningún hijo llamado 'door'. Asígnalo manualmente en el Inspector.");
            }
        }

        // 2. Establecer estado visual inicial (Cerrada = Visible, Abierta = Oculta)
        ApplyDoorVisualState();

        if (doorPromptUI != null)
        {
            doorPromptUI.SetActive(false);
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && (openSound != null || closeSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
        }

        // 3. Vincular CargoZone si existe
        cargoZone = GetComponentInChildren<CargoZone>();
        if (cargoZone == null && transform.parent != null)
        {
            cargoZone = transform.parent.GetComponentInChildren<CargoZone>();
        }

        // 4. Buscar al jugador en la escena si no se ha detectado aún
        FindPlayer();
    }

    void OnEnable()
    {
        if (interactAction != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += OnInteractPerformed;
        }
    }

    void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.action.performed -= OnInteractPerformed;
        }
    }

    void Update()
    {
        CheckPlayerProximity();
    }

    private void CheckPlayerProximity()
    {
        if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        Vector3 localCenter = GetLocalZoneCenter();
        Vector3 playerLocalPos = transform.InverseTransformPoint(playerTransform.position);

        bool inZone = false;
        if (detectionShape == DetectionShape.Box)
        {
            Vector3 diff = playerLocalPos - localCenter;
            inZone = Mathf.Abs(diff.x) <= boxDimensions.x * 0.5f &&
                     Mathf.Abs(diff.y) <= boxDimensions.y * 0.5f &&
                     Mathf.Abs(diff.z) <= boxDimensions.z * 0.5f;
        }
        else
        {
            inZone = Vector3.Distance(localCenter, playerLocalPos) <= sphereRadius;
        }

        bool wasInZone = playerInRearZone;
        playerInRearZone = inZone;

        if (doorPromptUI != null)
        {
            if (playerInRearZone)
            {
                UpdatePromptText();
            }
            else if (wasInZone)
            {
                doorPromptUI.SetActive(false);
            }
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!playerInRearZone) return;

        // 1. Si CargoZone acaba de guardar o descargar un objeto en la van, NO cambiar estado de la puerta
        if (cargoZone != null && (Time.time - cargoZone.LastLoadTime < 0.6f || Time.time - cargoZone.LastUnloadTime < 0.6f))
        {
            return;
        }

        // 2. Si el jugador está apuntando a CUALQUIER objeto recogible (en el suelo o en la van),
        // la pulsación de E es para recoger o sacar, NO para abrir o cerrar la puerta
        if (playerPickup != null && playerPickup.HoveredItem != null)
        {
            return;
        }

        // 3. Si el jugador acaba de recoger un objeto hace menos de 0.5s, NO alterar la puerta
        if (playerPickup != null && Time.time - playerPickup.LastPickupTime < 0.5f)
        {
            return;
        }

        // 3. Si el jugador lleva un objeto en brazos: ¡MANOS OCUPADAS!
        // No puede abrir ni cerrar la puerta hasta soltar el objeto con G
        if (playerPickup != null && playerPickup.IsCarryingItem)
        {
            UpdatePromptText();
            return;
        }

        // 4. Manos libres: puede abrir o cerrar la puerta normalmente
        ToggleDoor();
    }

    /// <summary>
    /// Alterna el estado de la puerta: la oculta si se abre, o la muestra si se cierra.
    /// </summary>
    public void ToggleDoor()
    {
        if (Time.time - lastToggleTime < toggleCooldown) return;
        lastToggleTime = Time.time;

        isOpen = !isOpen;
        ApplyDoorVisualState();

        // Reproducir sonido si está asignado
        if (audioSource != null)
        {
            AudioClip clip = isOpen ? openSound : closeSound;
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        // Notificar a la zona de carga y otros observadores
        OnDoorStateChanged?.Invoke(isOpen);
    }

    /// <summary>
    /// Abre o cierra la puerta explícitamente.
    /// </summary>
    public void SetDoorState(bool open)
    {
        if (isOpen == open) return;
        isOpen = open;
        ApplyDoorVisualState();
        OnDoorStateChanged?.Invoke(isOpen);
    }

    private void ApplyDoorVisualState()
    {
        if (doorObject != null)
        {
            // Abierta = Oculta (false), Cerrada = Visible (true)
            doorObject.SetActive(!isOpen);
        }
    }

    public Vector3 GetLocalZoneCenter()
    {
        if (rearPoint != null)
        {
            return transform.InverseTransformPoint(rearPoint.position);
        }
        return zoneOffset;
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerPickup = playerObj.GetComponent<PlayerPickup>();
        }
    }

    /// <summary>
    /// Mensaje descriptivo para la UI dependiendo de si las manos están ocupadas o libres.
    /// </summary>
    public string GetDoorPrompt()
    {
        if (!playerInRearZone) return string.Empty;

        bool hasItem = playerPickup != null && playerPickup.IsCarryingItem;

        if (!isOpen) // Puerta CERRADA
        {
            if (hasItem)
            {
                return "¡Manos ocupadas!\nPresiona [G] para soltar el objeto y poder abrir.";
            }
            return "[E] Abrir puerta trasera";
        }
        else // Puerta ABIERTA
        {
            if (hasItem)
            {
                // Con puerta abierta y objeto en mano, CargoZone muestra el prompt para guardar
                return string.Empty;
            }
            return "[E] Cerrar puerta trasera";
        }
    }

    private void UpdatePromptText()
    {
        if (doorPromptUI == null) return;

        // Si el objeto ya tiene el nuevo sistema VanDoorPromptHUD, dejar que él lo gestione
        if (doorPromptUI.GetComponent<VanDoorPromptHUD>() != null) return;

        string msg = GetDoorPrompt();
        if (string.IsNullOrEmpty(msg))
        {
            doorPromptUI.SetActive(false);
            return;
        }

        doorPromptUI.SetActive(true);

        // Actualizar componente UI.Text si existe
        UnityEngine.UI.Text uiText = doorPromptUI.GetComponentInChildren<UnityEngine.UI.Text>();
        if (uiText != null)
        {
            uiText.text = msg;
            return;
        }

        // Compatibilidad con TextMeshPro por reflexión sin errores de ensamblado
        Component[] components = doorPromptUI.GetComponentsInChildren<Component>();
        foreach (var c in components)
        {
            if (c != null && (c.GetType().Name.Contains("TMP_Text") || c.GetType().Name.Contains("TextMeshPro")))
            {
                var prop = c.GetType().GetProperty("text");
                if (prop != null)
                {
                    prop.SetValue(c, msg);
                    return;
                }
            }
        }
    }

    // Soporte también para Trigger Colliders si decides poner un BoxCollider en la parte trasera
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRearZone = true;
            if (doorPromptUI != null) doorPromptUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRearZone = false;
            if (doorPromptUI != null) doorPromptUI.SetActive(false);
        }
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return child;
            Transform result = FindChildRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualizar en la vista de Escena la zona de detección orientada con el vehículo
        Gizmos.color = isOpen ? Color.green : Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Vector3 localCenter = GetLocalZoneCenter();

        if (detectionShape == DetectionShape.Box)
        {
            Gizmos.DrawWireCube(localCenter, boxDimensions);
        }
        else
        {
            Gizmos.DrawWireSphere(localCenter, sphereRadius);
        }

        Gizmos.DrawRay(localCenter, -Vector3.forward * 0.5f);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
