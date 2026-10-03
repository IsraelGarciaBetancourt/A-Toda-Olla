using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controlador del Velocímetro y Nivel de Combustible para el vehículo en A-Toda-Olla.
/// 
/// Características:
/// - Aparece automáticamente al entrar a la van (fade-in) y se oculta al salir (fade-out).
/// - En el Editor se mantiene 100% visible para que puedas posicionar y escalar la UI a tu gusto.
/// - Rota la aguja analógica con física suave (damping) entre 0 y 140 km/h.
/// - Actualiza el número digital en la placa inferior con TextMeshPro.
/// - Controla la barra de combustible (relleno escalonado por barritas o continuo).
/// </summary>
public class SpeedometerHUD : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("CanvasGroup del panel completo para control de opacidad / fade.")]
    public CanvasGroup canvasGroup;

    [Tooltip("RectTransform de la aguja. Debe rotar sobre el centro del dial.")]
    public RectTransform needleRect;

    [Tooltip("Texto digital para la velocidad (TextMeshPro).")]
    public TMP_Text speedText;

    [Tooltip("Imagen de relleno de las barritas de gasolina (Image Type: Filled).")]
    public Image fuelFillImage;

    [Header("Calibración del Velocímetro")]
    [Tooltip("Velocidad máxima representada en la esfera (ej: 140 km/h).")]
    public float maxSpeedKmh = 140f;

    [Tooltip("Ángulo de la aguja cuando la velocidad es 0 km/h (ej: 125°).")]
    public float zeroSpeedAngle = 125f;

    [Tooltip("Ángulo de la aguja cuando la velocidad es máxima (ej: -125°).")]
    public float maxSpeedAngle = -125f;

    [Tooltip("Multiplicador para ajustar la velocidad visual (m/s a km/h es 3.6f).")]
    public float speedMultiplier = 3.6f;

    [Tooltip("Suavizado de la aguja (mayor valor = más reactiva, menor = más pesada).")]
    public float needleSmoothSpeed = 10f;

    [Tooltip("Vibración sutil de la aguja simulando motor de furgoneta clásica.")]
    public bool enableNeedleJitter = true;

    [Header("Combustible")]
    [Tooltip("Nivel de combustible actual (de 0.0 a 1.0).")]
    [Range(0f, 1f)]
    public float currentFuel = 0.85f;

    [Tooltip("Número de barritas en el medidor de gasolina (ej: 7).")]
    public int fuelBarSegments = 7;

    [Tooltip("Si es true, apaga las barritas de forma escalonada tipo LED.")]
    public bool isFuelSegmented = true;

    [Header("Visibilidad y Fade")]
    [Tooltip("Duración del fade al entrar / salir del auto.")]
    public float fadeSpeed = 6f;

    [Tooltip("Si es true, el HUD se mantiene visible siempre en el Editor para acomodar la UI.")]
    public bool visibleInEditor = true;

    // Referencias al vehículo
    private VehicleInteraction vehicleInteraction;
    private VehicleController vehicleController;

    private float currentDisplayedSpeed = 0f;
    private float targetAlpha = 0f;

    void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        FindVehicleReferences();
    }

    void Start()
    {
        // En Play Mode inicia oculto a menos que ya estemos dentro del vehículo
        if (Application.isPlaying)
        {
            bool isInside = vehicleInteraction != null && vehicleInteraction.PlayerIsInside;
            targetAlpha = isInside ? 1f : 0f;
            if (canvasGroup != null) canvasGroup.alpha = targetAlpha;
        }
    }

    void Update()
    {
        FindVehicleReferences();

        bool isDriving = false;
        float actualSpeed = 0f;

        if (vehicleInteraction != null && vehicleInteraction.PlayerIsInside)
        {
            isDriving = true;
            if (vehicleController != null)
            {
                // Velocidad real del vehículo en km/h
                actualSpeed = Mathf.Abs(vehicleController.ForwardSpeed) * speedMultiplier;
            }
        }

        // 1. Control de visibilidad (Fade in / Fade out)
        if (Application.isPlaying)
        {
            targetAlpha = isDriving ? 1f : 0f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (canvasGroup != null && canvasGroup.alpha <= 0.01f)
                return; // No calcular si está completamente invisible
        }
        else
        {
            // En modo edición en Unity
            if (visibleInEditor && canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        // 2. Suavizado de la aguja y cálculo de ángulo
        currentDisplayedSpeed = Mathf.Lerp(currentDisplayedSpeed, actualSpeed, Time.deltaTime * needleSmoothSpeed);

        if (needleRect != null)
        {
            float speedPercent = Mathf.Clamp01(currentDisplayedSpeed / maxSpeedKmh);
            float targetAngle = Mathf.Lerp(zeroSpeedAngle, maxSpeedAngle, speedPercent);

            if (enableNeedleJitter && isDriving && currentDisplayedSpeed > 1f)
            {
                float jitter = (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * 1.5f;
                targetAngle += jitter;
            }

            needleRect.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
        }

        // 3. Texto digital en la placa inferior
        if (speedText != null)
        {
            int roundSpeed = Mathf.RoundToInt(currentDisplayedSpeed);
            speedText.text = roundSpeed.ToString();
        }

        // 4. Medidor de combustible
        if (fuelFillImage != null)
        {
            if (isFuelSegmented && fuelBarSegments > 0)
            {
                float step = 1f / fuelBarSegments;
                fuelFillImage.fillAmount = Mathf.Floor(currentFuel * fuelBarSegments) * step;
            }
            else
            {
                fuelFillImage.fillAmount = currentFuel;
            }
        }
    }

    private void FindVehicleReferences()
    {
        if (vehicleInteraction == null)
            vehicleInteraction = Object.FindAnyObjectByType<VehicleInteraction>();

        if (vehicleController == null)
            vehicleController = Object.FindAnyObjectByType<VehicleController>();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Permite previsualizar el ángulo de la aguja y la gasolina directamente en el Inspector
        if (!Application.isPlaying && needleRect != null)
        {
            float speedPercent = Mathf.Clamp01(currentDisplayedSpeed / maxSpeedKmh);
            float angle = Mathf.Lerp(zeroSpeedAngle, maxSpeedAngle, speedPercent);
            needleRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (!Application.isPlaying && fuelFillImage != null)
        {
            if (isFuelSegmented && fuelBarSegments > 0)
                fuelFillImage.fillAmount = Mathf.Floor(currentFuel * fuelBarSegments) / fuelBarSegments;
            else
                fuelFillImage.fillAmount = currentFuel;
        }
    }
#endif
}
