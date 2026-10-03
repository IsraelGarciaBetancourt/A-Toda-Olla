using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gestiona la UI en pantalla (HUD) del aviso de interacción al entrar con la van al cilindro del taller mecánico.
/// Muestra la indicación de mantener pulsada la tecla [F] con un indicador de progreso de carga.
/// </summary>
public class MechanicWorkshopPromptHUD : MonoBehaviour
{
    private static MechanicWorkshopPromptHUD instance;
    public static MechanicWorkshopPromptHUD Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindAnyObjectByType<MechanicWorkshopPromptHUD>(FindObjectsInactive.Include);
            }
            return instance;
        }
    }

    [Header("Referencias de UI")]
    [Tooltip("Contenedor raíz del prompt para mover y escalar.")]
    public RectTransform promptContainer;

    [Tooltip("CanvasGroup para fundido de opacidad suave.")]
    public CanvasGroup canvasGroup;

    [Tooltip("Fondo o borde del botón de la tecla [F].")]
    public Image keyBadgeBg;

    [Tooltip("Texto dentro del botón (por defecto 'F').")]
    public TMP_Text keyText;

    [Tooltip("Imagen de relleno (radial o barra) que muestra el progreso al mantener presionada la tecla.")]
    public Image progressFill;

    [Tooltip("Texto descriptivo de la acción ('Mantén para entrar al Taller').")]
    public TMP_Text actionText;

    [Header("Colores")]
    public Color promptActiveColor = new Color(0.18f, 0.72f, 1.0f, 1f); // Azul eléctrico GTA
    public Color normalTextColor = Color.white;
    public Color normalFillColor = new Color(0.2f, 0.8f, 1.0f, 1f);

    [Header("Animación")]
    public float promptScale = 1.25f;
    public float transitionSpeed = 14f;

    private float targetAlpha = 0f;
    private float currentProgress = 0f;
    private bool isVisible = false;

    void Awake()
    {
        instance = this;

        if (promptContainer == null) promptContainer = GetComponent<RectTransform>();

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null && promptContainer != null)
            {
                canvasGroup = promptContainer.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (keyText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in texts)
            {
                if (t.name.Contains("Key")) keyText = t;
                else if (actionText == null) actionText = t;
            }
        }

        if (progressFill != null)
        {
            progressFill.fillAmount = 0f;
        }

        // Iniciar oculto
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        if (promptContainer != null)
        {
            promptContainer.localScale = Vector3.one * (promptScale * 0.85f);
        }
    }

    void LateUpdate()
    {
        targetAlpha = isVisible ? 1f : 0f;
        Vector3 targetScale = isVisible ? Vector3.one * promptScale : Vector3.one * (promptScale * 0.85f);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * transitionSpeed);
            canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.5f;
        }

        if (promptContainer != null)
        {
            promptContainer.localScale = Vector3.Lerp(
                promptContainer.localScale,
                targetScale,
                Time.unscaledDeltaTime * transitionSpeed
            );
        }

        if (progressFill != null)
        {
            progressFill.fillAmount = Mathf.Lerp(progressFill.fillAmount, currentProgress, Time.unscaledDeltaTime * 25f);
        }
    }

    /// <summary>
    /// Muestra u oculta el prompt del taller.
    /// </summary>
    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (!visible)
        {
            currentProgress = 0f;
            if (progressFill != null) progressFill.fillAmount = 0f;
        }
    }

    /// <summary>
    /// Actualiza el progreso de mantener presionada la tecla (0 a 1).
    /// </summary>
    public void SetHoldProgress(float progress01)
    {
        currentProgress = Mathf.Clamp01(progress01);
    }
}
