using UnityEngine;
using UnityEngine.InputSystem;

public class VehicleController : MonoBehaviour
{
    [Header("Vehicle Physics Settings")]
    public float acceleration = 30f;
    public float maxSpeed = 20f;
    public float turnSpeed = 150f; // Más giro (antes 100)
    public float coastingFriction = 0.5f; // Fricción al soltar el acelerador (suave)
    public float brakePower = 20f; // Freno activo
    public float grip = 5f; // Cuánto "agarre" tienen las llantas (evita que resbale como hielo)

    [Header("Camera Settings")]
    [Tooltip("Arrastra aquí la cámara del camión")]
    public Transform vehicleCamera;
    public float mouseSensitivity = 100f;
    public float cameraDistance = 7f; // Distancia hacia atrás
    public float cameraHeight = 2.5f;   // Altura de la cámara
    public float cameraResetDelay = 1.5f; // Tiempo antes de auto-centrar (cooldown)

    [Header("Input Setup")]
    public InputActionReference moveAction;
    [Tooltip("Asigna la acción 'Look' del InputSystem_Actions (Player/Look).")]
    public InputActionReference lookAction;

    [Header("Wheel Visuals")]
    public float wheelRadius = 0.4f;
    public float maxSteerAngle = 30f;
    private Transform[] frontWheels = new Transform[2];
    private Transform[] backWheels = new Transform[2];
    private float currentWheelRotation = 0f;

    [Header("State")]
    public bool isPlayerInside = false;

    private Rigidbody rb;
    private Vector2 moveInput;
    private Vector2 lookInput;
    
    // Variables para la cámara
    private float cameraXRotation = 0f;
    private float cameraYRotation = 0f;
    private float cameraResetTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Bajamos el centro de masa para que el camión no se vuelque fácilmente en las curvas
            rb.centerOfMass = new Vector3(0, -0.5f, 0); 
            
            // IMPORTANTE: Interpolación para que el auto se mueva fluidamente en pantalla
            // y no tiemble/vibre cuando la cámara (que es suave) lo persiga
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        // Si tenemos cámara, la preparamos para el seguimiento suave
        if (vehicleCamera != null)
        {
            cameraYRotation = transform.eulerAngles.y;
            cameraXRotation = 15f;
            
            // IMPORTANTE: Desvinculamos la cámara del camión para que se mueva en el mundo
            // y no herede directamente las rotaciones bruscas ni la física (evita el movimiento "robótico")
            if (vehicleCamera.parent == transform)
            {
                vehicleCamera.parent = null;
            }
        }

