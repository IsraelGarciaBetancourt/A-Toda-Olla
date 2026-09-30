using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Velocidad normal de caminata.")]
    public float walkSpeed = 5f;

    [Tooltip("Fuerza de gravedad aplicada al jugador.")]
    public float gravity = -18f;

    [Header("Jump Settings")]
    [Tooltip("Altura del salto en metros (suficiente para subir al piso de la van).")]
    public float jumpHeight = 1.3f;

    [Tooltip("Acción de salto (Input Action Player/Jump o barra espaciadora).")]
    public InputActionReference jumpAction;

    [Tooltip("Capas consideradas suelo para saltar.")]
    public LayerMask groundLayerMask = ~0;
    
    [Header("Look Settings (1ra Persona)")]
    public float mouseSensitivity = 100f;
    [Tooltip("Arrastra aquí la cámara que acabas de crear como hija del Player")]
    public Transform playerCamera;

    [Header("Input Setup")]
    public InputActionReference moveAction;
    [Tooltip("Asigna la acción 'Look' del InputSystem_Actions (Player/Look).")]
    public InputActionReference lookAction;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float xRotation = 0f;

    // Buffer de salto y coyote time para máxima respuesta
    private float jumpBufferTime = 0.2f;
    private float jumpBufferCounter = 0f;
    private float coyoteTime = 0.15f;
    private float coyoteTimeCounter = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        
        // Bloquear el cursor en el centro de la pantalla para el juego en 1ra persona
        Cursor.lockState = CursorLockMode.Locked;

        // Si hay un CapsuleCollider duplicado en el mismo GameObject, deshabilitarlo
        // para que no interfiera con el CharacterController
        CapsuleCollider extraCol = GetComponent<CapsuleCollider>();
        if (extraCol != null && extraCol.enabled)
        {
            Debug.Log("[PlayerController] Desactivando CapsuleCollider adicional para evitar interferencias con CharacterController.");
            extraCol.enabled = false;
        }
    }

    void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (lookAction != null) lookAction.action.Enable();
        if (jumpAction != null) jumpAction.action.Enable();
    }

    void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (lookAction != null) lookAction.action.Disable();
        if (jumpAction != null) jumpAction.action.Disable();
    }

    void Update()
    {
        HandleMouseLook();
        HandleJumpInput();
        HandleMovement();
    }

    private void HandleJumpInput()
    {
        // Detectar si se presionó el botón de salto por InputAction O por teclado directo (Espacio)
        bool jumpPressed = (jumpAction != null && jumpAction.action.WasPressedThisFrame())
                        || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);

        if (jumpPressed)
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    private void HandleMouseLook()
    {
        if (lookAction != null && playerCamera != null)
        {
            lookInput = lookAction.action.ReadValue<Vector2>();

            float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
            float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

            // Rotación vertical (arriba/abajo) se aplica a la cámara
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

            // Rotación horizontal (izquierda/derecha) se aplica a todo el cuerpo del jugador
            transform.Rotate(Vector3.up * mouseX);
        }
    }

    private void HandleMovement()
    {
        if (moveAction != null)
        {
            moveInput = moveAction.action.ReadValue<Vector2>();
        }

        // Comprobación de suelo doble: CharacterController.isGrounded + Raycast de seguridad
        bool isGrounded = controller.isGrounded || CheckGroundedFallback();

        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
            if (velocity.y < 0f)
            {
                velocity.y = -2f; // Ligera presión hacia abajo para mantener contacto
            }
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // Ejecutar salto si el jugador está en el suelo (o en coyote time) y se presionó saltar
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }

        // Aplicar gravedad continua
        velocity.y += gravity * Time.deltaTime;

        // Movimiento horizontal y vertical unificado en una sola llamada a controller.Move
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        Vector3 totalMovement = (move * walkSpeed) + velocity;
        controller.Move(totalMovement * Time.deltaTime);
    }

    /// <summary>
    /// Comprobación de respaldo para saber si estamos en el suelo cuando el CharacterController
    /// tiene dudas en desniveles, bordes de la van o colisionadores complejos.
    /// </summary>
    private bool CheckGroundedFallback()
    {
        if (controller == null) return false;

        Vector3 bottom = transform.position + controller.center - Vector3.up * (controller.height * 0.5f - controller.radius);
        return Physics.SphereCast(bottom, controller.radius * 0.85f, Vector3.down, out _, 0.18f, groundLayerMask, QueryTriggerInteraction.Ignore);
    }
}
