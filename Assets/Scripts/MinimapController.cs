using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controlador del minimapa estilo GTA para A-Toda-Olla.
///
/// - Cámara ortográfica cenital que sigue al jugador o al DeliveryCar.
/// - Orientación 'Heading-Up': La parte superior del minimapa siempre es hacia donde
///   mira el jugador (en primera persona) o hacia donde apunta el auto (al conducir).
/// - Icono del jugador (triángulo blanco) en el centro apuntando siempre hacia arriba.
/// - Marcador de objetivo (cuadrado amarillo #FFD700 + borde negro) siempre visible:
///     Si el destino está dentro del radio: posición exacta en el mapa.
///     Si el destino está fuera del radio: clampea al borde del círculo relativo a la orientación.
/// - Sin marcador para la OllaConComida.
/// - El Zoom se controla 100% mediante el componente Camera (Size / OrthographicSize).
/// - Compatible con URP (Universal Render Pipeline).
/// </summary>
public class MinimapController : MonoBehaviour
{
    [Header("Cámara del Minimapa")]
    [Tooltip("Cámara ortográfica que mira el mundo desde arriba.")]
    public Camera minimapCamera;

    [Header("Referencias de UI")]
    [Tooltip("RawImage donde se proyecta el RenderTexture del minimapa.")]
    public RawImage minimapView;

    [Tooltip("RectTransform del icono del jugador (triángulo blanco, siempre en el centro).")]
    public RectTransform playerIcon;

    [Tooltip("RectTransform del marcador del objetivo (cuadrado amarillo con borde negro).")]
    public RectTransform objectiveMarker;

    [Header("Orientación del Mapa")]
    [Tooltip("Si está activo, la parte superior del minimapa siempre apunta hacia donde mira el jugador (a pie) o hacia donde apunta el auto (al conducir).")]
    public bool rotateWithPlayer = true;

    [Header("Parámetros del Mapa")]
    [Tooltip("Radio del mundo que abarca el minimapa. Muestra el Size actual de la cámara.")]
    public float worldRadius = 20f;

    [Tooltip("Radio del círculo del minimapa en píxeles de pantalla. Dejar en 0 para calcular automáticamente según el tamaño del contenedor UI.")]
    public float uiRadius = 0f;

    [Tooltip("Altura de la cámara del minimapa sobre el jugador/vehículo.")]
    public float cameraHeight = 150f;

    [Header("Resolución del Minimapa")]
    [Tooltip("Resolución de la textura del minimapa (512x512 recomendado para UI grande).")]
    public int renderTextureSize = 512;

    // Referencias internas
    private FoodDeliveryManager deliveryManager;
    private Transform playerTransform;
    private Transform playerCameraTransform;
    private VehicleInteraction vehicleInteraction;
    private Transform vehicleTransform;
    private RenderTexture minimapRT;

    /// <summary>
    /// Radio actual del mundo abarcado por la cámara (Size ortográfico).
    /// </summary>
    public float CurrentWorldRadius
    {
        get
        {
            if (minimapCamera != null && minimapCamera.orthographic && minimapCamera.orthographicSize > 0f)
                return minimapCamera.orthographicSize;
            return worldRadius > 0f ? worldRadius : 20f;
        }
    }

    private void Reset()
    {
        if (minimapCamera == null)
            minimapCamera = GetComponent<Camera>();
        FindUIReferences();
    }

    private void Awake()
    {
        if (minimapCamera == null)
            minimapCamera = GetComponent<Camera>();

        if (minimapCamera == null)
        {
            GameObject camGO = GameObject.Find("MinimapCamera");
            if (camGO != null) minimapCamera = camGO.GetComponent<Camera>();
        }

        FindUIReferences();

        // Sincronizar el radio con el Size real de la cámara (sin sobreescribir la cámara)
        if (minimapCamera != null)
        {
            if (minimapCamera.orthographic && minimapCamera.orthographicSize > 0f)
            {
                worldRadius = minimapCamera.orthographicSize;
            }
            if (minimapCamera.transform.position.y > 0f)
            {
                cameraHeight = minimapCamera.transform.position.y;
            }
        }
    }

