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

    [Header("Step & Seam Assist (Superar juntas de pistas)")]
    [Tooltip("Permite superar desniveles microscópicos, baldosas y juntas sin frenar en seco.")]
    public bool enableStepAssist = true;

    [Tooltip("Altura máxima del desnivel o borde que el auto puede absorber suavemente (en metros). 0.35 cubre veredas urbanas estándar.")]
    public float maxStepHeight = 0.35f;

    [Tooltip("Fuerza hacia abajo (Downforce) para mantener el auto pegado al asfalto.")]
    public float downforce = 15f;

    [Tooltip("Capas consideradas suelo / pista.")]
    public LayerMask roadLayerMask = ~0;

    [Header("Bevel Colliders (Bordes Redondeados)")]
    [Tooltip("Agrega esferas en las 4 esquinas inferiores para que el vehículo suba sobre veredas en lugar de chocarse.")]
    public bool enableBevelColliders = true;

    [Tooltip("Radio de los colliders esféricos en las esquinas. Ajusta si el vehículo se entierra o flota.")]
    public float bevelRadius = 0.18f;

    [Header("Camera Settings")]
    [Tooltip("Arrastra aquí la cámara del camión")]
    public Transform vehicleCamera;
    public float mouseSensitivity = 100f;
    public float cameraDistance = 7f; // Distancia hacia atrás
    public float cameraHeight = 2.5f;   // Altura de la cámara
    public float cameraResetDelay = 1.5f; // Tiempo antes de auto-centrar (cooldown)
    public float cameraTransitionDuration = 1f; // Duración de la animación de cámara al entrar/salir

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
    private float cameraTransitionTimer = 0f;
    
    // Variables para la interpolación precisa
    private Vector3 transitionStartPosition;
    private Quaternion transitionStartRotation;
    private bool isExiting = false;
    private Transform exitTargetCamera;

    // Dimensiones calculadas del vehículo para Step Assist
    private float vehicleBottomY = 0f;
    private float vehicleFrontZ = 1.8f;
    private float vehicleRearZ = -1.8f;
    private float vehicleHalfWidth = 0.8f;
    private bool boundsCalculated = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Si la masa quedó en el valor por defecto de 1kg, la ajustamos a una masa real de camioneta (1200kg)
            if (rb.mass <= 10f)
            {
                rb.mass = 1200f;
            }

            // Bajamos el centro de masa para que el camión no se vuelque fácilmente en las curvas
            rb.centerOfMass = new Vector3(0, -0.8f, 0); 
            
            // IMPORTANTE: Interpolación para que el auto se mueva fluidamente en pantalla
            // y no tiemble/vibre cuando la cámara (que es suave) lo persiga
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Detección continua para evitar atravesar o engancharse con bordes de colliders
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        // Asignar PhysicMaterial sin fricción en los colliders para deslizarse sin trabarse en juntas de pista
        CalculateVehicleBounds();
        ApplyFrictionlessMaterial();

        // Agregar colliders esféricos en las esquinas inferiores del vehículo
        // para que suba suavemente sobre veredas (efecto "bisel" o borde redondeado)
        if (enableBevelColliders)
        {
            AddBevelColliders();
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
        // También lo llamamos si está saliendo para hacer la transición
        if (isPlayerInside || isExiting)
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
            if (isExiting)
            {
                // Lógica de transición de salida (de la vista del camión hacia el jugador)
                if (cameraTransitionTimer > 0f)
                {
                    cameraTransitionTimer -= Time.deltaTime;
                    // Progresión de 0 a 1 basada en la duración total
                    float t = 1f - Mathf.Clamp01(cameraTransitionTimer / cameraTransitionDuration);
                    t = Mathf.SmoothStep(0f, 1f, t);

                    if (exitTargetCamera != null)
                    {
                        vehicleCamera.position = Vector3.Lerp(transitionStartPosition, exitTargetCamera.position, t);
                        vehicleCamera.rotation = Quaternion.Slerp(transitionStartRotation, exitTargetCamera.rotation, t);
                    }
                }
                else
                {
                    isExiting = false;
                }
                return; // Evita calcular el resto de rotaciones del vehículo
            }

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

            // Realizar una transición basada en tiempo (SmoothStep) en lugar de un Lerp asintótico
            if (cameraTransitionTimer > 0f)
            {
                cameraTransitionTimer -= Time.deltaTime;
                
                // Calculamos el progreso de 0 a 1 basado en la duración total configurada
                float t = 1f - Mathf.Clamp01(cameraTransitionTimer / cameraTransitionDuration); 
                t = Mathf.SmoothStep(0f, 1f, t); // Le damos suavidad al inicio y al final de la animación

                // Movemos la cámara explícitamente desde donde estaba el jugador hacia el camión
                vehicleCamera.position = Vector3.Lerp(transitionStartPosition, targetPosition, t);
                vehicleCamera.rotation = Quaternion.Slerp(transitionStartRotation, desiredRotation, t);
            }
           else
            {
                // Suavizado normal para seguir el vehículo una vez terminada la transición
                vehicleCamera.position = Vector3.Lerp(vehicleCamera.position, targetPosition, Time.deltaTime * 15f);
                vehicleCamera.rotation = Quaternion.Slerp(vehicleCamera.rotation, desiredRotation, Time.deltaTime * 15f);
            } 
        }
    }

    private void DrivePhysics()
    {
        float verticalInput = moveInput.y;
        float horizontalInput = moveInput.x;

        // Saber a qué velocidad vamos hacia adelante o atrás
        float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
        
        // --- 0. ASISTENCIA DE ESCALONES Y JUNTAS DE BALDOSAS/PISTA ---
        HandleStepAssist(verticalInput, forwardSpeed);

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

            // BUG FIX: Rotar SOLO en el eje Y del mundo, ignorando pitch/roll del Rigidbody.
            // rb.rotation * Euler(Y) heredaba inclinaciones de rampas y juntas → descontrol.
            float currentYaw = rb.rotation.eulerAngles.y;
            Quaternion newRotation = Quaternion.Euler(0f, currentYaw + turnAmount, 0f);
            rb.MoveRotation(newRotation);
        }
        
        // --- 4. AGARRE (EVITAR DERRAPE EXCESIVO) ---
        // BUG FIX: Aplicar el agarre como fuerza (AddForce) en lugar de mutar linearVelocity
        // directamente, lo que causaba tirones bruscos a altas velocidades.
        Vector3 rightVelocity = transform.right * Vector3.Dot(rb.linearVelocity, transform.right);
        rb.AddForce(-rightVelocity * grip, ForceMode.Acceleration);

        // --- 5. DOWNFORCE (MANTENER PEGADO AL ASFALTO) ---
        // BUG FIX: Usar -Vector3.up (mundo) en vez de -transform.up (local).
        // Con transform.up inclinado, la downforce empujaba el auto lateralmente → descontrol.
        if (downforce > 0f)
        {
            rb.AddForce(-Vector3.up * downforce, ForceMode.Acceleration);
        }
    }
    
    // Función para resetear la cámara al subir al camión
    public void ResetCamera()
    {
        cameraXRotation = 15f;
        cameraYRotation = transform.eulerAngles.y;
        // BUG FIX: Reiniciar el cooldown para que la cámara no se auto-centre
        // inmediatamente al entrar al vehículo.
        cameraResetTimer = cameraResetDelay;
    }

    // Configura la cámara para que inicie la transición desde la vista del jugador
    public void SetupCameraTransition(Transform playerCameraTransform)
    {
        ResetCamera();
        isExiting = false;
        if (vehicleCamera != null && playerCameraTransform != null)
        {
            vehicleCamera.position = playerCameraTransform.position;
            vehicleCamera.rotation = playerCameraTransform.rotation;
            
            // Guardamos el punto exacto de donde iniciará el "viaje" la cámara
            transitionStartPosition = vehicleCamera.position;
            transitionStartRotation = vehicleCamera.rotation;
        }
        cameraTransitionTimer = cameraTransitionDuration;
    }

    // Configura la cámara para que inicie la transición de regreso al jugador al salir
    public void SetupExitCameraTransition(Transform targetCameraTransform)
    {
        if (vehicleCamera != null && targetCameraTransform != null)
        {
            transitionStartPosition = vehicleCamera.position;
            transitionStartRotation = vehicleCamera.rotation;
            exitTargetCamera = targetCameraTransform;
            isExiting = true;
            cameraTransitionTimer = cameraTransitionDuration;
        }
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

    private void ApplyFrictionlessMaterial()
    {
        // BUG FIX: Fricción 0 absoluta hacía que el vehículo resbalara sin control
        // en superficies inclinadas. Valores muy bajos (0.05) suavizan el paso por
        // juntas sin eliminar toda la fricción.
        PhysicsMaterial seamSlipMat = new PhysicsMaterial("VehicleSeamSlip")
        {
            dynamicFriction = 0.05f,
            staticFriction = 0.05f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        Collider[] cols = GetComponentsInChildren<Collider>();
        foreach (Collider col in cols)
        {
            if (!col.isTrigger && col.sharedMaterial == null)
            {
                col.sharedMaterial = seamSlipMat;
            }
        }
    }

    /// <summary>
    /// Crea 4 SphereColliders en las esquinas inferiores del vehículo (delantera-izq,
    /// delantera-der, trasera-izq, trasera-der). Al ser esferas, el motor de físicas
    /// de Unity las desliza automáticamente hacia arriba cuando topan con un borde
    /// pequeño, en lugar de bloquearse como lo haría la cara plana de un BoxCollider.
    /// </summary>
    private void AddBevelColliders()
    {
        if (!boundsCalculated) CalculateVehicleBounds();

        // Material resbaloso para los bevel colliders (mismo que el cuerpo)
        PhysicsMaterial bevelMat = new PhysicsMaterial("BevelSlip")
        {
            dynamicFriction = 0.0f,
            staticFriction  = 0.0f,
            bounciness      = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine   = PhysicsMaterialCombine.Minimum
        };

        // Las 4 esquinas inferiores en espacio local del vehículo.
        // Desplazamos las esferas hacia adentro del cuerpo en X para que no asomen
        // por los lados del mesh. El Y se pone en el fondo + radio para que queden
        // justo en la línea de tierra.
        float cornerX  = vehicleHalfWidth - bevelRadius * 0.5f;
        float cornerY  = vehicleBottomY   + bevelRadius;   // centros a altura de radio
        float cornerFZ = vehicleFrontZ    - bevelRadius * 0.5f;
        float cornerRZ = vehicleRearZ     + bevelRadius * 0.5f;

        Vector3[] corners = new Vector3[]
        {
            new Vector3(-cornerX,  cornerY,  cornerFZ),  // frente-izquierda
            new Vector3( cornerX,  cornerY,  cornerFZ),  // frente-derecha
            new Vector3(-cornerX,  cornerY,  cornerRZ),  // atrás-izquierda
            new Vector3( cornerX,  cornerY,  cornerRZ),  // atrás-derecha
        };

        string[] cornerNames = { "Bevel_FL", "Bevel_FR", "Bevel_RL", "Bevel_RR" };

        for (int i = 0; i < corners.Length; i++)
        {
            // Reutilizar un hijo existente si ya fue creado (re-entrada en Play Mode)
            Transform existing = transform.Find(cornerNames[i]);
            GameObject bevelGO = existing != null ? existing.gameObject : new GameObject(cornerNames[i]);

            bevelGO.transform.SetParent(transform, false);
            bevelGO.transform.localPosition = corners[i];
            bevelGO.transform.localRotation = Quaternion.identity;
            bevelGO.layer = gameObject.layer;

            SphereCollider sc = bevelGO.GetComponent<SphereCollider>();
            if (sc == null) sc = bevelGO.AddComponent<SphereCollider>();
            sc.radius         = bevelRadius;
            sc.center         = Vector3.zero;
            sc.sharedMaterial = bevelMat;
        }
    }

    private void CalculateVehicleBounds()
    {
        BoxCollider[] boxes = GetComponentsInChildren<BoxCollider>();
        bool foundAny = false;
        float minY = float.MaxValue;
        float maxZ = float.MinValue;
        float minZ = float.MaxValue;
        float maxX = float.MinValue;

        foreach (BoxCollider box in boxes)
        {
            if (box.isTrigger) continue;

            Vector3 center = box.center;
            Vector3 size = box.size;

            Vector3[] corners = new Vector3[8]
            {
                center + new Vector3(-size.x, -size.y, -size.z) * 0.5f,
                center + new Vector3(-size.x, -size.y,  size.z) * 0.5f,
                center + new Vector3(-size.x,  size.y, -size.z) * 0.5f,
                center + new Vector3(-size.x,  size.y,  size.z) * 0.5f,
                center + new Vector3( size.x, -size.y, -size.z) * 0.5f,
                center + new Vector3( size.x, -size.y,  size.z) * 0.5f,
                center + new Vector3( size.x,  size.y, -size.z) * 0.5f,
                center + new Vector3( size.x,  size.y,  size.z) * 0.5f
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 worldCorner = box.transform.TransformPoint(corner);
                Vector3 localToVehicle = transform.InverseTransformPoint(worldCorner);

                minY = Mathf.Min(minY, localToVehicle.y);
                maxZ = Mathf.Max(maxZ, localToVehicle.z);
                minZ = Mathf.Min(minZ, localToVehicle.z);
                maxX = Mathf.Max(maxX, Mathf.Abs(localToVehicle.x));
                foundAny = true;
            }
        }

        if (foundAny)
        {
            vehicleBottomY = minY;
            vehicleFrontZ = maxZ;
            vehicleRearZ = minZ;
            vehicleHalfWidth = Mathf.Max(0.4f, maxX);
            boundsCalculated = true;
        }
        else
        {
            vehicleBottomY = 0f;
            vehicleFrontZ = 1.8f;
            vehicleRearZ = -1.8f;
            vehicleHalfWidth = 0.8f;
            boundsCalculated = true;
        }
    }

    private void HandleStepAssist(float verticalInput, float forwardSpeed)
    {
        if (!enableStepAssist || rb == null) return;

        float moveDirSign = 0f;
        if (Mathf.Abs(verticalInput) > 0.05f)
        {
            moveDirSign = Mathf.Sign(verticalInput);
        }
        else if (Mathf.Abs(forwardSpeed) > 0.2f)
        {
            moveDirSign = Mathf.Sign(forwardSpeed);
        }

        if (moveDirSign == 0f) return;

        bool isMovingForward = moveDirSign > 0f;
        Vector3 travelDirection = transform.forward * moveDirSign;
        float edgeZ = isMovingForward ? vehicleFrontZ : vehicleRearZ;

        float probeZ = edgeZ - (0.15f * moveDirSign);
        float probeY = vehicleBottomY + 0.05f;

        Vector3[] probeOffsets = new Vector3[]
        {
            new Vector3(0f, probeY, probeZ),
            new Vector3(-vehicleHalfWidth * 0.75f, probeY, probeZ),
            new Vector3(vehicleHalfWidth * 0.75f, probeY, probeZ)
        };

        float probeDistance = Mathf.Clamp(Mathf.Abs(forwardSpeed) * Time.fixedDeltaTime * 2.5f + 0.35f, 0.4f, 1.2f);

        for (int i = 0; i < probeOffsets.Length; i++)
        {
            Vector3 origin = transform.TransformPoint(probeOffsets[i]);

            // 1. Rayo Inferior (a ras de suelo)
            if (Physics.Raycast(origin, travelDirection, out RaycastHit lowHit, probeDistance, roadLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (lowHit.collider.transform == transform || lowHit.collider.transform.IsChildOf(transform))
                    continue;

                // 2. Rayo Superior (a la altura de maxStepHeight)
                Vector3 highOrigin = origin + Vector3.up * maxStepHeight;
                bool highBlocked = false;

                if (Physics.Raycast(highOrigin, travelDirection, out RaycastHit highHit, probeDistance, roadLayerMask, QueryTriggerInteraction.Ignore))
                {
                    if (highHit.collider.transform != transform && !highHit.collider.transform.IsChildOf(transform))
                    {
                        highBlocked = true;
                    }
                }

                // Si arriba está despejado, es un escalón o junta de piso que podemos superar
                if (!highBlocked)
                {
                    // 3. Medir altura de la superficie
                    Vector3 downRayOrigin = lowHit.point + travelDirection * 0.12f + Vector3.up * (maxStepHeight + 0.15f);
                    if (Physics.Raycast(downRayOrigin, Vector3.down, out RaycastHit surfaceHit, maxStepHeight + 0.25f, roadLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        if (surfaceHit.collider.transform != transform && !surfaceHit.collider.transform.IsChildOf(transform))
                        {
                            float stepHeight = surfaceHit.point.y - (origin.y - 0.05f);

                            if (stepHeight > 0.002f && stepHeight <= maxStepHeight)
                            {
                                float liftAmount = Mathf.Min(stepHeight + 0.025f, maxStepHeight);
                                rb.position += Vector3.up * (liftAmount * 12f * Time.fixedDeltaTime);

                                if (Mathf.Abs(forwardSpeed) > 0.5f)
                                {
                                    Vector3 currentPlanarVel = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
                                    if (currentPlanarVel.magnitude < Mathf.Abs(forwardSpeed) * 0.85f)
                                    {
                                        Vector3 targetVel = travelDirection * Mathf.Abs(forwardSpeed);
                                        rb.linearVelocity = new Vector3(targetVel.x, Mathf.Max(rb.linearVelocity.y, 0.6f), targetVel.z);
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        AssistContactClimb(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        AssistContactClimb(collision);
    }

    private void AssistContactClimb(Collision collision)
    {
        if (!enableStepAssist || !isPlayerInside || rb == null) return;
        
        float verticalInput = moveInput.y;
        if (Mathf.Abs(verticalInput) < 0.1f) return;

        if (collision.gameObject.transform.IsChildOf(transform)) return;

        float forwardSign = Mathf.Sign(verticalInput);
        Vector3 moveDir = transform.forward * forwardSign;
        float worldBottomY = transform.position.y + vehicleBottomY;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            
            float dotAgainstMove = Vector3.Dot(contact.normal, moveDir);
            if (dotAgainstMove < -0.3f)
            {
                float contactHeight = contact.point.y - worldBottomY;
                if (contactHeight >= -0.1f && contactHeight <= maxStepHeight)
                {
                    rb.position += Vector3.up * 0.035f;

                    // BUG FIX: Antes forzaba una velocidad mínima de 3.5 m/s en cualquier
                    // contacto lateral → arrancones incontrolables al rozar objetos.
                    // Ahora solo se preserva la velocidad actual sin forzar un mínimo.
                    float currentSpeed = rb.linearVelocity.magnitude;
                    if (currentSpeed > 0.3f)
                    {
                        Vector3 slideVelocity = moveDir * currentSpeed;
                        rb.linearVelocity = new Vector3(slideVelocity.x, Mathf.Max(rb.linearVelocity.y, 0.3f), slideVelocity.z);
                    }
                    break;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!boundsCalculated)
        {
            CalculateVehicleBounds();
        }

        Gizmos.color = Color.cyan;
        float probeZ = vehicleFrontZ - 0.15f;
        float probeY = vehicleBottomY + 0.05f;

        Vector3[] frontPoints = new Vector3[]
        {
            transform.TransformPoint(new Vector3(0f, probeY, probeZ)),
            transform.TransformPoint(new Vector3(-vehicleHalfWidth * 0.75f, probeY, probeZ)),
            transform.TransformPoint(new Vector3(vehicleHalfWidth * 0.75f, probeY, probeZ))
        };

        foreach (Vector3 pt in frontPoints)
        {
            Gizmos.DrawSphere(pt, 0.04f);
            Gizmos.DrawLine(pt, pt + transform.forward * 0.8f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pt + Vector3.up * maxStepHeight, pt + Vector3.up * maxStepHeight + transform.forward * 0.8f);
            Gizmos.color = Color.cyan;
        }
    }
}