        frontWheels[0] = FindChildRecursive(transform, "wheel-front-left");
        frontWheels[1] = FindChildRecursive(transform, "wheel-front-right");
        backWheels[0] = FindChildRecursive(transform, "wheel-back-left");
        backWheels[1] = FindChildRecursive(transform, "wheel-back-right");
    }

    void Update()
    {
        if (isPlayerInside)
        {
            // Asegurarnos de que las acciones están encendidas
            if (moveAction != null && !moveAction.action.enabled) moveAction.action.Enable();
            if (lookAction != null && !lookAction.action.enabled) lookAction.action.Enable();

            ReadInput();
        }

        UpdateWheelVisuals();
    }

    void LateUpdate()
    {
        // Movimiento de cámara en LateUpdate para que siga al auto DESPUÉS de las físicas
        if (isPlayerInside)
        {
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
            cameraXRotation = Mathf.Clamp(cameraXRotation, -15f, 60f);

            // Izquierda y derecha (ángulo mundial, no anclado a la rotación del auto)
            cameraYRotation += mouseX;

            // Manejar el temporizador (cooldown) del auto-centrado
            if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
            {
                cameraResetTimer = cameraResetDelay; // Si se mueve la cámara, reiniciamos el tiempo
            }
            else
            {
                cameraResetTimer -= Time.deltaTime; // Si soltamos el mouse, el tiempo empieza a bajar
            }

            // Auto-centrar la cámara hacia donde apunta el auto si avanzamos y el cooldown terminó
            float forwardSpeed = rb != null ? Vector3.Dot(transform.forward, rb.linearVelocity) : 0f;
            if (cameraResetTimer <= 0f && forwardSpeed > 2f)
            {
                float targetYAngle = transform.eulerAngles.y;
                cameraYRotation = Mathf.LerpAngle(cameraYRotation, targetYAngle, Time.deltaTime * 3f);
            }

            // Calcular la rotación y posición deseadas
            Quaternion desiredRotation = Quaternion.Euler(cameraXRotation, cameraYRotation, 0f);
            
            // El pivote es el centro del carro más la altura
            Vector3 pivot = transform.position + new Vector3(0, cameraHeight, 0);
            // La posición final es el pivote más la distancia hacia atrás
            Vector3 targetPosition = pivot + (desiredRotation * new Vector3(0, 0, -cameraDistance));

            // Suavizado suave (Lerp) para evitar tirones y movimiento "en seco"
            vehicleCamera.position = Vector3.Lerp(vehicleCamera.position, targetPosition, Time.deltaTime * 15f);
            vehicleCamera.rotation = Quaternion.Slerp(vehicleCamera.rotation, desiredRotation, Time.deltaTime * 15f);
        }
    }

    private void DrivePhysics()
    {
        float verticalInput = moveInput.y;
        float horizontalInput = moveInput.x;

        // Saber a qué velocidad vamos hacia adelante o atrás
        float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
        
        // --- 1. ACELERACIÓN Y FRENADO ---
        if (Mathf.Abs(verticalInput) > 0.1f)
        {
            // Detectar si estamos intentando frenar (moviéndonos adelante y presionando atrás, o viceversa)
            bool isBraking = (forwardSpeed > 1f && verticalInput < -0.1f) || (forwardSpeed < -1f && verticalInput > 0.1f);

            if (isBraking)
            {
                // Freno activo: se aplica una fuerza contraria a la velocidad actual
                Vector3 brakeForce = -rb.linearVelocity.normalized * brakePower;
                brakeForce.y = 0;
                rb.AddForce(brakeForce, ForceMode.Acceleration);
            }
            else if (rb.linearVelocity.magnitude < maxSpeed)
            {
                // Aceleración
                rb.AddForce(transform.forward * verticalInput * acceleration, ForceMode.Acceleration);
            }
        }
        else
        {
            // --- 2. FRICCIÓN NATURAL (COASTING) ---
            // Si soltamos las teclas, el auto rueda y se detiene suavemente
            Vector3 frictionForce = -rb.linearVelocity * coastingFriction;
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
        Vector3 rightVelocity = transform.right * Vector3.Dot(rb.linearVelocity, transform.right);
        rb.linearVelocity = rb.linearVelocity - (rightVelocity * Time.fixedDeltaTime * grip);
    }
    
    // Función para resetear la cámara al subir al camión
    public void ResetCamera()
    {
        cameraXRotation = 15f;
        cameraYRotation = transform.eulerAngles.y;
    }

    private void UpdateWheelVisuals()
    {
        if (rb == null) return;

        float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
        
        // Calculamos cuánto deben rotar las ruedas basado en la distancia recorrida
        // Velocidad * Tiempo = Distancia. Rotación = (Distancia / Radio) * Rad2Deg
        float rotationAngle = (forwardSpeed * Time.deltaTime) / wheelRadius * Mathf.Rad2Deg;
        currentWheelRotation += rotationAngle;
        currentWheelRotation %= 360f;

        // Solo aplicamos giro si el jugador está manejando
        float horizontalInput = isPlayerInside ? moveInput.x : 0f;
        float steerAngle = horizontalInput * maxSteerAngle;

        foreach (Transform wheel in frontWheels)
        {
            if (wheel != null)
            {
                // En Unity, normalmente X es para rodar adelante/atrás y Y es para girar izq/der
                wheel.localRotation = Quaternion.Euler(currentWheelRotation, steerAngle, 0f);
            }
        }
        
        foreach (Transform wheel in backWheels)
        {
            if (wheel != null)
            {
                wheel.localRotation = Quaternion.Euler(currentWheelRotation, 0f, 0f);
            }
        }
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform result = FindChildRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
