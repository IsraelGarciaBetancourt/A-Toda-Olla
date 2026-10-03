using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Punto de mira (Crosshair / Retícula) minimalista en el centro de la pantalla.
/// 
/// Características:
///  - Solo visible en primera persona (se oculta automáticamente al conducir el vehículo).
///  - Crea su propio sprite circular suave sin necesidad de importar imágenes externas.
///  - Incluye un contorno sutil oscuro para verse claramente sobre fondos brillantes (cielo, luces).
///  - Opcional: Reacciona ligeramente (cambio de tamaño o color) al apuntar a objetos recogibles o interactuables.
/// </summary>
public class FirstPersonCrosshair : MonoBehaviour
{
    [Header("Configuración Visual")]
    [Tooltip("Tamaño del punto en píxeles (diámetro). 4 a 6 suele ser el tamaño ideal.")]
    [Range(2f, 16f)]
    public float dotSize = 5f;

    [Tooltip("Color base del punto blanco.")]
    public Color dotColor = new Color(1f, 1f, 1f, 0.95f);

    [Tooltip("Añadir contorno sutil oscuro para que no se pierda contra fondos blancos.")]
    public bool useOutline = true;

    [Tooltip("Color del contorno.")]
    public Color outlineColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("Comportamiento")]
    [Tooltip("¿Ocultar automáticamente la retícula mientras el jugador conduce el vehículo?")]
    public bool hideWhileDriving = true;

    [Tooltip("Velocidad de transición al aparecer/desaparecer o cambiar de escala.")]
    public float animationSpeed = 12f;

    [Header("Feedback al Apuntar Objetos")]
    [Tooltip("¿Cambiar el punto al mirar un objeto recogible o interactuable?")]
    public bool highlightOnInteractable = true;

    [Tooltip("Tamaño del punto al apuntar a algo interactuable.")]
    [Range(4f, 20f)]
    public float targetHoverSize = 8f;

    [Tooltip("Color del punto al apuntar a un objeto recogible (ej: verde suave, amarillo o blanco brillante).")]
    public Color hoverColor = new Color(0.35f, 1f, 0.6f, 1f); // Verde menta suave

    [Header("Referencias Opcionales (Se auto-buscan si están vacías)")]
    public Canvas targetCanvas;
    public Image crosshairImage;

    // Referencias internas
    private RectTransform rectTransform;
    private Outline outlineComponent;
    private CanvasGroup canvasGroup;
    private VehicleInteraction vehicle;
    private PlayerPickup playerPickup;

    private float currentScale = 1f;
    private float targetScale = 1f;
    private Color currentColor;
    private Color targetCurrentColor;
    private float targetAlpha = 1f;

    private static Sprite cachedCircleSprite;

    void Awake()
    {
        currentColor = dotColor;
        targetCurrentColor = dotColor;

        EnsureCrosshairUI();
    }

    void Start()
    {
        FindGameReferences();
    }

    void Update()
    {
        EnsureReferences();
        EvaluateVisibilityAndState();
        UpdateAnimations();
    }

    /// <summary>
    /// Garantiza la existencia del Canvas y el GameObject UI Image con su sprite circular.
    /// </summary>
    private void EnsureCrosshairUI()
    {
        // 1. Si no hay Image asignada, buscar en hijos o en el Canvas existente
        if (crosshairImage == null)
        {
            // Buscar si ya existe un objeto llamado "FirstPersonCrosshair" en el Canvas
            if (targetCanvas == null)
            {
                targetCanvas = FindTargetCanvas();
            }

            if (targetCanvas != null)
            {
                Transform existingDot = targetCanvas.transform.Find("FirstPersonCrosshair");
                if (existingDot != null)
                {
                    crosshairImage = existingDot.GetComponent<Image>();
                }
            }
        }

        // 2. Si todavía no existe, crearlo automáticamente
        if (crosshairImage == null)
        {
            if (targetCanvas == null)
            {
                targetCanvas = FindTargetCanvas();
            }

            if (targetCanvas != null)
            {
                GameObject dotGO = new GameObject("FirstPersonCrosshair", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                dotGO.transform.SetParent(targetCanvas.transform, false);

                crosshairImage = dotGO.GetComponent<Image>();
            }
            else
            {
                Debug.LogWarning("[FirstPersonCrosshair] No se encontró ningún Canvas en la escena. Asegúrate de tener un HUDCanvas.");
                return;
            }
        }

        // Configurar RectTransform centrado
        rectTransform = crosshairImage.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(dotSize, dotSize);

        // Desactivar RaycastTarget para no interferir con clicks del mouse
        crosshairImage.raycastTarget = false;

        // Asignar Sprite circular suave anti-aliased
        if (crosshairImage.sprite == null)
        {
            crosshairImage.sprite = GetOrCreateCircleSprite();
        }

        crosshairImage.color = dotColor;

        // Configurar CanvasGroup para fading limpio
        canvasGroup = crosshairImage.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = crosshairImage.gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        // Configurar Outline para alto contraste
        if (useOutline)
        {
            outlineComponent = crosshairImage.GetComponent<Outline>();
            if (outlineComponent == null)
            {
                outlineComponent = crosshairImage.gameObject.AddComponent<Outline>();
            }
            outlineComponent.effectColor = outlineColor;
            outlineComponent.effectDistance = new Vector2(1f, -1f);
            outlineComponent.enabled = true;
        }
    }

    /// <summary>
    /// Determina si el punto debe verse y si está interactuando con algo.
    /// </summary>
    private void EvaluateVisibilityAndState()
    {
        // 1. Visibilidad: Ocultar si está conduciendo
        bool isDriving = (vehicle != null && vehicle.PlayerIsInside);
        
        // También comprobar si el jugador está deshabilitado
        bool playerActive = (playerPickup != null && playerPickup.gameObject.activeInHierarchy);

        if (hideWhileDriving && (isDriving || !playerActive))
        {
            targetAlpha = 0f;
            return;
        }

        targetAlpha = 1f;

        // 2. Feedback de interacción
        bool isHoveringItem = highlightOnInteractable 
            && playerPickup != null 
            && playerPickup.HoveredItem != null 
            && !playerPickup.IsCarryingItem;

        if (isHoveringItem)
        {
            targetScale = targetHoverSize / Mathf.Max(1f, dotSize);
            targetCurrentColor = hoverColor;
        }
        else
        {
            targetScale = 1f;
            targetCurrentColor = dotColor;
        }
    }

    /// <summary>
    /// Anima suavemente la opacidad, escala y color.
    /// </summary>
    private void UpdateAnimations()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * animationSpeed);
        }

        if (rectTransform != null)
        {
            currentScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * animationSpeed);
            rectTransform.sizeDelta = new Vector2(dotSize * currentScale, dotSize * currentScale);
        }

        if (crosshairImage != null)
        {
            currentColor = Color.Lerp(currentColor, targetCurrentColor, Time.deltaTime * animationSpeed);
            crosshairImage.color = currentColor;
        }
    }

    private void EnsureReferences()
    {
        if (vehicle == null || playerPickup == null)
        {
            FindGameReferences();
        }
    }

    private void FindGameReferences()
    {
        if (vehicle == null) vehicle = Object.FindAnyObjectByType<VehicleInteraction>();
        if (playerPickup == null) playerPickup = Object.FindAnyObjectByType<PlayerPickup>();
    }

    private Canvas FindTargetCanvas()
    {
        // Buscar preferiblemente el Canvas HUD
        GameObject hudGO = GameObject.Find("HUDCanvas");
        if (hudGO != null)
        {
            Canvas c = hudGO.GetComponent<Canvas>();
            if (c != null) return c;
        }

        // Si no, buscar cualquier Canvas en escena
        return Object.FindAnyObjectByType<Canvas>();
    }

    /// <summary>
    /// Genera en tiempo de ejecución un sprite circular anti-aliased de alta definición
    /// para no depender de importar assets o texturas externas.
    /// </summary>
    private static Sprite GetOrCreateCircleSprite()
    {
        if (cachedCircleSprite != null) return cachedCircleSprite;

        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        float radius = (size * 0.5f) - 1f;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                // Anti-aliasing suave en el borde del círculo
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        cachedCircleSprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f)
        );

        return cachedCircleSprite;
    }
}
