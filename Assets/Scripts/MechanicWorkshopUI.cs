using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Controlador principal de la vista de mejoras del mecánico (MecanicoFondo.png).
/// Se abre al entrar con la van al cilindro azul y mantener pulsada la tecla [F].
/// Detiene temporalmente los controles del vehículo, muestra el cursor del ratón,
/// y permite salir con [ESC], [F] o botón de interfaz para volver a conducir.
/// </summary>
public class MechanicWorkshopUI : MonoBehaviour
{
    private static MechanicWorkshopUI instance;
    public static MechanicWorkshopUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindAnyObjectByType<MechanicWorkshopUI>(FindObjectsInactive.Include);
            }
            return instance;
        }
    }

    [Header("Referencias de UI")]
    [Tooltip("CanvasGroup del panel raíz del taller.")]
    public CanvasGroup panelGroup;

    [Tooltip("Imagen que muestra el fondo del taller (MecanicoFondo.png).")]
    public Image backgroundImage;

    [Tooltip("Botón opcional para cerrar o salir del taller.")]
    public Button closeButton;

    [Tooltip("Texto de ayuda de salida (ej: '[ESC] o [F] Volver a la ciudad').")]
    public TMP_Text exitHintText;

    [Header("Audio")]
    public AudioClip openSound;
    public AudioClip closeSound;

    [Header("Animación")]
    public float fadeDuration = 0.2f;

    // Estado público
    public bool IsOpen => isOpen;

    private bool isOpen = false;
    private Coroutine fadeCoroutine;
    private AudioSource audioSource;

    private VehicleController currentVehicle;
    private RadioPlayer currentRadioPlayer;
    private float preWorkshopRadioVolume = 1f;
    private float openCooldownTimer = 0f;

    void Awake()
    {
        instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.ignoreListenerPause = true;
        }

        if (panelGroup == null)
        {
            panelGroup = GetComponent<CanvasGroup>();
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseWorkshop);
        }

        // Iniciar completamente transparente y sin bloquear raycasts
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }
    }

    void Update()
    {
        if (openCooldownTimer > 0f)
        {
            openCooldownTimer -= Time.unscaledDeltaTime;
        }

        if (!isOpen) return;

        // Comprobar si se presiona ESC o F para salir del taller
        if (openCooldownTimer <= 0f)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame)
                {
                    CloseWorkshop();
                    return;
                }
            }

            var gp = Gamepad.current;
            if (gp != null && (gp.bButton.wasPressedThisFrame || gp.startButton.wasPressedThisFrame))
            {
                CloseWorkshop();
                return;
            }

            try
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F))
                {
                    CloseWorkshop();
                    return;
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Abre la vista del taller mecánico.
    /// </summary>
    public void OpenWorkshop(VehicleController vehicle = null)
    {
        gameObject.SetActive(true);
        if (isOpen) return;
        isOpen = true;
        openCooldownTimer = 0.35f; // Evita que la misma pulsación de F lo cierre al instante

        currentVehicle = vehicle;
        if (currentVehicle == null)
        {
            currentVehicle = Object.FindAnyObjectByType<VehicleController>();
        }

        // 1. Detener físicas e inhabilitar control del vehículo mientras se está en el menú
        if (currentVehicle != null)
        {
            Rigidbody vRb = currentVehicle.GetComponent<Rigidbody>();
            if (vRb != null)
            {
                vRb.linearVelocity = Vector3.zero;
                vRb.angularVelocity = Vector3.zero;
            }
            currentVehicle.enabled = false;

            currentRadioPlayer = currentVehicle.GetComponent<RadioPlayer>();
            if (currentRadioPlayer != null)
            {
                // Atenuar sutilmente la radio mientras está en el taller
                preWorkshopRadioVolume = currentRadioPlayer.volume;
                currentRadioPlayer.SetVolume(preWorkshopRadioVolume * 0.35f);
            }
        }

        // 2. Liberar el cursor para navegar en la interfaz
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3. Activar panel UI
        if (panelGroup != null)
        {
            panelGroup.gameObject.SetActive(true);
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
        }

        if (openSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(openSound);
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(1f, true));

        Debug.Log("[MechanicWorkshop] Vista de taller mecánico abierta.");
    }

    /// <summary>
    /// Cierra la vista del taller mecánico y devuelve el control al jugador en la van.
    /// </summary>
    public void CloseWorkshop()
    {
        if (!isOpen) return;
        isOpen = false;

        // 1. Restaurar volumen de la radio si estaba atenuado
        if (currentRadioPlayer != null)
        {
            currentRadioPlayer.SetVolume(preWorkshopRadioVolume);
            currentRadioPlayer = null;
        }

        // 2. Reactivar el vehículo
        if (currentVehicle != null)
        {
            currentVehicle.enabled = true;
            currentVehicle = null;
        }

        // 3. Bloquear de nuevo el cursor para conducir
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (panelGroup != null)
        {
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        if (closeSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(closeSound);
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(0f, false));

        Debug.Log("[MechanicWorkshop] Vista de taller cerrada. Conducción reanudada.");
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool isOpening)
    {
        if (panelGroup == null) yield break;

        float startAlpha = panelGroup.alpha;
        float elapsed = 0f;
        float dur = fadeDuration > 0.01f ? fadeDuration : 0.2f;

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / dur);
            yield return null;
        }

        panelGroup.alpha = targetAlpha;
        panelGroup.interactable = isOpening;
        panelGroup.blocksRaycasts = isOpening;
    }
}
