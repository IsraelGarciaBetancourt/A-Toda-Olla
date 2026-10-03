using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Añade micro-animaciones de hover y pulsación (juice) a los botones del menú.
/// </summary>
public class MenuButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Escala Multiplicadora")]
    [Tooltip("Multiplicador de escala al pasar el mouse por encima (ej. 1.06 = 6% más grande).")]
    public float hoverMultiplier = 1.06f;

    [Tooltip("Multiplicador de escala al presionar el botón (ej. 0.96 = 4% más chico).")]
    public float pressedMultiplier = 0.96f;

    [Tooltip("Velocidad de interpolación.")]
    public float animationSpeed = 14f;

    private Vector3 initialScale;
    private Vector3 currentTargetScale;
    private bool isHovered = false;
    private MainMenuController mainMenuController;
    private PauseMenuController pauseMenuController;

    void Awake()
    {
        initialScale = transform.localScale;
        currentTargetScale = initialScale;
        mainMenuController = Object.FindAnyObjectByType<MainMenuController>();
        pauseMenuController = Object.FindAnyObjectByType<PauseMenuController>();
    }

    void OnDisable()
    {
        if (initialScale != Vector3.zero)
        {
            transform.localScale = initialScale;
            currentTargetScale = initialScale;
        }
        isHovered = false;
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, currentTargetScale, Time.unscaledDeltaTime * animationSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        currentTargetScale = initialScale * hoverMultiplier;
        if (mainMenuController != null)
        {
            mainMenuController.PlayHoverSound();
        }
        else if (pauseMenuController != null)
        {
            pauseMenuController.PlayHoverSound();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        currentTargetScale = initialScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        currentTargetScale = initialScale * pressedMultiplier;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        currentTargetScale = isHovered ? initialScale * hoverMultiplier : initialScale;
    }
}
