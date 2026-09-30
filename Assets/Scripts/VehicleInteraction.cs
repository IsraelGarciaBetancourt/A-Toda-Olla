using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(VehicleController))]
public class VehicleInteraction : MonoBehaviour
{
    [Header("Input Setup")]
    public InputActionReference interactAction;

    [Header("References")]
    public GameObject playerObject;
    
    [Tooltip("La cámara principal del jugador. Arrástrala aquí para apagarla al subir.")]
    public GameObject playerCamera; // NUEVO: Referencia a la cámara del jugador

    public GameObject vehicleCamera;
    public Transform exitPoint;

    [Header("UI (Opcional)")]
    public GameObject interactUI;

    private VehicleController vehicleController;
    private bool canInteract = false;
    private bool playerIsInside = false;

    public bool CanInteract => canInteract;
    public bool PlayerIsInside => playerIsInside;

    void Start()
    {
        vehicleController = GetComponent<VehicleController>();
        
        vehicleController.isPlayerInside = false;
        if (vehicleCamera != null) vehicleCamera.SetActive(false);
        if (interactUI != null) interactUI.SetActive(false);
        
        // Si no asignaste la cámara del jugador, intentamos buscarla
        if (playerCamera == null)
        {
            // 1. Intentamos con la cámara principal
            if (Camera.main != null)
            {
                playerCamera = Camera.main.gameObject;
            }
            // 2. Si no hay cámara principal, la buscamos dentro del jugador
            else if (playerObject != null)
            {
                Camera cam = playerObject.GetComponentInChildren<Camera>(true);
                if (cam != null)
                {
                    playerCamera = cam.gameObject;
                }
            }
        }
    }

    [Header("Detection Zone (Puerta del Conductor)")]
    [Tooltip("¿Usar zona de caja matemática local para evitar activaciones desde dentro de la van?")]
    public bool useZoneBox = true;

    [Tooltip("Desplazamiento local respecto a la van. X = -1.1 para la puerta izquierda del conductor.")]
    public Vector3 zoneOffset = new Vector3(-1.1f, 0.7f, 0.6f);

    [Tooltip("Dimensiones de la caja (Ancho X, Alto Y, Profundidad Z).")]
    public Vector3 boxDimensions = new Vector3(1.0f, 1.6f, 1.2f);

    [Header("Distancia de Seguridad")]
    [Tooltip("Distancia máxima permitida para interactuar con el vehículo.")]
    public float maxInteractionDistance = 4.5f;

