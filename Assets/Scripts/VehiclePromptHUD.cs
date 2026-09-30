using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gestiona la UI en pantalla (HUD) para entrar al vehículo cuando el jugador está en la zona de la puerta del conductor.
/// Muestra el ícono de la tecla [E] y el texto "Para entrar".
/// 
/// CONSEJO RÁPIDO:
/// Puedes duplicar (Ctrl+D / Cmd+D) tu objeto "VanDoorPrompt", renombrarlo a "VehiclePrompt",
/// quitarle el script VanDoorPromptHUD y ponerle este script (VehiclePromptHUD).
/// ¡Conservará todo el estilo, fondo negro redondeado e ícono automáticamente!
/// </summary>
public class VehiclePromptHUD : MonoBehaviour
{
    [Header("Referencias de Vehículo")]
    [Tooltip("Controlador de interacción del vehículo. Si se deja vacío, se buscará automáticamente.")]
    public VehicleInteraction vehicleInteraction;

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
    [Tooltip("Texto para entrar al vehículo.")]
    public string enterText = "Para entrar";

    [Tooltip("Texto cuando el jugador intenta entrar con las manos ocupadas.")]
    public string handsOccupiedText = "¡Manos ocupadas! Presiona [G] para soltar";

    [Tooltip("Texto para salir del vehículo mientras se conduce.")]
    public string exitText = "Para salir";

    [Tooltip("¿Mostrar también el prompt para salir mientras el jugador está conduciendo?")]
    public bool showExitPromptWhileDriving = false;

    [Header("Colores")]
    public Color normalTextColor = Color.white;
    public Color handsOccupiedColor = new Color(1f, 0.45f, 0.45f, 1f); // Rojo coral
    public Color normalKeyColor = Color.white;
    public Color handsOccupiedKeyColor = new Color(1f, 1f, 1f, 0.3f); // Tecla atenuada

    [Header("Animación")]
    [Tooltip("Multiplicador de escala global de todo el prompt (ej: 1.3 o 1.5).")]
    public float promptScale = 1.3f;

    [Tooltip("Velocidad de fundido y animación de escala.")]
    public float transitionSpeed = 14f;

    [Tooltip("¿Animar con un ligero pop de escala al aparecer?")]
    public bool useScaleAnimation = true;

    private float targetAlpha = 0f;
    private Vector3 targetScale = Vector3.one;
    private PlayerPickup playerPickup;

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

        // Buscar solo en los hijos para no confundir con la imagen de fondo
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
        FindReferences();
    }

    void LateUpdate()
    {
        if (vehicleInteraction == null)
        {
            FindReferences();
            if (vehicleInteraction == null)
            {
                targetAlpha = 0f;
                UpdateAnimation();
                return;
            }
        }

        UpdatePromptState();
        UpdateAnimation();
    }

    private void UpdatePromptState()
    {
        // 1. Si el jugador está dentro conduciendo
        if (vehicleInteraction.PlayerIsInside)
        {
            if (showExitPromptWhileDriving)
            {
                SetContent(exitText, normalTextColor, true, normalKeyColor);
                targetAlpha = 1f;
                targetScale = Vector3.one * promptScale;
            }
            else
            {
                targetAlpha = 0f;
                targetScale = Vector3.one * (promptScale * 0.85f);
            }
            return;
        }

        // 2. Si el jugador puede interactuar (está en la zona de la puerta del auto)
        if (vehicleInteraction.CanInteract)
        {
            // Comprobar si tiene las manos ocupadas
            bool hasItem = playerPickup != null && playerPickup.IsCarryingItem;

            if (hasItem)
            {
                SetContent(handsOccupiedText, handsOccupiedColor, false, handsOccupiedKeyColor);
            }
            else
            {
                SetContent(enterText, normalTextColor, true, normalKeyColor);
            }

            targetAlpha = 1f;
            targetScale = Vector3.one * promptScale;
        }
        else
        {
            // Fuera de la zona
            targetAlpha = 0f;
            targetScale = Vector3.one * (promptScale * 0.85f);
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

    private void FindReferences()
    {
        if (vehicleInteraction == null)
        {
            vehicleInteraction = Object.FindFirstObjectByType<VehicleInteraction>();
        }

        if (playerPickup == null)
        {
            playerPickup = Object.FindFirstObjectByType<PlayerPickup>();
        }
    }
}
