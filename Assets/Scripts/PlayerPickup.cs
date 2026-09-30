using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class PlayerPickup : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Distancia máxima para detectar y recoger objetos del suelo.")]
    public float pickupRange = 2.8f;

    [Tooltip("Distancia máxima para alcanzar y sacar objetos dentro de la van.")]
    public float cargoPickupRange = 3.5f;

    [Tooltip("Capas (Layers) en las que se buscarán los objetos recogibles.")]
    public LayerMask itemLayerMask = ~0;

    [Header("Hold Settings")]
    [Tooltip("Punto donde se sostendrá el objeto (idealmente hijo de la cámara del jugador).")]
    public Transform holdPoint;

    [Tooltip("Velocidad de suavizado para llevar el objeto al HoldPoint al levantarlo.")]
    public float pickupSmoothSpeed = 12f;

    [Tooltip("Distancia hacia adelante frente al jugador donde se colocará el objeto en el suelo.")]
    public float dropForwardDistance = 1.1f;

    [Tooltip("Capas consideradas suelo para apoyar el objeto.")]
    public LayerMask groundLayerMask = ~0;

    [Header("Input Setup")]
    [Tooltip("Acción para Interactuar / Recoger (por defecto Tecla E).")]
    public InputActionReference interactAction;

    [Tooltip("Acción para Soltar en el suelo (por defecto Tecla G).")]
    public InputActionReference dropAction;

    [Header("Weight Effect")]
    [Tooltip("¿El peso del objeto cargado debe reducir la velocidad al caminar?")]
    public bool applyWeightPenaltyToPlayer = true;

    [Tooltip("Porcentaje de reducción de velocidad por cada 10kg cargados (0.1 = 10%).")]
    public float speedPenaltyPer10Kg = 0.08f;

    // Estado público
    public PickableItem CurrentItem { get; private set; }
    public PickableItem HoveredItem { get; private set; }
    public bool IsCarryingItem => CurrentItem != null;
    public float LastPickupTime { get; private set; } = -1f;

    // Referencias internas
    private PlayerController playerController;
    private Transform playerCameraTransform;
    private float originalWalkSpeed;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        if (playerController != null)
        {
            originalWalkSpeed = playerController.walkSpeed;
            if (playerController.playerCamera != null)
            {
                playerCameraTransform = playerController.playerCamera;
            }
        }

        // Si no se asignó la cámara manualmente, intentar encontrarla
        if (playerCameraTransform == null && Camera.main != null)
        {
            playerCameraTransform = Camera.main.transform;
        }

        // Si no se creó un holdPoint, lo creamos automáticamente hijo de la cámara
        if (holdPoint == null && playerCameraTransform != null)
        {
            GameObject autoHold = new GameObject("HoldPoint_Auto");
            autoHold.transform.SetParent(playerCameraTransform);
            autoHold.transform.localPosition = new Vector3(0f, -0.35f, 0.75f);
            autoHold.transform.localRotation = Quaternion.identity;
            holdPoint = autoHold.transform;
        }
    }

    void OnEnable()
    {
        if (interactAction != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += OnInteractPerformed;
        }

        if (dropAction != null)
        {
            dropAction.action.Enable();
            dropAction.action.performed += OnDropPerformed;
        }
    }

    void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.action.performed -= OnInteractPerformed;
        }

        if (dropAction != null)
        {
            dropAction.action.performed -= OnDropPerformed;
        }
    }

    void Update()
    {
        DetectPickableItem();
        UpdateHeldItemPosition();
        CheckKeyboardFallbacks();
    }

    private CargoZone cargoZone;
    private VanDoorController vanDoorController;

    /// <summary>
    /// Lanza un raycast desde el centro de la cámara para detectar objetos recogibles en la mira,
    /// incluyendo objetos almacenados dentro de la van si la puerta trasera está abierta.
    /// </summary>
    private void DetectPickableItem()
    {
        if (playerCameraTransform == null) return;

        if (cargoZone == null) cargoZone = Object.FindFirstObjectByType<CargoZone>();
        if (vanDoorController == null) vanDoorController = Object.FindFirstObjectByType<VanDoorController>();

        float maxRange = Mathf.Max(pickupRange, cargoPickupRange);
        Ray ray = new Ray(playerCameraTransform.position, playerCameraTransform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRange, itemLayerMask, QueryTriggerInteraction.Collide);

        if (hits != null && hits.Length > 0)
        {
            // Ordenar por distancia (el más cercano primero)
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                // 1. Collider sólido (no trigger)
                if (!hit.collider.isTrigger)
                {
                    PickableItem item = hit.collider.GetComponentInParent<PickableItem>();
                    if (item != null && !item.IsBeingCarried && !item.IsStoredInCargo)
                    {
                        if (hit.distance <= pickupRange)
                        {
                            HoveredItem = item;
                            return;
                        }
                    }

                    // Si chocamos contra una superficie sólida que NO es un PickableItem (pared, puerta cerrada, carrocería)
                    // bloquea la visión de lo que hay detrás.
                    if (item == null)
                    {
                        break;
                    }
                }
                // 2. Collider tipo Trigger (los ítems en la van son triggers)
                else
                {
                    PickableItem item = hit.collider.GetComponentInParent<PickableItem>();
                    if (item != null && !item.IsBeingCarried)
                    {
                        if (item.IsStoredInCargo)
                        {
                            // Solo se puede alcanzar si la puerta trasera está abierta
                            bool doorOpen = vanDoorController == null || vanDoorController.IsOpen;
                            if (doorOpen && hit.distance <= cargoPickupRange)
                            {
                                HoveredItem = item;
                                return;
                            }
                        }
                        else if (hit.distance <= pickupRange)
                        {
                            HoveredItem = item;
                            return;
                        }
                    }
                }
            }
        }

        HoveredItem = null;
    }

    /// <summary>
    /// Mantiene el objeto suavemente en la posición del HoldPoint.
    /// </summary>
    private void UpdateHeldItemPosition()
    {
        if (CurrentItem != null && holdPoint != null)
        {
            Vector3 targetLocalPos = CurrentItem.holdOffset;
            Quaternion targetLocalRot = Quaternion.Euler(CurrentItem.holdRotation);

            CurrentItem.transform.localPosition = Vector3.Lerp(
                CurrentItem.transform.localPosition,
                targetLocalPos,
                Time.deltaTime * pickupSmoothSpeed
            );

            CurrentItem.transform.localRotation = Quaternion.Slerp(
                CurrentItem.transform.localRotation,
                targetLocalRot,
                Time.deltaTime * pickupSmoothSpeed
            );
        }
    }

    /// <summary>
    /// Fallback para probar inmediatamente en el teclado si aún no se han vinculado InputActions en el Inspector.
    /// </summary>
    private void CheckKeyboardFallbacks()
    {
        if (Keyboard.current == null) return;

        // Si interactAction no está asignada, usar tecla E directamente
        if (interactAction == null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            HandleInteract();
        }

        // Si dropAction no está asignada, usar tecla G directamente
        if (dropAction == null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            DropItem();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        HandleInteract();
    }

    private void OnDropPerformed(InputAction.CallbackContext context)
    {
        DropItem();
    }

    private void HandleInteract()
    {
        // 1. Si no tenemos nada en brazos y estamos apuntando a un ítem
        if (CurrentItem == null && HoveredItem != null)
        {
            if (HoveredItem.IsStoredInCargo)
            {
                UnloadItemFromCargo(HoveredItem);
            }
            else
            {
                PickUp(HoveredItem);
            }
        }
    }

    /// <summary>
    /// Descarga un ítem almacenado en la van y lo entrega a las manos del jugador.
    /// </summary>
    public void UnloadItemFromCargo(PickableItem item)
    {
        if (item == null || CurrentItem != null) return;

        LastPickupTime = Time.time;

        if (cargoZone == null) cargoZone = Object.FindFirstObjectByType<CargoZone>();

        if (cargoZone != null)
        {
            cargoZone.UnloadItemToPlayer(item);
        }
        else
        {
            // Fallback directo si no hay CargoZone en la escena
            CargoSlotLayout slotLayout = item.GetComponentInParent<CargoSlotLayout>();
            if (slotLayout != null) slotLayout.RemoveItem(item);

            CargoManager cargoManager = item.GetComponentInParent<CargoManager>();
            if (cargoManager != null) cargoManager.UnregisterItem(item);

            PickUp(item);
        }
    }

    /// <summary>
    /// Recoge el ítem especificado y lo ata al holdPoint.
    /// </summary>
    public void PickUp(PickableItem item)
    {
        if (item == null) return;

        LastPickupTime = Time.time;
        CurrentItem = item;
        CurrentItem.PickUp(holdPoint);

        ApplyPlayerWeightPenalty();
    }

    /// <summary>
    /// Suelta el objeto y lo planta en seco directamente en el suelo frente al jugador, sin rebotes ni deslizamientos.
    /// </summary>
    public void DropItem()
    {
        if (CurrentItem == null) return;

        // Vector horizontal frente al jugador
        Vector3 forwardFlat = transform.forward;
        forwardFlat.y = 0f;
        forwardFlat.Normalize();

        Vector3 dropOrigin = transform.position + forwardFlat * dropForwardDistance + Vector3.up * 0.8f;
        RaycastHit hit;
        Vector3 plantPosition;

        // Buscamos la superficie exacta del suelo debajo
        if (Physics.Raycast(dropOrigin, Vector3.down, out hit, 3.5f, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            Collider col = CurrentItem.GetComponentInChildren<Collider>();
            float halfHeight = col != null ? col.bounds.extents.y : 0.25f;
            plantPosition = hit.point + Vector3.up * (halfHeight + 0.005f);
        }
        else
        {
            plantPosition = transform.position + forwardFlat * dropForwardDistance;
        }

        // Rotación recta y nivelada (sin inclinaciones raras) orientada con el jugador
        Quaternion plantRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        CurrentItem.Drop(plantPosition, plantRotation);
        CurrentItem = null;

        RestorePlayerSpeed();
    }

    /// <summary>
    /// Transfiere el objeto cargado hacia un receptor externo (como la zona de carga de la van).
    /// </summary>
    public PickableItem ReleaseItemToCargo()
    {
        if (CurrentItem == null) return null;

        PickableItem item = CurrentItem;
        CurrentItem = null;
        RestorePlayerSpeed();
        return item;
    }

    private void ApplyPlayerWeightPenalty()
    {
        if (!applyWeightPenaltyToPlayer || playerController == null || CurrentItem == null) return;

        float weightFactor = (CurrentItem.weightKg / 10f) * speedPenaltyPer10Kg;
        float penaltyMultiplier = Mathf.Clamp(1f - weightFactor, 0.4f, 1f);

        playerController.walkSpeed = originalWalkSpeed * penaltyMultiplier;
    }

    private void RestorePlayerSpeed()
    {
        if (playerController != null)
        {
            playerController.walkSpeed = originalWalkSpeed;
        }
    }

    /// <summary>
    /// Mensaje descriptivo contextual útil para la interfaz (HUD).
    /// </summary>
    public string GetInteractionPrompt()
    {
        if (CurrentItem != null)
        {
            return $"Cargando: {CurrentItem.itemName} ({CurrentItem.weightKg}kg)\n[G] Soltar en el suelo";
        }
        else if (HoveredItem != null)
        {
            if (HoveredItem.IsStoredInCargo)
            {
                return $"[E] Sacar {HoveredItem.itemName} ({HoveredItem.weightKg}kg)";
            }
            return $"[E] Recoger {HoveredItem.itemName} ({HoveredItem.weightKg}kg)";
        }

        return string.Empty;
    }

    private void OnDrawGizmosSelected()
    {
        if (playerCameraTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(playerCameraTransform.position, playerCameraTransform.forward * pickupRange);
        }
    }
}