    private float interactionCooldown = 0.4f;
    private float lastInteractionTime = -1f;

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
            interactAction.action.Disable();
        }
    }

    void Update()
    {
        // 1. Si el jugador está dentro conduciendo, asegurar que la acción interactuar permanezca activa
        if (playerIsInside && interactAction != null && !interactAction.action.enabled)
        {
            interactAction.action.Enable();
        }

        // 2. Comprobación de proximidad a la puerta del conductor
        if (!playerIsInside)
        {
            if (useZoneBox)
            {
                CheckPlayerProximity();
            }
            else if (canInteract && playerObject != null)
            {
                float dist = Vector3.Distance(transform.position, playerObject.transform.position);
                if (dist > maxInteractionDistance)
                {
                    canInteract = false;
                    if (interactUI != null) interactUI.SetActive(false);
                }
            }
        }
    }

    private void CheckPlayerProximity()
    {
        if (playerObject == null || !playerObject.activeInHierarchy)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerObject = p;
            else return;
        }

        Vector3 playerLocalPos = transform.InverseTransformPoint(playerObject.transform.position);
        Vector3 diff = playerLocalPos - zoneOffset;

        bool inBox = Mathf.Abs(diff.x) <= boxDimensions.x * 0.5f &&
                     Mathf.Abs(diff.y) <= boxDimensions.y * 0.5f &&
                     Mathf.Abs(diff.z) <= boxDimensions.z * 0.5f;

        canInteract = inBox;
        if (interactUI != null) interactUI.SetActive(canInteract);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        TryInteract();
    }

    private void TryInteract()
    {
        if (Time.time - lastInteractionTime < interactionCooldown) return;

        if (playerIsInside)
        {
            lastInteractionTime = Time.time;
            ExitVehicle();
        }
        else if (canInteract)
        {
            // Verificación extra de distancia antes de permitir entrar
            if (playerObject != null && Vector3.Distance(transform.position, playerObject.transform.position) > maxInteractionDistance)
            {
                canInteract = false;
                if (interactUI != null) interactUI.SetActive(false);
                return;
            }

            // Si tiene manos ocupadas, no puede entrar a conducir
            PlayerPickup pickup = playerObject != null ? playerObject.GetComponent<PlayerPickup>() : null;
            if (pickup != null && pickup.IsCarryingItem)
            {
                return;
            }

            lastInteractionTime = Time.time;
            EnterVehicle();
        }
    }

    private void EnterVehicle()
    {
        canInteract = false;
        playerIsInside = true;
        vehicleController.isPlayerInside = true;
        
        // Preparar la cámara para que transicione suavemente desde la vista del jugador
        if (playerCamera != null)
        {
            vehicleController.SetupCameraTransition(playerCamera.transform);
        }
        else if (playerObject != null)
        {
            // Fallback: usar la posición del jugador si por alguna razón no se encontró su cámara
            vehicleController.SetupCameraTransition(playerObject.transform);
        }
        else
        {
            vehicleController.ResetCamera();
        }

        if (playerObject != null)
        {
            playerObject.SetActive(false);
        }

        // Apagar la cámara del jugador
        if (playerCamera != null)
        {
            playerCamera.SetActive(false);
        }

        // Encender la cámara del camión
        if (vehicleCamera != null)
        {
            vehicleCamera.SetActive(true);
        }
        
        if (interactUI != null) interactUI.SetActive(false);
    }

    private void ExitVehicle()
    {
        canInteract = false;
        if (interactUI != null) interactUI.SetActive(false);

        playerIsInside = false;
        vehicleController.isPlayerInside = false;

        if (playerObject != null)
        {
            // IMPORTANT: Apagar el CharacterController antes de mover al jugador
            // para que el motor de físicas no bloquee el teletransporte
            CharacterController cc = playerObject.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            if (exitPoint != null)
            {
                playerObject.transform.position = exitPoint.position;
                playerObject.transform.rotation = exitPoint.rotation;
            }
            else
            {
                playerObject.transform.position = transform.position + transform.right * -2.5f;
            }

            // Encender el CharacterController de nuevo
            if (cc != null) cc.enabled = true;
            
            playerObject.SetActive(true);
        }

        StartCoroutine(ExitCameraTransitionCoroutine());
    }

    private System.Collections.IEnumerator ExitCameraTransitionCoroutine()
    {
        // 1. Iniciamos la transición de la cámara del camión hacia el jugador
        if (playerCamera != null)
        {
            vehicleController.SetupExitCameraTransition(playerCamera.transform);
        }
        else if (playerObject != null)
        {
            vehicleController.SetupExitCameraTransition(playerObject.transform);
        }

        // 2. Esperamos el tiempo exacto que dura la animación (ahora editable desde el Inspector)
        yield return new WaitForSeconds(vehicleController.cameraTransitionDuration);

        // 3. Apagamos la cámara del camión y devolvemos la del jugador
        if (vehicleCamera != null)
        {
            vehicleCamera.SetActive(false);
        }

        if (playerCamera != null)
        {
            playerCamera.SetActive(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !playerIsInside)
        {
            canInteract = true;
            if (interactUI != null) interactUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canInteract = false;
            if (interactUI != null) interactUI.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (useZoneBox)
        {
            Gizmos.color = canInteract ? Color.green : new Color(1f, 0.9f, 0.1f, 0.7f); // Amarillo / Verde
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(zoneOffset, boxDimensions);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