    void Start()
    {
        FindGameReferences();
        SetupRenderTexture();
    }

    void LateUpdate()
    {
        if (deliveryManager == null || playerTransform == null || vehicleInteraction == null)
            FindGameReferences();

        // Mantener worldRadius sincronizado con la cámara en tiempo real
        if (minimapCamera != null && minimapCamera.orthographic)
        {
            worldRadius = minimapCamera.orthographicSize;
        }

        UpdateCameraPosition();
        UpdatePlayerIcon();
        UpdateObjectiveMarker();
    }

    void OnDestroy()
    {
        if (minimapRT != null)
        {
            minimapRT.Release();
            Destroy(minimapRT);
        }
    }

    private void FindUIReferences()
    {
        if (minimapView == null)
        {
            GameObject viewGO = GameObject.Find("MinimapView");
            if (viewGO != null) minimapView = viewGO.GetComponent<RawImage>();
        }
        if (playerIcon == null)
        {
            GameObject iconGO = GameObject.Find("PlayerIcon");
            if (iconGO != null) playerIcon = iconGO.GetComponent<RectTransform>();
        }
        if (objectiveMarker == null)
        {
            GameObject markerGO = GameObject.Find("ObjectiveMarker");
            if (markerGO != null) objectiveMarker = markerGO.GetComponent<RectTransform>();
        }
    }

    private void FindGameReferences()
    {
        if (deliveryManager == null)
            deliveryManager = Object.FindAnyObjectByType<FoodDeliveryManager>();

        if (playerTransform == null)
        {
            PlayerPickup pp = Object.FindAnyObjectByType<PlayerPickup>();
            if (pp != null) playerTransform = pp.transform;
        }

        if (playerCameraTransform == null)
        {
            if (Camera.main != null)
            {
                playerCameraTransform = Camera.main.transform;
            }
            else if (playerTransform != null)
            {
                Camera cam = playerTransform.GetComponentInChildren<Camera>(true);
                if (cam != null) playerCameraTransform = cam.transform;
            }
        }

        if (vehicleInteraction == null)
            vehicleInteraction = Object.FindAnyObjectByType<VehicleInteraction>();

        if (vehicleTransform == null && vehicleInteraction != null)
            vehicleTransform = vehicleInteraction.transform;
    }

