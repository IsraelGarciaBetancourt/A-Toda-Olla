using UnityEngine;
using UnityEngine.InputSystem;

public class VehicleController : MonoBehaviour
{
    [Header("Vehicle Physics Settings")]
    public float acceleration = 30f;
    public float maxSpeed = 20f;
    public float turnSpeed = 100f;
    public float brakingFriction = 3f; // Fricción al soltar el acelerador
    public float grip = 5f; // Cuánto "agarre" tienen las llantas (evita que resbale como hielo)

    [Header("Camera Settings")]
    [Tooltip("Arrastra aquí la cámara del camión")]
    public Transform vehicleCamera;
    public float mouseSensitivity = 100f;

    [Header("Input Setup")]
    public InputActionReference moveAction;
    [Tooltip("Asigna la acción 'Look' del InputSystem_Actions (Player/Look).")]
    public InputActionReference lookAction;

    [Header("State")]
    public bool isPlayerInside = false;

    private Rigidbody rb;
    private Vector2 moveInput;
    private Vector2 lookInput;
    
    // Variables para la cámara
    private float cameraXRotation = 0f;
    private float cameraYRotation = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Bajamos el centro de masa para que el camión no se vuelque fácilmente en las curvas
            rb.centerOfMass = new Vector3(0, -0.5f, 0); 
        }

        // Si tenemos cámara, guardamos su rotación inicial
        if (vehicleCamera != null)
        {
            cameraYRotation = vehicleCamera.localEulerAngles.y;
            cameraXRotation = vehicleCamera.localEulerAngles.x;
        }
    }

    void Update()
    {
        if (isPlayerInside)
        {
            // Asegurarnos de que las acciones están encendidas
            if (moveAction != null && !moveAction.action.enabled) moveAction.action.Enable();
            if (lookAction != null && !lookAction.action.enabled) lookAction.action.Enable();

            ReadInput();
            HandleCameraLook();
        }
    }

    void FixedUpdate()
    {
        if (isPlayerInside && rb != null)
        {
            DrivePhysics();
        }
    }

    private void ReadInput()
    {
        moveInput = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        lookInput = lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;
    }

    private void HandleCameraLook()
    {
        if (vehicleCamera != null)
        {
            float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
            float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

            // Arriba y abajo
            cameraXRotation -= mouseY;
            cameraXRotation = Mathf.Clamp(cameraXRotation, -60f, 60f);

            // Izquierda y derecha (limitado para no romper el cuello mirando hacia atrás)
            cameraYRotation += mouseX;
            cameraYRotation = Mathf.Clamp(cameraYRotation, -110f, 110f); 

            vehicleCamera.localRotation = Quaternion.Euler(cameraXRotation, cameraYRotation, 0f);
        }
    }

    private void DrivePhysics()
    {
        float verticalInput = moveInput.y;
        float horizontalInput = moveInput.x;

        // Saber a qué velocidad vamos hacia adelante o atrás
        float forwardSpeed = Vector3.Dot(transform.forward, rb.velocity);
        
        // --- 1. ACELERACIÓN ---
        if (Mathf.Abs(verticalInput) > 0.1f)
        {
            // Si no hemos superado la velocidad máxima
            if (rb.velocity.magnitude < maxSpeed)
            {
                rb.AddForce(transform.forward * verticalInput * acceleration, ForceMode.Acceleration);
            }
        }
        else
        {
            // --- 2. FRENADO/FRICCIÓN AUTOMÁTICA ---
            // Si soltamos las teclas, el camión se detiene gradualmente
            Vector3 frictionForce = -rb.velocity * brakingFriction;
            frictionForce.y = 0; // No afectar la gravedad
            rb.AddForce(frictionForce, ForceMode.Acceleration);
        }

        // --- 3. GIRO (DIRECCIÓN) ---
        // Solo podemos girar si el camión se está moviendo
        if (Mathf.Abs(forwardSpeed) > 0.5f)
        {
            // Invertimos los controles si vamos en reversa para que se sienta natural
            float steerDirection = forwardSpeed > 0 ? 1f : -1f;
            float turnAmount = horizontalInput * turnSpeed * steerDirection * Time.fixedDeltaTime;
            
            Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
        
        // --- 4. AGARRE (EVITAR DERRAPE EXCESIVO) ---
        // Eliminamos parte de la velocidad lateral (drifting) para que se sienta como un vehículo real
        Vector3 rightVelocity = transform.right * Vector3.Dot(rb.velocity, transform.right);
        rb.velocity = rb.velocity - (rightVelocity * Time.fixedDeltaTime * grip);
    }
    
    // Función para resetear la cámara al subir al camión
    public void ResetCamera()
    {
        cameraXRotation = 0f;
        cameraYRotation = 0f;
    }
}
