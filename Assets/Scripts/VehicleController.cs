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

    [Header("Bail-Out / Desaceleración al Salir")]
    [Tooltip("Porcentaje de velocidad que conserva el auto inmediatamente al bajarse el jugador en marcha (ej: 0.35 = se reduce al 35% de la velocidad actual).")]
    [Range(0.05f, 1f)]
    public float exitSpeedMultiplier = 0.35f;

    [Tooltip("Velocidad máxima (m/s) que puede conservar el vehículo inmediatamente al bajarse.")]
    public float maxExitSpeed = 6f;

    [Tooltip("Fuerza de desaceleración y frenado progresivo cuando el auto rueda sin conductor hasta detenerse.")]
    public float emptyVehicleDeceleration = 4f;

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
    private bool wasPlayerInside = false;

    /// <summary>Velocidad de avance longitudinal en m/s (positiva hacia adelante, negativa marcha atrás).</summary>
    public float ForwardSpeed => rb != null ? Vector3.Dot(transform.forward, rb.linearVelocity) : 0f;

    /// <summary>Magnitud de la velocidad en m/s.</summary>
    public float LinearSpeed => rb != null ? rb.linearVelocity.magnitude : 0f;

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

    // Mutex de frame: solo UN sistema de seam-assist puede aplicar fuerza por FixedUpdate.
    // Evita que HandleStepAssist + SeamSkimmer + AssistContactClimb se acumulen y produzcan saltos.
    private bool _seamAssistedThisFrame = false;

    // Material resbaloso compartido, creado una sola vez y reutilizado.
    private PhysicsMaterial _vehicleSlipMat;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Masa real de camioneta de reparto
            rb.mass = 1200f;

            // IMPORTANTE: desactivar el centro de masa automático para poder bajarlo nosotros.
            // Si automaticCenterOfMass = true Unity lo recalcula y sobreescribe nuestro valor.
            rb.automaticCenterOfMass = false;
            // Centro de masa bajo para mayor estabilidad (evita vuelcos en curvas y bordillos)
            rb.centerOfMass = new Vector3(0f, -0.5f, 0f);

            // Congelar rotación en X y Z para que el motor de físicas no incline el auto
            // al chocar con bordillos, juntas o superficies irregulares.
            // Y (giro de dirección) lo manejamos manualmente con MoveRotation.
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            // Amortiguación angular alta para que el auto no siga girando solo
            rb.angularDamping = 5f;

            // Iteraciones de solver altas = físicas más estables en colisiones complejas
            rb.solverIterations = 12;
            rb.solverVelocityIterations = 4;

            // Interpolación para movimiento fluido en pantalla
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Detección continua para evitar atravesar bordes de colliders
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        // Calcular dimensiones para el step assist y los bevel colliders
        CalculateVehicleBounds();

        // Agregar colliders esféricos en las esquinas inferiores del vehículo
        // para que suba suavemente sobre veredas (efecto "bisel" o borde redondeado)
        if (enableBevelColliders)
        {
            AddBevelColliders();
        }

        // IMPORTANTE: Aplicar el material resbaloso DESPUÉS de crear los bevel colliders,
        // para que todos los colliders (incluyendo los recién creados) lo reciban.
        // BUG FIX: Antes se llamaba antes de AddBevelColliders → los bevel spheres quedaban
        // con fricción por defecto (0.6) y se enganchaban en las juntas de tiles.
        ApplyFrictionlessMaterial();

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

        wasPlayerInside = isPlayerInside;
    }

    void Update()
    {
        // Si el jugador acaba de salir (por código o Inspector), aplicar el frenado de escape
        if (wasPlayerInside && !isPlayerInside)
        {
            ApplyExitBraking();
        }
        wasPlayerInside = isPlayerInside;

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
        if (rb == null) return;

        if (isPlayerInside)
        {
            _seamAssistedThisFrame = false; // Resetear mutex al inicio de cada frame físico
            DrivePhysics();
            SeamSkimmer();
        }
        else
        {
            HandleEmptyVehiclePhysics();
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

    /// <summary>
    /// Llamado cuando el jugador se baja del vehículo.
    /// Reduce drásticamente la velocidad excesiva para que el auto ruede con inercia controlada
    /// y justa para el jugador, sin salir disparado fuera de alcance.
    /// </summary>
    public void OnPlayerExit()
    {
        isPlayerInside = false;
        wasPlayerInside = false;
        ApplyExitBraking();
    }

    private void ApplyExitBraking()
    {
        if (rb == null) return;

        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float currentSpeed = horizontalVel.magnitude;

        if (currentSpeed > 0.1f)
        {
            // Reducir la velocidad horizontal al porcentaje configurado y limitarla a maxExitSpeed
            float targetSpeed = Mathf.Min(currentSpeed * exitSpeedMultiplier, maxExitSpeed);
            Vector3 newHorizontalVel = horizontalVel.normalized * targetSpeed;
            rb.linearVelocity = new Vector3(newHorizontalVel.x, rb.linearVelocity.y, newHorizontalVel.z);
        }
    }

    /// <summary>
    /// Físicas cuando el vehículo no tiene conductor:
    /// Desacelera suavemente por fricción de rodadura, mantiene agarre lateral y downforce,
    /// y aplica el freno de estacionamiento una vez detenido para que no deslice infinitamente.
    /// </summary>
    private void HandleEmptyVehiclePhysics()
    {
        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float speed = horizontalVel.magnitude;

        if (speed > 0.15f)
        {
            // 1. Desaceleración progresiva (freno natural por fricción y rodadura sin conductor)
            Vector3 brakeForce = -horizontalVel.normalized * emptyVehicleDeceleration;
            rb.AddForce(brakeForce, ForceMode.Acceleration);

            // 2. Agarre lateral (evita que el auto derrape de lado sin control)
            Vector3 rightVelocity = transform.right * Vector3.Dot(rb.linearVelocity, transform.right);
            rb.AddForce(-rightVelocity * grip, ForceMode.Acceleration);

            // 3. Downforce para mantener las ruedas pegadas al asfalto
            if (downforce > 0f)
            {
                rb.AddForce(-Vector3.up * downforce, ForceMode.Acceleration);
            }
        }
        else
        {
            // 4. Detención completa / Freno de estacionamiento:
            // Anula cualquier deslizamiento horizontal residual sin afectar la gravedad vertical
            if (horizontalVel.sqrMagnitude > 0.0001f)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
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
        // Crear el material una sola vez y guardarlo como campo.
        if (_vehicleSlipMat == null)
        {
            _vehicleSlipMat = new PhysicsMaterial("VehicleSeamSlip")
            {
                dynamicFriction  = 0.02f,
                staticFriction   = 0.02f,
                bounciness       = 0f,
                frictionCombine  = PhysicsMaterialCombine.Minimum,
                bounceCombine    = PhysicsMaterialCombine.Minimum
            };
        }

        // BUG FIX: La condición anterior "if (col.sharedMaterial == null)" saltaba los
        // colliders que tenían un material vacío instanciado en tiempo de ejecución.
        // Ahora SIEMPRE asignamos el material a todos los colliders no-trigger del vehículo.
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in cols)
        {
            if (!col.isTrigger)
            {
                col.sharedMaterial = _vehicleSlipMat;
            }
        }
    }

    /// <summary>
    /// Crea un PERÍMETRO COMPLETO de SphereColliders a lo largo de los bordes
    /// inferiores del vehículo (frente, trasera, laterales).
    ///
    /// Con solo 4 esferas en las esquinas, la arista frontal del BoxCollider
    /// del Piso sigue siendo una línea recta a 90° que se engancha en las
    /// juntas de tiles. Con un perímetro denso, el perfil inferior del vehículo
    /// es prácticamente ovalado → desliza sobre cualquier junta sin trabarse.
    ///
    /// Densidad controlada por bevelSteps (por defecto: 1 esfera por cada
    /// ~bevelRadius*1.5 metros de arista).
    /// </summary>
    private void AddBevelColliders()
    {
        if (!boundsCalculated) CalculateVehicleBounds();

        // Reutilizar el material resbaloso ya creado por ApplyFrictionlessMaterial()
        // (fricción 0.02, bounciness 0, combine=Minimum)
        PhysicsMaterial mat = _vehicleSlipMat;
        if (mat == null)
        {
            mat = new PhysicsMaterial("BevelSlip")
            {
                dynamicFriction = 0.0f,
                staticFriction  = 0.0f,
                bounciness      = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine   = PhysicsMaterialCombine.Minimum
            };
        }

        float r  = bevelRadius;
        float cY = vehicleBottomY + r;           // altura del centro de las esferas
        float fZ = vehicleFrontZ  - r * 0.4f;   // borde frontal (ligeramente dentro)
        float rZ = vehicleRearZ   + r * 0.4f;   // borde trasero
        float lX = -(vehicleHalfWidth - r * 0.4f); // lateral izquierdo
        float rxX =  (vehicleHalfWidth - r * 0.4f); // lateral derecho

        // --- Calcular cuántas esferas caben a lo largo de cada arista ---
        // Paso entre centros: 1.4 * radio → ligera superposición para no dejar huecos
        float spacing = r * 1.4f;

        // Ancho disponible para las esferas del borde frontal/trasero
        float widthSpan  = rxX - lX;
        // Longitud disponible para los bordes laterales
        float lengthSpan = fZ - rZ;

        int stepsW = Mathf.Max(1, Mathf.RoundToInt(widthSpan  / spacing));
        int stepsL = Mathf.Max(1, Mathf.RoundToInt(lengthSpan / spacing));

        var positions = new System.Collections.Generic.List<(Vector3 pos, string name)>();

        // --- BORDE FRONTAL (fila de esferas en Z = fZ, barriendo X) ---
        for (int i = 0; i <= stepsW; i++)
        {
            float t = stepsW > 0 ? (float)i / stepsW : 0.5f;
            float x = Mathf.Lerp(lX, rxX, t);
            positions.Add((new Vector3(x, cY, fZ), $"Bevel_F{i:00}"));
        }

        // --- BORDE TRASERO (fila de esferas en Z = rZ, barriendo X) ---
        for (int i = 0; i <= stepsW; i++)
        {
            float t = stepsW > 0 ? (float)i / stepsW : 0.5f;
            float x = Mathf.Lerp(lX, rxX, t);
            positions.Add((new Vector3(x, cY, rZ), $"Bevel_R{i:00}"));
        }

        // --- BORDE LATERAL IZQUIERDO (barriendo Z, sin repetir esquinas) ---
        for (int i = 1; i < stepsL; i++)
        {
            float t = (float)i / stepsL;
            float z = Mathf.Lerp(fZ, rZ, t);
            positions.Add((new Vector3(lX, cY, z), $"Bevel_L{i:00}"));
        }

        // --- BORDE LATERAL DERECHO ---
        for (int i = 1; i < stepsL; i++)
        {
            float t = (float)i / stepsL;
            float z = Mathf.Lerp(fZ, rZ, t);
            positions.Add((new Vector3(rxX, cY, z), $"Bevel_Rx{i:00}"));
        }

        // --- Crear / reutilizar los GameObjects de bevel ---
        foreach (var (localPos, bName) in positions)
        {
            Transform existing = transform.Find(bName);
            GameObject bGO = existing != null ? existing.gameObject : new GameObject(bName);

            bGO.transform.SetParent(transform, false);
            bGO.transform.localPosition = localPos;
            bGO.transform.localRotation = Quaternion.identity;
            bGO.layer = gameObject.layer;

            SphereCollider sc = bGO.GetComponent<SphereCollider>();
            if (sc == null) sc = bGO.AddComponent<SphereCollider>();
            sc.radius         = r;
            sc.center         = Vector3.zero;
            sc.sharedMaterial = mat;
        }
    }

    private void CalculateVehicleBounds()
    {
        // BUG FIX: Usar TransformPoint/InverseTransformPoint para que la escala (1.4x) quede
        // correctamente incluida en las dimensiones calculadas del vehículo en espacio local del root.
        Collider[] allCols = GetComponentsInChildren<Collider>();
        bool foundAny = false;
        float minY = float.MaxValue;
        float maxZ = float.MinValue;
        float minZ = float.MaxValue;
        float maxX = float.MinValue;

        foreach (Collider col in allCols)
        {
            if (col.isTrigger) continue;
            // Ignorar los bevel colliders generados por este script para no crear recursión
            if (col.gameObject.name.StartsWith("Bevel_")) continue;

            Bounds wb = col.bounds; // Bounds en espacio mundo
            // Los 8 corners del AABB en espacio mundo
            Vector3 c = wb.center;
            Vector3 e = wb.extents;
            Vector3[] worldCorners = new Vector3[8]
            {
                c + new Vector3(-e.x, -e.y, -e.z),
                c + new Vector3(-e.x, -e.y,  e.z),
                c + new Vector3(-e.x,  e.y, -e.z),
                c + new Vector3(-e.x,  e.y,  e.z),
                c + new Vector3( e.x, -e.y, -e.z),
                c + new Vector3( e.x, -e.y,  e.z),
                c + new Vector3( e.x,  e.y, -e.z),
                c + new Vector3( e.x,  e.y,  e.z)
            };

            foreach (Vector3 wc in worldCorners)
            {
                // Convertir a espacio local del root (incluye escala y rotación del transform raíz)
                Vector3 lc = transform.InverseTransformPoint(wc);
                minY = Mathf.Min(minY, lc.y);
                maxZ = Mathf.Max(maxZ, lc.z);
                minZ = Mathf.Min(minZ, lc.z);
                maxX = Mathf.Max(maxX, Mathf.Abs(lc.x));
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
            moveDirSign = Mathf.Sign(verticalInput);
        else if (Mathf.Abs(forwardSpeed) > 0.2f)
            moveDirSign = Mathf.Sign(forwardSpeed);

        if (moveDirSign == 0f) return;

        Vector3 travelDirection = transform.forward * moveDirSign;
        bool isMovingForward = moveDirSign > 0f;
        float edgeZ = isMovingForward ? vehicleFrontZ : vehicleRearZ;

        // Probe ligeramente dentro del borde del vehículo (no afuera) para detectar antes de impactar
        float probeZ    = edgeZ - (0.1f * moveDirSign);
        float probeY    = vehicleBottomY + 0.06f;   // a 6 cm del fondo (por encima del asfalto)

        // 3 puntos de sondeo: centro, izquierda, derecha del frente/trasera
        Vector3[] probeOffsets = new Vector3[]
        {
            new Vector3(0f,                       probeY, probeZ),
            new Vector3(-vehicleHalfWidth * 0.7f, probeY, probeZ),
            new Vector3( vehicleHalfWidth * 0.7f, probeY, probeZ),
        };

        // Radio ajustado para no rozar el suelo plano al cabecear el vehículo
        float probeRadius   = 0.035f;
        float probeDistance = Mathf.Clamp(Mathf.Abs(forwardSpeed) * Time.fixedDeltaTime * 3f + 0.3f, 0.3f, 1.0f);

        for (int i = 0; i < probeOffsets.Length; i++)
        {
            Vector3 origin = transform.TransformPoint(probeOffsets[i]);

            // 1. SphereCast inferior – detecta bordillos reales (no asfalto)
            RaycastHit lowHit;
            bool hitLow = Physics.SphereCast(origin, probeRadius, travelDirection,
                                              out lowHit, probeDistance,
                                              roadLayerMask, QueryTriggerInteraction.Ignore);
            if (!hitLow) continue;
            if (lowHit.collider.transform == transform ||
                lowHit.collider.transform.IsChildOf(transform)) continue;

            // 2. Verificar que arriba está despejado (es escalón, no muro alto)
            Vector3 highOrigin = origin + Vector3.up * maxStepHeight;
            bool highBlocked = Physics.SphereCast(highOrigin, probeRadius * 0.5f, travelDirection,
                                                   out _, probeDistance * 0.8f,
                                                   roadLayerMask, QueryTriggerInteraction.Ignore);
            if (highBlocked) continue;

            // 3. Medir la altura real del obstáculo
            Vector3 downOrigin = lowHit.point + travelDirection * 0.1f
                                + Vector3.up * (maxStepHeight + 0.1f);
            RaycastHit surfaceHit;
            if (!Physics.Raycast(downOrigin, Vector3.down, out surfaceHit,
                                  maxStepHeight + 0.2f, roadLayerMask, QueryTriggerInteraction.Ignore))
                continue;
            if (surfaceHit.collider.transform == transform ||
                surfaceHit.collider.transform.IsChildOf(transform)) continue;

            float stepHeight = surfaceHit.point.y - (origin.y - 0.06f);

            // Solo actuar si es un bordillo real (≥ 70mm y ≤ maxStepHeight).
            // Juntas y micro-desniveles (< 70mm) son absorbidos suavemente por los colliders biselados sin saltar.
            if (stepHeight < 0.07f || stepHeight > maxStepHeight) continue;

            // Si el vehículo ya tiene velocidad hacia arriba, NO agregar más fuerza vertical
            if (rb.linearVelocity.y > 0.02f) break;

            // Mutex: ceder el turno si otro sistema ya actuó este frame
            if (_seamAssistedThisFrame) break;
            _seamAssistedThisFrame = true;

            // --- Nudge vertical muy suave (máximo 0.35 m/s) para no catapultar el auto ---
            float targetLiftVel = Mathf.Lerp(0.15f, 0.35f, stepHeight / maxStepHeight);
            if (rb.linearVelocity.y < targetLiftVel)
            {
                float velDiff = targetLiftVel - rb.linearVelocity.y;
                rb.AddForce(Vector3.up * velDiff * 2.5f, ForceMode.Acceleration);
            }

            // Pequeño boost hacia adelante para no perder momentum al subir
            if (Mathf.Abs(forwardSpeed) > 0.5f)
            {
                float forwardBoost = Mathf.Clamp(stepHeight * 3f, 0.3f, 2f);
                rb.AddForce(travelDirection * forwardBoost, ForceMode.Acceleration);
            }
            break;
        }
    }

    /// <summary>
    /// SeamSkimmer: se ejecuta cada FixedUpdate.
    /// Hace un SphereCast hacia abajo desde las 4 esquinas inferiores del vehículo.
    /// Si una esquina está "colgando" sobre el borde de un tile (diferencia de altura respecto
    /// al suelo promedio), aplica una pequeña fuerza hacia abajo en esa esquina para re-centrar
    /// el vehículo sobre la junta y evitar que se enganche.
    /// También detecta contactos laterales mínimos y los convierte en fuerza de escalada.
    /// </summary>
    private void SeamSkimmer()
    {
        if (!enableStepAssist || rb == null) return;

        float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
        if (Mathf.Abs(forwardSpeed) < 0.2f) return; // Solo actúa en movimiento

        // Sondear el suelo bajo cada una de las 4 esquinas del vehículo
        float cx = vehicleHalfWidth * 0.85f;
        float cz_f = vehicleFrontZ  * 0.85f;
        float cz_r = vehicleRearZ   * 0.85f;
        float probeStart = vehicleBottomY + 0.15f; // Empieza un poco arriba del fondo

        Vector3[] corners = new Vector3[]
        {
            new Vector3(-cx, probeStart, cz_f),
            new Vector3( cx, probeStart, cz_f),
            new Vector3(-cx, probeStart, cz_r),
            new Vector3( cx, probeStart, cz_r),
        };

        float groundSphereRadius = 0.07f;
        float groundMaxDist      = 0.35f;
        float totalGroundY       = 0f;
        int   groundCount        = 0;

        float[] cornerGroundY = new float[4];
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 worldOrigin = transform.TransformPoint(corners[i]);
            RaycastHit groundHit;
            if (Physics.SphereCast(worldOrigin, groundSphereRadius, Vector3.down,
                                    out groundHit, groundMaxDist,
                                    roadLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (!groundHit.collider.transform.IsChildOf(transform))
                {
                    cornerGroundY[i]  = groundHit.point.y;
                    totalGroundY     += groundHit.point.y;
                    groundCount++;
                }
                else
                {
                    cornerGroundY[i] = float.NaN;
                }
            }
            else
            {
                cornerGroundY[i] = float.NaN;
            }
        }

        if (groundCount < 2) return;

        float avgGroundY = totalGroundY / groundCount;

        // Umbral mínimo de 70mm para ignorar rugosidad, curvas y juntas de pistas
        // Solo actuar sobre bordillos de vereda reales
        const float kSeamThreshold = 0.07f;

        for (int i = 0; i < corners.Length; i++)
        {
            if (float.IsNaN(cornerGroundY[i])) continue;

            float diff = cornerGroundY[i] - avgGroundY;

            if (diff > kSeamThreshold && diff <= maxStepHeight)
            {
                // Si el vehículo ya va subiendo, no acumular más impulso vertical
                if (rb.linearVelocity.y > 0.02f) break;

                // Mutex: no acumular con HandleStepAssist
                if (_seamAssistedThisFrame) break;
                _seamAssistedThisFrame = true;

                // Nudge de velocidad Y muy suave — máximo 0.3 m/s hacia arriba
                float targetLiftVel = Mathf.Lerp(0.1f, 0.3f, diff / maxStepHeight);
                if (rb.linearVelocity.y < targetLiftVel)
                {
                    float velDiff = targetLiftVel - rb.linearVelocity.y;
                    rb.AddForce(Vector3.up * velDiff * 2f, ForceMode.Acceleration);
                }
                break;
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
        float forwardSpeed  = Vector3.Dot(transform.forward, rb.linearVelocity);

        // Actúa si hay movimiento intencionado o inercia
        float effectiveMoveSign = 0f;
        if (Mathf.Abs(verticalInput) > 0.05f)
            effectiveMoveSign = Mathf.Sign(verticalInput);
        else if (Mathf.Abs(forwardSpeed) > 0.5f)
            effectiveMoveSign = Mathf.Sign(forwardSpeed);

        if (effectiveMoveSign == 0f) return;
        if (collision.gameObject.transform.IsChildOf(transform)) return;

        Vector3 moveDir      = transform.forward * effectiveMoveSign;
        float worldBottomY   = transform.TransformPoint(new Vector3(0, vehicleBottomY, 0)).y;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);

            // La normal del contacto debe ser mayoritariamente horizontal y opuesta al movimiento
            float dotAgainstMove = Vector3.Dot(contact.normal, moveDir);
            if (dotAgainstMove > -0.3f) continue; // No es un bloqueo frontal

            // Comprobar que la normal sea horizontal (no el suelo plano ni un muro)
            float normalVertical = Mathf.Abs(contact.normal.y);
            if (normalVertical > 0.7f) continue; // Si es inclinada como suelo o curva, ignorar

            float contactHeight = contact.point.y - worldBottomY;

            // Solo reaccionar si el contacto es un bordillo real (≥ 70mm y ≤ maxStepHeight).
            if (contactHeight < 0.07f || contactHeight > maxStepHeight) continue;

            // Si el vehículo ya tiene velocidad hacia arriba, NO agregar más fuerza vertical
            if (rb.linearVelocity.y > 0.02f) break;

            // Mutex: ceder el turno si HandleStepAssist o SeamSkimmer ya actuaron
            if (_seamAssistedThisFrame) break;
            _seamAssistedThisFrame = true;

            // --- Nudge de velocidad Y muy suave para no dar botes ---
            float blockIntensity = Mathf.Clamp01(1f - (contactHeight / maxStepHeight));
            float targetLiftVel = blockIntensity * 0.35f;
            if (rb.linearVelocity.y < targetLiftVel)
            {
                float velDiff = targetLiftVel - rb.linearVelocity.y;
                rb.AddForce(Vector3.up * velDiff * 2.5f, ForceMode.Acceleration);
            }

            // Pequeño boost adelante para no perder momentum
            float currentSpeed = Mathf.Abs(forwardSpeed);
            if (currentSpeed > 0.5f)
            {
                float forwardAlignment = Vector3.Dot(rb.linearVelocity.normalized, moveDir);
                if (forwardAlignment > 0.4f)
                    rb.AddForce(moveDir * blockIntensity * 2f, ForceMode.Acceleration);
            }

            break;
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
