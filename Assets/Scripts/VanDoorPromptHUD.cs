using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gestiona la UI en pantalla (HUD) para interactuar con la puerta trasera de la van.
/// Muestra el ícono de la tecla [E] y el texto de acción correspondiente.
/// 
/// ESTRUCTURA RECOMENDADA EN UNITY:
/// HUDCanvas (Screen Space - Overlay)
///   └── VanDoorPrompt (Panel o fondo oscuro con CanvasGroup)
///         ├── KeyIcon (Image con el sprite de keyboard_e)
///         └── ActionText (TextMeshPro - Text UI)
/// </summary>
public class VanDoorPromptHUD : MonoBehaviour
{
    [Header("Referencias de Van")]
    [Tooltip("Controlador de la puerta trasera. Si se deja vacío, se buscará automáticamente.")]
    public VanDoorController vanDoorController;

    [Header("Referencias de UI")]
    [Tooltip("Contenedor raíz del prompt (para mover o desvanecer todo el conjunto).")]
    public RectTransform promptContainer;

    [Tooltip("CanvasGroup para transición suave de opacidad. Si no tiene, se añadirá automáticamente.")]
    public CanvasGroup canvasGroup;

    [Tooltip("Imagen donde va el sprite de la tecla (Assets/UI/keyboard_e.svg).")]
    public Image keyIcon;

    [Tooltip("Componente de texto TextMeshPro donde se muestra la acción.")]
    public TMP_Text actionText;

    [Header("Textos Personalizables")]
    [Tooltip("Texto cuando la puerta está cerrada y se puede abrir.")]
    public string openDoorText = "Para abrir";

    [Tooltip("Texto cuando la puerta está abierta y se puede cerrar.")]
    public string closeDoorText = "Para cerrar";

    [Tooltip("Texto de advertencia cuando el jugador tiene un objeto en las manos.")]
    public string handsOccupiedText = "¡Manos ocupadas! Presiona [G] para soltar";

    [Header("Colores")]
    public Color normalTextColor = Color.white;
    public Color handsOccupiedColor = new Color(1f, 0.45f, 0.45f, 1f); // Rojo suave / coral
    public Color normalKeyColor = Color.white;
    public Color handsOccupiedKeyColor = new Color(1f, 1f, 1f, 0.3f); // Tecla atenuada

    [Header("Animación")]
    [Tooltip("Multiplicador de escala global de todo el prompt (ej: 1.3 o 1.5 para hacerlo más grande).")]
    public float promptScale = 1.3f;

    [Tooltip("Velocidad de fundido y animación de escala.")]
    public float transitionSpeed = 14f;

    [Tooltip("¿Animar con un ligero pop de escala al aparecer?")]
    public bool useScaleAnimation = true;

    private float targetAlpha = 0f;
    private Vector3 targetScale = Vector3.one;

    void Awake()
    {
        // 1. Auto-asignar referencias de UI si no se colocaron en el Inspector
        if (promptContainer == null)
        {
            promptContainer = GetComponent<RectTransform>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null && promptContainer != null)
            {
                canvasGroup = promptContainer.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (keyIcon == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject != gameObject)
                {
                    keyIcon = img;
                    break;
                }
            }
        }

        if (actionText == null)
        {
            actionText = GetComponentInChildren<TMP_Text>();
        }

        // Inicializar oculto
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        if (promptContainer != null && useScaleAnimation)
        {
            promptContainer.localScale = Vector3.one * (promptScale * 0.85f);
        }
    }

    void Start()
    {
        FindVanDoorController();
    }

    void LateUpdate()
    {
        if (vanDoorController == null)
        {
            FindVanDoorController();
            if (vanDoorController == null)
            {
                targetAlpha = 0f;
                UpdateAnimation();
                return;
            }
        }

        // Determinar si debemos mostrar el prompt y qué contenido poner
        UpdatePromptState();

        // Animar opacidad y escala suavemente
        UpdateAnimation();
    }

    private void UpdatePromptState()
    {
        // Si el jugador no está en la zona trasera de la van, ocultar
        if (!vanDoorController.PlayerInRearZone)
        {
            targetAlpha = 0f;
            targetScale = Vector3.one * (promptScale * 0.85f);
            return;
        }

        bool isOpen = vanDoorController.IsOpen;
        bool hasItem = vanDoorController.IsPlayerCarryingItem;

        if (!isOpen)
        {
            // Puerta CERRADA
            if (hasItem)
            {
                // Manos ocupadas: No puede abrir la puerta
                SetContent(handsOccupiedText, handsOccupiedColor, false, handsOccupiedKeyColor);
            }
            else
            {
                // Manos libres: Puede abrir con [E]
                SetContent(openDoorText, normalTextColor, true, normalKeyColor);
            }

            targetAlpha = 1f;
            targetScale = Vector3.one * promptScale;
        }
        else
        {
            // Puerta ABIERTA
            if (hasItem)
            {
                // Con puerta abierta y objeto en mano, el sistema de carga (CargoZone)
                // tiene prioridad para guardar el objeto, por lo que ocultamos este prompt.
                targetAlpha = 0f;
                targetScale = Vector3.one * (promptScale * 0.85f);
            }
            else
            {
                // Manos libres: Puede cerrar con [E]
                SetContent(closeDoorText, normalTextColor, true, normalKeyColor);
                targetAlpha = 1f;
                targetScale = Vector3.one * promptScale;
            }
        }
    }

    private void SetContent(string text, Color textColor, bool showKey, Color keyColor)
    {
        if (actionText != null)
        {
            actionText.text = text;
            actionText.color = textColor;
        }

        if (keyIcon != null)
        {
            keyIcon.gameObject.SetActive(showKey);
            keyIcon.color = keyColor;
        }
    }

    private void UpdateAnimation()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * transitionSpeed);
            canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.5f;
        }

        if (promptContainer != null && useScaleAnimation)
        {
            promptContainer.localScale = Vector3.Lerp(
                promptContainer.localScale,
                targetScale,
                Time.deltaTime * transitionSpeed
            );
        }
    }

    private void FindVanDoorController()
    {
        if (vanDoorController == null)
        {
            vanDoorController = Object.FindAnyObjectByType<VanDoorController>();
        }
    }
}
