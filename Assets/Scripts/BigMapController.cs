using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controlador del mapa grande rectangular estilo GTA para A-Toda-Olla.
///
/// CARACTERÍSTICAS:
///   - Se abre y cierra al presionar la tecla [M] (o Escape / botón Select en mando).
///   - Cubre el 90% de la pantalla de forma responsiva en cualquier resolución.
///   - Vista rectangular panorámica completa de toda la ciudad (cámara ortográfica).
///   - El juego continúa en tiempo real de fondo con marco translúcido (puedes conducir mientras ves el mapa).
///   - Muestra el icono del jugador con su posición y orientación en tiempo real.
///   - Muestra el marcador de objetivo (cuadrado amarillo estilo GTA) en la casa de entrega activa.
///   - Oculta el minimapa circular de la esquina mientras el mapa grande está abierto.
///   - La cámara del mapa grande se desactiva cuando el mapa está cerrado (0% coste de GPU al jugar normalmente).
/// </summary>
public class BigMapController : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────────
    //  INSPECTOR
    // ──────────────────────────────────────────────────────────────────────

    [Header("Cámara del Mapa Grande")]
    [Tooltip("Cámara ortográfica cenital para el mapa grande.")]
    public Camera bigMapCamera;

    [Tooltip("Posición central de la cámara (centro de la ciudad).")]
    public Vector3 cityCenter = new Vector3(0f, 250f, 0f);

    [Tooltip("Tamaño ortográfico (zoom out) para abarcar toda la ciudad completa.")]
    public float orthographicSize = 205f;

    [Header("Referencias de UI")]
    [Tooltip("CanvasGroup del panel raíz del mapa grande.")]
    public CanvasGroup mapPanelGroup;

    [Tooltip("RawImage donde se proyecta la RenderTexture del mapa grande.")]
    public RawImage bigMapView;

    [Tooltip("RectTransform del icono del jugador (flecha/triángulo).")]
    public RectTransform playerIcon;

    [Tooltip("RectTransform del marcador de entrega (cuadrado amarillo con borde negro).")]
    public RectTransform objectiveMarker;

    [Tooltip("Contenedor del minimapa circular de la esquina (se oculta al abrir el mapa grande).")]
    public RectTransform minimapContainer;

    [Header("Render Texture")]
    [Tooltip("Ancho de la textura del mapa en píxeles.")]
    public int textureWidth = 1920;

    [Tooltip("Alto de la textura del mapa en píxeles.")]
    public int textureHeight = 1080;

    [Header("Animación")]
    [Tooltip("Duración del desvanecimiento (fade in/out) al abrir y cerrar el mapa.")]
    public float fadeDuration = 0.15f;

    // ──────────────────────────────────────────────────────────────────────
    //  ESTADO INTERNO
    // ──────────────────────────────────────────────────────────────────────

    private bool isOpen = false;
    public bool IsOpen => isOpen;

    private RenderTexture bigMapRT;
    private Coroutine fadeCoroutine;

    // Dimensiones de la ciudad
    private float cityHalfWidth = 200f;
    private float cityHalfHeight = 180f;
    private bool hasCityBounds = false;

    // Referencias al juego
    private FoodDeliveryManager deliveryManager;
    private Transform playerTransform;
    private VehicleInteraction vehicleInteraction;
    private Transform vehicleTransform;

    // ──────────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (bigMapCamera == null)
            bigMapCamera = GetComponent<Camera>();

        if (bigMapCamera == null)
        {
            GameObject camGO = GameObject.Find("BigMapCamera");
            if (camGO != null) bigMapCamera = camGO.GetComponent<Camera>();
        }

        FindUIReferences();
        SetupRenderTexture();
        CalculateCityBounds();

        // Al iniciar, el mapa grande arranca cerrado y la cámara apagada para no gastar GPU
        if (mapPanelGroup != null)
        {
            mapPanelGroup.alpha = 0f;
            mapPanelGroup.interactable = false;
            mapPanelGroup.blocksRaycasts = false;
            mapPanelGroup.gameObject.SetActive(false);
        }

        if (bigMapCamera != null)
        {
            bigMapCamera.transform.position = cityCenter;
            bigMapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            bigMapCamera.orthographic = true;
            bigMapCamera.orthographicSize = orthographicSize;
            bigMapCamera.enabled = false; // Desactivar renderizado mientras esté cerrado
        }
    }

    private void Start()
    {
        FindGameReferences();
    }

    private void Update()
    {
        // 1. Detección de entrada con el nuevo Input System (Tecla M o Escape)
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.mKey.wasPressedThisFrame)
            {
                ToggleMap();
            }
            else if (isOpen && kb.escapeKey.wasPressedThisFrame)
            {
                CloseMap();
            }
        }

        // 2. Soporte para mando (botón Select / View)
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.selectButton.wasPressedThisFrame)
            {
                ToggleMap();
            }
        }
    }

    private void LateUpdate()
    {
        if (!isOpen) return;

        FindGameReferences();
        UpdateMarkers();
    }

    private void OnDestroy()
    {
        if (bigMapRT != null)
        {
            bigMapRT.Release();
            Destroy(bigMapRT);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  APERTURA / CIERRE DEL MAPA
    // ──────────────────────────────────────────────────────────────────────

    public void ToggleMap()
    {
        if (isOpen)
            CloseMap();
        else
            OpenMap();
    }

    public void OpenMap()
    {
        if (isOpen) return;
        isOpen = true;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        if (!hasCityBounds) CalculateCityBounds();

        // Activar la cámara del mapa grande y ajustar proporción
        if (bigMapCamera != null)
        {
            bigMapCamera.transform.position = cityCenter;
            bigMapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Ajustar aspect ratio dinámico según el tamaño real de la pantalla
            if (bigMapView != null)
            {
                Rect r = bigMapView.rectTransform.rect;
                if (r.width > 10f && r.height > 10f)
                {
                    float aspect = r.width / r.height;
                    bigMapCamera.aspect = aspect;

                    if (hasCityBounds)
                    {
                        float requiredForWidth = cityHalfWidth / aspect;
                        orthographicSize = Mathf.Max(cityHalfHeight, requiredForWidth) * 1.06f;
                    }
                }
            }

            bigMapCamera.orthographicSize = orthographicSize;
            bigMapCamera.enabled = true;
        }

        // Ocultar el minimapa pequeño circular de la esquina
        if (minimapContainer != null)
        {
            minimapContainer.gameObject.SetActive(false);
        }

        // Activar el panel UI
        if (mapPanelGroup != null)
        {
            mapPanelGroup.gameObject.SetActive(true);
            mapPanelGroup.alpha = 0f;
        }

        FindGameReferences();
        UpdateMarkers();

        fadeCoroutine = StartCoroutine(FadeRoutine(1f, isOpening: true));
    }

    public void CloseMap()
    {
        if (!isOpen) return;
        isOpen = false;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(0f, isOpening: false));
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool isOpening)
    {
        if (mapPanelGroup == null) yield break;

        float startAlpha = mapPanelGroup.alpha;
        float elapsed = 0f;
        float duration = fadeDuration > 0.01f ? fadeDuration : 0.15f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            mapPanelGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        mapPanelGroup.alpha = targetAlpha;

        if (!isOpening)
        {
            // Apagar la cámara para que no gaste GPU
            if (bigMapCamera != null) bigMapCamera.enabled = false;
            if (mapPanelGroup != null) mapPanelGroup.gameObject.SetActive(false);

            // Restaurar el minimapa circular
            if (minimapContainer != null) minimapContainer.gameObject.SetActive(true);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  PROYECCIÓN Y ACTUALIZACIÓN DE MARCADORES
    // ──────────────────────────────────────────────────────────────────────

    private void UpdateMarkers()
    {
        if (bigMapCamera == null || bigMapView == null) return;

        RectTransform viewRect = bigMapView.rectTransform;
        float mapW = viewRect.rect.width > 10f ? viewRect.rect.width : 1600f;
        float mapH = viewRect.rect.height > 10f ? viewRect.rect.height : 900f;

        // 1. Icono del Jugador / Vehículo
        Transform tracked = GetTrackedTransform();
        if (tracked != null && playerIcon != null)
        {
            Vector3 vp = bigMapCamera.WorldToViewportPoint(tracked.position);
            float uiX = (Mathf.Clamp01(vp.x) - 0.5f) * mapW;
            float uiY = (Mathf.Clamp01(vp.y) - 0.5f) * mapH;
            playerIcon.anchoredPosition = new Vector2(uiX, uiY);

            float yRot = tracked.eulerAngles.y;
            playerIcon.localRotation = Quaternion.Euler(0f, 0f, -yRot);
        }

        // 2. Marcador de Objetivo (Casa de entrega activa)
        if (objectiveMarker != null)
        {
            bool hasObjective = deliveryManager != null
                && deliveryManager.CurrentDestination != null
                && deliveryManager.CurrentState != FoodDeliveryManager.DeliveryState.Idle
                && deliveryManager.CurrentState != FoodDeliveryManager.DeliveryState.Completed;

            objectiveMarker.gameObject.SetActive(hasObjective);

            if (hasObjective)
            {
                Vector3 destPos = deliveryManager.CurrentDestination.transform.position;
                Vector3 vp = bigMapCamera.WorldToViewportPoint(destPos);
                float uiX = (Mathf.Clamp01(vp.x) - 0.5f) * mapW;
                float uiY = (Mathf.Clamp01(vp.y) - 0.5f) * mapH;
                objectiveMarker.anchoredPosition = new Vector2(uiX, uiY);

                // Pulso sutil estilo GTA
                float pulse = 1f + Mathf.PingPong(Time.unscaledTime * 2.5f, 0.22f);
                objectiveMarker.localScale = new Vector3(pulse, pulse, 1f);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  CONFIGURACIÓN Y REFERENCIAS
    // ──────────────────────────────────────────────────────────────────────

    public void CalculateCityBounds()
    {
        GameObject city = GameObject.Find("City");
        if (city != null)
        {
            Renderer[] rends = city.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++)
                    b.Encapsulate(rends[i].bounds);

                cityCenter = new Vector3(b.center.x, 250f, b.center.z);
                cityHalfWidth = b.extents.x;
                cityHalfHeight = b.extents.z;
                hasCityBounds = true;
            }
        }
    }

    private void SetupRenderTexture()
    {
        if (bigMapCamera == null) return;

        if (bigMapRT == null)
        {
            int w = textureWidth > 0 ? textureWidth : 1920;
            int h = textureHeight > 0 ? textureHeight : 1080;
            bigMapRT = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
            bigMapRT.name = "BigMapRT_Runtime";
            bigMapRT.filterMode = FilterMode.Bilinear;
            bigMapRT.Create();
        }

        bigMapCamera.targetTexture = bigMapRT;
        bigMapCamera.orthographic = true;
        bigMapCamera.orthographicSize = orthographicSize;
        bigMapCamera.depth = -3;
        bigMapCamera.clearFlags = CameraClearFlags.SolidColor;
        bigMapCamera.backgroundColor = new Color(0.22f, 0.20f, 0.18f, 1f);
        bigMapCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));

        UniversalAdditionalCameraData urpData = bigMapCamera.GetUniversalAdditionalCameraData();
        if (urpData != null)
        {
            urpData.renderType = CameraRenderType.Base;
            urpData.renderShadows = false;
            urpData.requiresColorTexture = false;
            urpData.requiresDepthTexture = false;
            urpData.antialiasing = AntialiasingMode.None;
        }

        if (bigMapView != null)
        {
            bigMapView.texture = bigMapRT;
        }
    }

    private void FindUIReferences()
    {
        if (mapPanelGroup == null)
        {
            Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                if (c.name == "HUDCanvas")
                {
                    Transform t = c.transform.Find("BigMapPanel");
                    if (t != null)
                    {
                        mapPanelGroup = t.GetComponent<CanvasGroup>();
                        break;
                    }
                }
            }
            if (mapPanelGroup == null)
            {
                var allGroups = Resources.FindObjectsOfTypeAll<CanvasGroup>();
                foreach (var cg in allGroups)
                {
                    if (cg.gameObject.name == "BigMapPanel") { mapPanelGroup = cg; break; }
                }
            }
        }

        if (mapPanelGroup != null)
        {
            if (bigMapView == null)
            {
                Transform vt = mapPanelGroup.transform.Find("BigMapView");
                if (vt != null) bigMapView = vt.GetComponent<RawImage>();
            }

            if (playerIcon == null)
            {
                Transform it = mapPanelGroup.transform.Find("BigMapPlayerIcon");
                if (it != null) playerIcon = it.GetComponent<RectTransform>();
            }

            if (objectiveMarker == null)
            {
                Transform mt = mapPanelGroup.transform.Find("BigMapObjectiveMarker");
                if (mt != null) objectiveMarker = mt.GetComponent<RectTransform>();
            }
        }

        if (minimapContainer == null)
        {
            Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                if (c.name == "HUDCanvas")
                {
                    Transform t = c.transform.Find("MinimapContainer");
                    if (t != null) { minimapContainer = t.GetComponent<RectTransform>(); break; }
                }
            }
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

        if (vehicleInteraction == null)
            vehicleInteraction = Object.FindAnyObjectByType<VehicleInteraction>();

        if (vehicleTransform == null && vehicleInteraction != null)
            vehicleTransform = vehicleInteraction.transform;
    }

    private Transform GetTrackedTransform()
    {
        if (vehicleInteraction != null && vehicleInteraction.PlayerIsInside && vehicleTransform != null)
            return vehicleTransform;
        return playerTransform;
    }
}