    private void SetupRenderTexture()
    {
        if (minimapCamera == null) return;

        if (minimapRT == null)
        {
            int rtResolution = renderTextureSize > 0 ? renderTextureSize : 512;
            minimapRT = new RenderTexture(rtResolution, rtResolution, 16, RenderTextureFormat.ARGB32);
            minimapRT.name = "MinimapRT_Runtime";
            minimapRT.filterMode = FilterMode.Bilinear;
            minimapRT.Create();
        }

        minimapCamera.targetTexture = minimapRT;
        minimapCamera.orthographic = true;

        // El usuario tiene control total sobre el Size de la cámara en el Inspector.
        worldRadius = minimapCamera.orthographicSize;

        minimapCamera.depth = -2;
        minimapCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));

        UniversalAdditionalCameraData urpData = minimapCamera.GetUniversalAdditionalCameraData();
        if (urpData != null)
        {
            urpData.renderType = CameraRenderType.Base;
            urpData.renderShadows = false;
            urpData.requiresColorTexture = false;
            urpData.requiresDepthTexture = false;
            urpData.antialiasing = AntialiasingMode.None;
        }

        if (minimapView != null)
            minimapView.texture = minimapRT;
    }

    private void UpdateCameraPosition()
    {
        if (minimapCamera == null) return;

        Transform tracked = GetTrackedTransform();
        if (tracked == null) return;

        minimapCamera.transform.position = tracked.position + Vector3.up * cameraHeight;

        if (rotateWithPlayer)
        {
            float heading = GetTrackedHeading();
            minimapCamera.transform.rotation = Quaternion.Euler(90f, heading, 0f);
        }
        else
        {
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    private void UpdatePlayerIcon()
    {
        if (playerIcon == null) return;

        if (rotateWithPlayer)
        {
            // Al rotar la cámara con el heading, el jugador siempre apunta hacia ARRIBA en el minimapa
            playerIcon.localRotation = Quaternion.identity;
        }
        else
        {
            float yRot = GetTrackedHeading();
            playerIcon.localRotation = Quaternion.Euler(0f, 0f, -yRot);
        }
    }

    private void UpdateObjectiveMarker()
    {
        if (objectiveMarker == null) return;

        bool hasObjective = deliveryManager != null
            && deliveryManager.CurrentDestination != null
            && deliveryManager.CurrentState != FoodDeliveryManager.DeliveryState.Idle
            && deliveryManager.CurrentState != FoodDeliveryManager.DeliveryState.Completed;

        objectiveMarker.gameObject.SetActive(hasObjective);
        if (!hasObjective) return;

        Transform tracked = GetTrackedTransform();
        if (tracked == null) return;

        Vector3 toTarget = deliveryManager.CurrentDestination.transform.position - tracked.position;

        Vector2 localDir;
        if (rotateWithPlayer)
        {
            float heading = GetTrackedHeading();
            // Convertir el vector del mundo al espacio relativo de orientación del minimapa
            Vector3 rel3D = Quaternion.Euler(0f, -heading, 0f) * toTarget;
            localDir = new Vector2(rel3D.x, rel3D.z);
        }
        else
        {
            localDir = new Vector2(toTarget.x, toTarget.z);
        }

        // Radio en el mundo: se obtiene directamente del Size de la cámara
        float radius = CurrentWorldRadius;
        if (radius < 0.001f) radius = 20f;

        Vector2 normalized = localDir / radius;

        // Clampear al borde del círculo si el objetivo está fuera
        normalized = Vector2.ClampMagnitude(normalized, 1f);

        // Obtener el radio en píxeles de la UI
        float effectiveUiRadius = GetEffectiveUiRadius();

        // En espacio local del minimapa:
        // +X = derecha
        // +Y = arriba (frente)
        objectiveMarker.anchoredPosition = new Vector2(normalized.x, normalized.y) * effectiveUiRadius;
    }

    /// <summary>
    /// Calcula el radio en píxeles del círculo de la UI.
    /// Si uiRadius > 0 se usa dicho valor; de lo contrario se calcula automáticamente
    /// a partir del tamaño del contenedor para adaptarse a cualquier resolución o escala de UI.
    /// </summary>
    public float GetEffectiveUiRadius()
    {
        if (uiRadius > 0f) return uiRadius;

        if (objectiveMarker != null && objectiveMarker.parent != null)
        {
            RectTransform parentRT = objectiveMarker.parent as RectTransform;
            if (parentRT != null)
            {
                float markerHalf = objectiveMarker.rect.width * 0.5f;
                return Mathf.Max(10f, (parentRT.rect.width * 0.5f) - markerHalf - 6f);
            }
        }

        return 120f;
    }

    /// <summary>
    /// Obtiene el ángulo de orientación (heading) en grados.
    /// Si el jugador está dentro del auto: orientación del chasis del auto.
    /// Si está a pie en primera persona: hacia donde está mirando el jugador.
    /// </summary>
    private float GetTrackedHeading()
    {
        // 1. Si está en el vehículo: hacia donde apunta el auto
        if (vehicleInteraction != null && vehicleInteraction.PlayerIsInside && vehicleTransform != null)
        {
            return vehicleTransform.eulerAngles.y;
        }

        // 2. Si está en primera persona: hacia donde está viendo el jugador
        if (playerCameraTransform != null)
        {
            return playerCameraTransform.eulerAngles.y;
        }

        if (playerTransform != null)
        {
            return playerTransform.eulerAngles.y;
        }

        return 0f;
    }

    private Transform GetTrackedTransform()
    {
        if (vehicleInteraction != null && vehicleInteraction.PlayerIsInside && vehicleTransform != null)
            return vehicleTransform;
        return playerTransform;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (minimapCamera == null)
            minimapCamera = GetComponent<Camera>();

        // En el editor: NO SOBREESCRIBIR minimapCamera.orthographicSize bajo ninguna circunstancia.
        if (minimapCamera != null && minimapCamera.orthographic && minimapCamera.orthographicSize > 0f)
        {
            worldRadius = minimapCamera.orthographicSize;
        }
    }
#endif
}
