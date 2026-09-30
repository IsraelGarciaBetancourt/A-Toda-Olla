using UnityEngine;
using UnityEngine.InputSystem;

public class CargoZone : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Sistema de slots y acomodo automático. Si se deja vacío, se buscará automáticamente.")]
    public CargoSlotLayout slotLayout;

    [Tooltip("Controlador de la puerta trasera de la van. Si se deja vacío, se buscará automáticamente.")]
    public VanDoorController vanDoorController;

    [Tooltip("Gestor de peso y penalizaciones. Si se deja vacío, se buscará automáticamente.")]
    public CargoManager cargoManager;

    [Tooltip("¿Es obligatorio que la puerta trasera esté abierta para poder cargar objetos?")]
    public bool requireDoorOpen = true;

    [Header("Input Setup")]
    [Tooltip("Acción de interacción del New Input System (por defecto Interact / tecla E).")]
    public InputActionReference interactAction;

    [Header("Detection Zone (Caja 3D detrás de la van)")]
    [Tooltip("Desplazamiento local respecto a la van donde el jugador puede estar para cargar.")]
    public Vector3 zoneOffset = new Vector3(0f, 0.7f, -1.9f);

    [Tooltip("Dimensiones de la zona de carga (Ancho X, Alto Y, Profundidad Z).")]
    public Vector3 boxDimensions = new Vector3(1.4f, 1.8f, 1.5f);

    [Header("UI Prompt (Opcional)")]
    [Tooltip("UI opcional para mostrar mensajes contextuales (ej. 'Presiona E para guardar en la van').")]
    public GameObject loadPromptUI;

    // Evento que notifica cuando un ítem es acomodado en la van
    public event System.Action<PickableItem> OnItemLoadedIntoCargo;

    // Evento que notifica cuando un ítem es descargado de la van
    public event System.Action<PickableItem> OnItemUnloadedFromCargo;

    private Transform playerTransform;
    private PlayerPickup playerPickup;
    private bool playerInZone = false;
    private float lastLoadTime = -1f;
    private float lastUnloadTime = -1f;
    private float loadCooldown = 0.4f;

    public bool PlayerInZone => playerInZone;
    public float LastLoadTime => lastLoadTime;
    public float LastUnloadTime => lastUnloadTime;

    void Start()
    {
        // 1. Auto-asignar referencias si faltan
        if (slotLayout == null)
        {
            slotLayout = GetComponentInChildren<CargoSlotLayout>();
            if (slotLayout == null && transform.parent != null)
            {
                slotLayout = transform.parent.GetComponentInChildren<CargoSlotLayout>();
            }
        }

        if (vanDoorController == null)
        {
            vanDoorController = GetComponentInParent<VanDoorController>();
        }

        if (cargoManager == null)
        {
            cargoManager = GetComponentInParent<CargoManager>();
        }

        if (loadPromptUI != null)
        {
            loadPromptUI.SetActive(false);
        }

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

        // Evaluar posición del jugador en coordenadas locales de la van
        Vector3 playerLocalPos = transform.InverseTransformPoint(playerTransform.position);
        Vector3 diff = playerLocalPos - zoneOffset;

        bool inBox = Mathf.Abs(diff.x) <= boxDimensions.x * 0.5f &&
                     Mathf.Abs(diff.y) <= boxDimensions.y * 0.5f &&
                     Mathf.Abs(diff.z) <= boxDimensions.z * 0.5f;

        playerInZone = inBox;

        // Actualizar UI si está asignada
        if (loadPromptUI != null)
        {
            bool shouldShow = playerInZone && CanLoadItem();
            loadPromptUI.SetActive(shouldShow);
            if (shouldShow)
            {
                UpdateCargoPromptText();
            }
        }
    }

    private void UpdateCargoPromptText()
    {
        if (loadPromptUI == null || playerPickup == null || playerPickup.CurrentItem == null) return;

        string msg;
        if (cargoManager != null && cargoManager.IsFull)
        {
            msg = $"¡Capacidad al límite! ({cargoManager.CurrentWeight:F0}/{cargoManager.MaxWeight:F0}kg)";
        }
        else if (slotLayout != null && !slotLayout.HasAvailableSlot)
        {
            msg = "Van llena: No quedan ranuras disponibles.";
        }
        else
        {
            msg = $"[E] Guardar {playerPickup.CurrentItem.itemName} ({playerPickup.CurrentItem.weightKg}kg)";
        }

        UnityEngine.UI.Text uiText = loadPromptUI.GetComponentInChildren<UnityEngine.UI.Text>();
        if (uiText != null)
        {
            uiText.text = msg;
            return;
        }

        Component[] components = loadPromptUI.GetComponentsInChildren<Component>();
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

    /// <summary>
    /// Verifica si se cumplen todas las condiciones para cargar un objeto en la van.
    /// </summary>
    public bool CanLoadItem()
    {
        if (playerPickup == null || !playerPickup.IsCarryingItem) return false;

        // IMPORTANTE: Si el jugador acaba de recoger el objeto del suelo (hace menos de 0.45s),
        // NO cargarlo en la van con la misma pulsación de tecla E
        if (Time.time - playerPickup.LastPickupTime < 0.45f)
        {
            return false;
        }

        // Si se requiere puerta abierta, comprobar estado de VanDoorController
        if (requireDoorOpen && vanDoorController != null && !vanDoorController.IsOpen)
        {
            return false;
        }

        // Comprobar si hay slots disponibles
        if (slotLayout != null && !slotLayout.HasAvailableSlot)
        {
            return false;
        }

        // Comprobar si el vehículo ya alcanzó su capacidad de carga máxima en peso
        if (cargoManager != null && cargoManager.IsFull)
        {
            return false;
        }

        return true;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (playerInZone && CanLoadItem())
        {
            LoadItemFromPlayer();
        }
    }

    /// <summary>
    /// Toma el objeto de los brazos del jugador y lo entrega al sistema de acomodo automático.
    /// </summary>
    public void LoadItemFromPlayer()
    {
        if (Time.time - lastLoadTime < loadCooldown) return;
        lastLoadTime = Time.time;

        if (playerPickup == null || !playerPickup.IsCarryingItem || slotLayout == null) return;

        PickableItem item = playerPickup.ReleaseItemToCargo();
        if (item != null)
        {
            slotLayout.StoreItem(item, () =>
            {
                OnItemLoadedIntoCargo?.Invoke(item);
            });
        }
    }

    /// <summary>
    /// Remueve un ítem de la ranura de la van y lo entrega a las manos del jugador.
    /// </summary>
    public bool UnloadItemToPlayer(PickableItem item)
    {
        if (item == null || playerPickup == null || playerPickup.IsCarryingItem) return false;
        if (requireDoorOpen && vanDoorController != null && !vanDoorController.IsOpen) return false;

        lastUnloadTime = Time.time;

        if (slotLayout != null)
        {
            slotLayout.RemoveItem(item);
        }

        playerPickup.PickUp(item);
        OnItemUnloadedFromCargo?.Invoke(item);
        return true;
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

    // Compatibilidad adicional con BoxCollider Trigger si se prefiere en el editor
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = true;
            if (playerPickup == null) playerPickup = other.GetComponent<PlayerPickup>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Visualizar la zona de carga en la escena (Cyan/Azul)
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.7f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(zoneOffset, boxDimensions);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
