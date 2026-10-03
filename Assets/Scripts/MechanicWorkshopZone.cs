using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestiona la zona de entrada al Taller Mecánico delante de la puerta del garaje.
/// Proyecta un cilindro brillante azul translúcido estilo GTA (misiones de vehículos) con aro en el piso y luz.
/// Detecta si el jugador está dentro de la van conduciendo (por proximidad geométrica y trigger),
/// y al mantener presionada la tecla [F], abre la interfaz de mejoras del mecánico (MechanicWorkshopUI).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class MechanicWorkshopZone : MonoBehaviour
{
    [Header("Dimensiones del Marcador GTA")]
    [Tooltip("Diámetro del cilindro y aro en el suelo (en metros).")]
    public float markerDiameter = 5.5f;

    [Tooltip("Altura del cilindro azul (en metros). No llega al cielo, estilo misiones GTA.")]
    public float cylinderHeight = 2.2f;

    [Header("Interacción")]
    [Tooltip("Tiempo necesario manteniendo presionada la tecla [F] para entrar al taller (en segundos).")]
    public float holdDuration = 0.8f;

    [Header("Materiales y Visuales")]
    public Material coronaMaterial;
    public Material ringMaterial;
    public Color blueColor = new Color(0.12f, 0.65f, 1.0f, 0.85f);

    [Header("Sub-Objetos Visuales")]
    public GameObject coronaWall;
    public GameObject floorRing;
    public Light coronaLight;

    // Estado interno
    private BoxCollider triggerCollider;
    private VehicleInteraction cachedVehicle;
    private float holdTimer = 0f;
    private bool isPlayerInZoneWithVan = false;
    private bool wasPlayerInZone = false;

    // Entrada dedicada para mantener F
    private InputAction holdFAction;

    public bool IsPlayerInZoneWithVan => isPlayerInZoneWithVan;
    public float HoldProgress => Mathf.Clamp01(holdTimer / (holdDuration > 0.01f ? holdDuration : 0.8f));

    // Sub-colliders del vehículo dentro del trigger
    private readonly HashSet<Collider> vanCollidersInTrigger = new HashSet<Collider>();
    private GameObject cachedPlayerObject;

    void Awake()
    {
        triggerCollider = GetComponent<BoxCollider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            // Aumentar la altura y área del trigger para abarcar holgadamente el chasis y ruedas
            triggerCollider.size = new Vector3(markerDiameter + 3.0f, cylinderHeight + 4.5f, markerDiameter + 3.0f);
            triggerCollider.center = new Vector3(0f, (cylinderHeight * 0.5f), 0f);
        }

        EnsureVisuals();
    }

    void OnEnable()
    {
        if (holdFAction == null)
        {
            holdFAction = new InputAction("HoldFToWorkshop", InputActionType.Button);
            holdFAction.AddBinding("<Keyboard>/f");
            holdFAction.AddBinding("<Gamepad>/buttonNorth"); // Botón Y / Triángulo
            holdFAction.AddBinding("<Gamepad>/buttonEast");  // Botón B / Círculo
        }
        holdFAction.Enable();
    }

    void OnDisable()
    {
        holdFAction?.Disable();
    }

    void Start()
    {
        EnsureVisuals();
        FindVehicle();
        FindPlayer();
    }

    void Update()
    {
        // 1. Asegurar referencias
        if (cachedVehicle == null) FindVehicle();
        if (cachedPlayerObject == null) FindPlayer();

        // 2. Comprobación HÍBRIDA de la Van (Trigger + Distancia generosa)
        // Limpiar colliders nulos o desactivados
        vanCollidersInTrigger.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);

        bool vanInZone = false;
        if (cachedVehicle != null)
        {
            Vector3 vanPos = cachedVehicle.transform.position;
            Vector2 cylinderXZ = new Vector2(transform.position.x, transform.position.z);
            Vector2 vanXZ = new Vector2(vanPos.x, vanPos.z);
            float distXZ = Vector2.Distance(cylinderXZ, vanXZ);
            float distY = Mathf.Abs(transform.position.y - vanPos.y);

            // Radio de detección generoso: radio del cilindro (2.75m) + longitud de van (3.2m) = 5.95m
            float maxRadius = (markerDiameter * 0.5f) + 3.2f;
            vanInZone = (vanCollidersInTrigger.Count > 0) || (distXZ <= maxRadius && distY <= 5.5f);
        }

        // 3. Comprobación de jugador a pie (por si se baja a explorar)
        bool playerOnFootInZone = false;
        if (cachedPlayerObject != null && cachedPlayerObject.activeInHierarchy)
        {
            float distPlayer = Vector3.Distance(transform.position, cachedPlayerObject.transform.position);
            playerOnFootInZone = (distPlayer <= (markerDiameter * 0.5f) + 1.2f);
        }

        bool vanDrivenByPlayer = (vanInZone && cachedVehicle != null && cachedVehicle.PlayerIsInside);
        isPlayerInZoneWithVan = vanDrivenByPlayer || playerOnFootInZone;

        // Feedback en consola al entrar/salir de la zona
        if (isPlayerInZoneWithVan != wasPlayerInZone)
        {
            wasPlayerInZone = isPlayerInZoneWithVan;
            if (isPlayerInZoneWithVan)
            {
                Debug.Log("[MechanicWorkshopZone] En posición en el Taller. Mantén [F] para entrar.");
            }
        }

        // 4. Si el menú del taller ya está abierto, ocultar prompt
        if (MechanicWorkshopUI.Instance != null && MechanicWorkshopUI.Instance.IsOpen)
        {
            if (MechanicWorkshopPromptHUD.Instance != null)
            {
                MechanicWorkshopPromptHUD.Instance.SetVisible(false);
            }
            holdTimer = 0f;
            return;
        }

        // 5. Procesar interacción cuando el jugador está en la zona
        if (isPlayerInZoneWithVan)
        {
            if (MechanicWorkshopPromptHUD.Instance != null)
            {
                MechanicWorkshopPromptHUD.Instance.SetVisible(true);
            }

            bool isHoldingF = CheckHoldInput();

            if (isHoldingF)
            {
                holdTimer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(holdTimer / holdDuration);

                if (MechanicWorkshopPromptHUD.Instance != null)
                {
                    MechanicWorkshopPromptHUD.Instance.SetHoldProgress(progress);
                }

                if (holdTimer >= holdDuration)
                {
                    holdTimer = 0f;
                    if (MechanicWorkshopPromptHUD.Instance != null)
                    {
                        MechanicWorkshopPromptHUD.Instance.SetVisible(false);
                    }

                    // Abrir la vista del taller mecánico
                    VehicleController vCtrl = cachedVehicle != null ? cachedVehicle.GetComponent<VehicleController>() : null;
                    if (MechanicWorkshopUI.Instance != null)
                    {
                        MechanicWorkshopUI.Instance.OpenWorkshop(vCtrl);
                    }
                    else
                    {
                        Debug.LogError("[MechanicWorkshopZone] MechanicWorkshopUI.Instance no fue encontrado en la escena.");
                    }
                }
            }
            else
            {
                if (holdTimer > 0f)
                {
                    holdTimer = Mathf.MoveTowards(holdTimer, 0f, Time.unscaledDeltaTime * 3.5f);
                    if (MechanicWorkshopPromptHUD.Instance != null)
                    {
                        MechanicWorkshopPromptHUD.Instance.SetHoldProgress(HoldProgress);
                    }
                }
            }
        }
        else
        {
            if (holdTimer > 0f)
            {
                holdTimer = 0f;
            }

            if (MechanicWorkshopPromptHUD.Instance != null)
            {
                MechanicWorkshopPromptHUD.Instance.SetVisible(false);
            }
        }
    }

    private bool CheckHoldInput()
    {
        // 1. InputAction dedicada
        if (holdFAction != null && holdFAction.IsPressed()) return true;

        // 2. Keyboard directo del New Input System
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.fKey.isPressed) return true;
            if (kb[Key.F].isPressed) return true;
        }

        // 3. Mando
        var gp = Gamepad.current;
        if (gp != null && (gp.buttonNorth.isPressed || gp.buttonEast.isPressed)) return true;

        // 4. Fallback legacy
        try
        {
            if (Input.GetKey(KeyCode.F)) return true;
        }
        catch { }

        return false;
    }

    private void FindVehicle()
    {
        if (cachedVehicle == null)
        {
            cachedVehicle = Object.FindAnyObjectByType<VehicleInteraction>();
        }
    }

    private void FindPlayer()
    {
        if (cachedPlayerObject == null)
        {
            cachedPlayerObject = GameObject.FindGameObjectWithTag("Player");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        VehicleInteraction vi = other.GetComponentInParent<VehicleInteraction>();
        if (vi != null)
        {
            cachedVehicle = vi;
            vanCollidersInTrigger.Add(other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        VehicleInteraction vi = other.GetComponentInParent<VehicleInteraction>();
        if (vi != null)
        {
            cachedVehicle = vi;
            vanCollidersInTrigger.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        vanCollidersInTrigger.Remove(other);
    }

    /// <summary>
    /// Construye y verifica la estructura visual del cilindro brillante azul estilo GTA.
    /// </summary>
    public void EnsureVisuals()
    {
        // 1. Cargar materiales si no están asignados
#if UNITY_EDITOR
        if (coronaMaterial == null)
        {
            coronaMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mechanic_GTACorona_Blue.mat");
        }
        if (ringMaterial == null)
        {
            ringMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mechanic_GTARing_Blue.mat");
        }
#endif

        if (coronaMaterial == null)
        {
            Shader cs = Shader.Find("Custom/GTA_MissionCorona");
            if (cs != null)
            {
                coronaMaterial = new Material(cs);
                coronaMaterial.SetColor("_Color", blueColor);
                coronaMaterial.SetFloat("_EmissionPower", 3.0f);
            }
        }

        if (ringMaterial == null)
        {
            Shader rs = Shader.Find("Custom/GTA_GroundRing");
            if (rs != null)
            {
                ringMaterial = new Material(rs);
                ringMaterial.SetColor("_Color", blueColor);
                ringMaterial.SetFloat("_EmissionPower", 2.6f);
            }
        }

        // 2. FloorRing (Aro plano sobre el pavimento)
        Transform ringT = transform.Find("FloorRing");
        if (ringT == null)
        {
            GameObject ringGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ringGO.name = "FloorRing";
            ringGO.transform.SetParent(transform, false);
            Collider c = ringGO.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
            ringT = ringGO.transform;
        }

        ringT.localPosition = new Vector3(0f, 0.03f, 0f);
        ringT.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ringT.localScale = new Vector3(markerDiameter, markerDiameter, 1f);

        MeshRenderer ringRend = ringT.GetComponent<MeshRenderer>();
        if (ringRend != null && ringMaterial != null)
        {
            ringRend.sharedMaterial = ringMaterial;
            ringRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringRend.receiveShadows = false;
        }
        floorRing = ringT.gameObject;

        // 3. CoronaWall (Cilindro vertical GTA de altura moderada)
        Transform coronaT = transform.Find("CoronaWall");
        if (coronaT == null)
        {
            GameObject coronaGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coronaGO.name = "CoronaWall";
            coronaGO.transform.SetParent(transform, false);
            Collider c = coronaGO.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
            coronaT = coronaGO.transform;
        }

        float halfHeight = cylinderHeight * 0.5f;
        coronaT.localPosition = new Vector3(0f, halfHeight, 0f);
        coronaT.localRotation = Quaternion.identity;
        coronaT.localScale = new Vector3(markerDiameter, halfHeight, markerDiameter);

        MeshRenderer coronaRend = coronaT.GetComponent<MeshRenderer>();
        if (coronaRend != null && coronaMaterial != null)
        {
            coronaRend.sharedMaterial = coronaMaterial;
            coronaRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            coronaRend.receiveShadows = false;
        }
        coronaWall = coronaT.gameObject;

        // 4. CoronaLight (Luz puntual azul que ilumina el suelo y la van al entrar)
        Transform lightT = transform.Find("CoronaLight");
        Light gLight = null;
        if (lightT == null)
        {
            GameObject lightGO = new GameObject("CoronaLight");
            lightGO.transform.SetParent(transform, false);
            gLight = lightGO.AddComponent<Light>();
            lightT = lightGO.transform;
        }
        else
        {
            gLight = lightT.GetComponent<Light>();
            if (gLight == null) gLight = lightT.gameObject.AddComponent<Light>();
        }

        lightT.localPosition = new Vector3(0f, 0.8f, 0f);
        gLight.type = LightType.Point;
        gLight.range = markerDiameter * 1.3f;
        gLight.color = new Color(0.18f, 0.72f, 1.0f);
        gLight.intensity = 2.8f;
        coronaLight = gLight;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.12f, 0.65f, 1f, 0.7f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * (cylinderHeight * 0.5f),
                            new Vector3(markerDiameter, cylinderHeight, markerDiameter));
    }
}
