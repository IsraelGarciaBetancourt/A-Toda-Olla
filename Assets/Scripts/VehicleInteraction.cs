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

    void Start()
    {
        vehicleController = GetComponent<VehicleController>();
        
        vehicleController.isPlayerInside = false;
        if (vehicleCamera != null) vehicleCamera.SetActive(false);
        if (interactUI != null) interactUI.SetActive(false);
        
        // Si no asignaste la cámara del jugador, intentamos buscar la cámara principal
        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.gameObject;
        }
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
            interactAction.action.Disable();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (canInteract || playerIsInside)
        {
            if (playerIsInside)
                ExitVehicle();
            else
                EnterVehicle();
        }
    }

    private void EnterVehicle()
    {
        playerIsInside = true;
        vehicleController.isPlayerInside = true;

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
        playerIsInside = false;
        vehicleController.isPlayerInside = false;

        if (playerObject != null)
        {
            playerObject.SetActive(true);
            if (exitPoint != null)
            {
                playerObject.transform.position = exitPoint.position;
                playerObject.transform.rotation = exitPoint.rotation;
            }
            else
            {
                playerObject.transform.position = transform.position + transform.right * -2.5f;
            }
        }

        // Apagar la cámara del camión
        if (vehicleCamera != null)
        {
            vehicleCamera.SetActive(false);
        }

        // Encender la cámara del jugador
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
}
