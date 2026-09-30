using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tooltip de peso en World Space.
/// Usa detección por PROXIMIDAD + CONO de visión, más generosa que el raycast
/// del PlayerPickup (que requiere apuntar exactamente al objeto).
///
/// SETUP:
///  1. Canvas → Render Mode: World Space   |  Scale: 0.003, 0.003, 0.003
///  2. Adjunta este script al Canvas (o a cualquier hijo).
///  3. Asigna tooltipContainer → RectTransform de WeightTooltip.
///  4. Asigna weightTextTMP   → TMP_Text de weightTextTMP.
/// </summary>
public class ItemWeightTooltip : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("RectTransform del panel/burbuja que flota en el mundo.")]
    public RectTransform tooltipContainer;

    [Tooltip("TMP_Text donde se mostrará el peso.")]
    public TMP_Text weightTextTMP;

    [Tooltip("Text legacy (si no usas TMP).")]
    public UnityEngine.UI.Text weightTextLegacy;

    [Header("Detección de Proximidad")]
    [Tooltip("Radio de detección alrededor del jugador (metros).")]
    public float detectionRadius = 3.5f;

    [Tooltip("Ángulo del cono de visión. 360 = detecta en todas direcciones.\n" +
             "60 = requiere mirar aproximadamente hacia el objeto.\n" +
             "Recomendado: 90-120 para que sea generoso sin ser omnisciente.")]
    [Range(10f, 360f)]
    public float detectionAngle = 110f;

    [Tooltip("Capas donde se buscan objetos recogibles.")]
    public LayerMask itemLayerMask = ~0;

    [Header("Posición en el Mundo")]
    [Tooltip("Metros por encima del punto más alto del objeto.")]
    public float heightAboveObject = 0.35f;

    [Tooltip("Desplazamiento extra en el eje Y del mundo (refinado).")]
    public float extraYOffset = 0f;

    [Header("Animación")]
    [Tooltip("Velocidad con que el globo sigue al objeto.")]
    public float followSpeed = 18f;

    [Tooltip("Velocidad de la animación de aparición / desaparición.")]
    public float scaleSpeed = 14f;

    [Header("Formato de Texto")]
    public bool   showItemName = false;
    public string weightFormat = "{0:0.#} kg";

    // ── Privadas ──────────────────────────────────────────────────────────
    private PlayerPickup playerPickup;
    private Camera       activeCamera;
    private Transform    playerTransform;
    private VanDoorController vanDoorController;

    private Vector3 targetScale    = Vector3.zero;
    private Vector3 targetWorldPos = Vector3.zero;
    private bool    hasTargetPos   = false;

    // Buffer para OverlapSphere sin GC
    private readonly Collider[] _hitBuffer = new Collider[16];

    // ─────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (tooltipContainer == null)
            tooltipContainer = GetComponent<RectTransform>();

        if (weightTextTMP == null && tooltipContainer != null)
            weightTextTMP = tooltipContainer.GetComponentInChildren<TMP_Text>(true);

        if (weightTextLegacy == null && tooltipContainer != null)
            weightTextLegacy = tooltipContainer.GetComponentInChildren<UnityEngine.UI.Text>(true);

        if (tooltipContainer != null)
            tooltipContainer.localScale = Vector3.zero;
    }

    void Start()
    {
        FindPlayerAndCamera();
    }

    void LateUpdate()
    {
        // ── 1. Mantener referencias vivas ─────────────────────────────────
        if (playerPickup == null || activeCamera == null)
        {
            FindPlayerAndCamera();
            if (playerPickup == null || activeCamera == null)
            {
                HideImmediate();
                return;
            }
        }

        // ── 2. Nunca mostrar si el jugador lleva algo en brazos ───────────
        if (playerPickup.IsCarryingItem)
        {
            targetScale  = Vector3.zero;
            hasTargetPos = false;
        }
        else
        {
            // ── 3. Detectar el ítem más cercano en el cono de visión ──────
            PickableItem item = FindBestItemInCone();

            if (item != null)
            {
                UpdateText(item);
                targetWorldPos = GetWorldTargetPos(item);
                hasTargetPos   = true;
                targetScale    = Vector3.one;
            }
            else
            {
                targetScale  = Vector3.zero;
                hasTargetPos = false;
            }
        }

        if (tooltipContainer == null) return;

        // ── 4. Animar escala (pop) ────────────────────────────────────────
        tooltipContainer.localScale = Vector3.Lerp(
            tooltipContainer.localScale,
            targetScale,
            Time.deltaTime * scaleSpeed
        );

        // ── 5. Mover y orientar en el mundo ──────────────────────────────
        if (hasTargetPos && targetScale.x > 0.01f)
        {
            tooltipContainer.position = Vector3.Lerp(
                tooltipContainer.position,
                targetWorldPos,
                Time.deltaTime * followSpeed
            );

            // Billboard: siempre mirando hacia la cámara
            Vector3 dirToCamera = activeCamera.transform.position - tooltipContainer.position;
            if (dirToCamera != Vector3.zero)
                tooltipContainer.rotation = Quaternion.LookRotation(-dirToCamera);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Busca el PickableItem más cercano dentro del radio y el cono de visión
    /// de la cámara del jugador. No requiere apuntar exactamente.
    /// </summary>
    private PickableItem FindBestItemInCone()
    {
        if (playerTransform == null) return null;

        Vector3 origin    = activeCamera.transform.position;
        Vector3 forward   = activeCamera.transform.forward;
        float   halfAngle = detectionAngle * 0.5f;

        int count = Physics.OverlapSphereNonAlloc(
            origin, detectionRadius, _hitBuffer, itemLayerMask,
            QueryTriggerInteraction.Collide
        );

        PickableItem bestItem  = null;
        float        bestScore = float.MaxValue; // menor ángulo = mejor

        if (vanDoorController == null) vanDoorController = Object.FindFirstObjectByType<VanDoorController>();

        for (int i = 0; i < count; i++)
        {
            PickableItem item = _hitBuffer[i].GetComponentInParent<PickableItem>();
            if (item == null || item.IsBeingCarried) continue;

            // Si está dentro de la van, solo mostrar el globo si la puerta trasera está abierta
            if (item.IsStoredInCargo)
            {
                if (vanDoorController != null && !vanDoorController.IsOpen) continue;
            }

            Vector3 toItem = (item.transform.position - origin).normalized;
            float   angle  = Vector3.Angle(forward, toItem);

            // ¿Está dentro del cono?
            if (angle <= halfAngle && angle < bestScore)
            {
                bestScore = angle;
                bestItem  = item;
            }
        }

        return bestItem;
    }

    // ─── Posición 3D encima del objeto ────────────────────────────────────
    private Vector3 GetWorldTargetPos(PickableItem item)
    {
        Vector3  worldPos = item.transform.position;
        Collider col      = item.GetComponentInChildren<Collider>();
        float    topY     = col != null ? col.bounds.max.y : worldPos.y + 0.35f;

        return new Vector3(worldPos.x, topY + heightAboveObject + extraYOffset, worldPos.z);
    }

    // ─── Actualizar texto ─────────────────────────────────────────────────
    private void UpdateText(PickableItem item)
    {
        string txt = showItemName
            ? $"{item.itemName}\n{string.Format(weightFormat, item.weightKg)}"
            : string.Format(weightFormat, item.weightKg);

        if (weightTextTMP != null)             weightTextTMP.text    = txt;
        else if (weightTextLegacy != null)     weightTextLegacy.text = txt;
    }

    // ─── Ocultar inmediatamente ───────────────────────────────────────────
    private void HideImmediate()
    {
        if (tooltipContainer != null)
            tooltipContainer.localScale = Vector3.zero;
        hasTargetPos = false;
    }

    // ─── Buscar Player y Cámara ───────────────────────────────────────────
    private void FindPlayerAndCamera()
    {
        if (playerPickup == null)
        {
            playerPickup = Object.FindFirstObjectByType<PlayerPickup>();
            if (playerPickup == null)
            {
                GameObject go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) playerPickup = go.GetComponent<PlayerPickup>();
            }
        }

        if (playerPickup != null)
            playerTransform = playerPickup.transform;

        if (activeCamera == null || !activeCamera.isActiveAndEnabled)
        {
            if (playerPickup != null)
                activeCamera = playerPickup.GetComponentInChildren<Camera>(false);

            if (activeCamera == null) activeCamera = Camera.main;
            if (activeCamera == null) activeCamera = Object.FindFirstObjectByType<Camera>();
        }
    }

#if UNITY_EDITOR
    // ─── Gizmo para visualizar el cono de detección ───────────────────────
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || activeCamera == null) return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
        Gizmos.DrawWireSphere(activeCamera.transform.position, detectionRadius);

        // Dibujar los bordes del cono
        float halfAngle = detectionAngle * 0.5f * Mathf.Deg2Rad;
        Vector3 fwd     = activeCamera.transform.forward * detectionRadius;
        Vector3 right   = Quaternion.Euler(0,  detectionAngle * 0.5f, 0) * activeCamera.transform.forward * detectionRadius;
        Vector3 left    = Quaternion.Euler(0, -detectionAngle * 0.5f, 0) * activeCamera.transform.forward * detectionRadius;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
        Gizmos.DrawRay(activeCamera.transform.position, fwd);
        Gizmos.DrawRay(activeCamera.transform.position, right);
        Gizmos.DrawRay(activeCamera.transform.position, left);
    }
#endif
}
