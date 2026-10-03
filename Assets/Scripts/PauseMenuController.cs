using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador principal del Menú de Pausa (PauseMenu).
/// Se ubica en un GameObject activo (PauseManager) para escuchar siempre la tecla ESC o mando,
/// congelar el tiempo (Time.timeScale = 0), liberar el cursor y controlar los sliders.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("CanvasGroup del panel raíz del menú de pausa (PauseMenuPanel).")]
    public CanvasGroup panelGroup;

    [Tooltip("Slider para volumen maestro general.")]
    public Slider generalVolumeSlider;

    [Tooltip("Slider para volumen de la radio del vehículo.")]
    public Slider radioVolumeSlider;

    [Tooltip("Slider para sensibilidad del mouse (a pie y conduciendo).")]
    public Slider mouseSensitivitySlider;

    [Tooltip("Botón Reanudar (verde).")]
    public Button resumeButton;

    [Tooltip("Botón Salir (rojo).")]
    public Button exitButton;

    [Header("Audio")]
    [Tooltip("Sonido al hacer clic en los botones.")]
    public AudioClip clickSound;

    [Tooltip("Sonido al pasar el cursor sobre los botones.")]
    public AudioClip hoverSound;

    [Header("Animación")]
    [Tooltip("Duración del desvanecimiento (fade in / out).")]
    public float fadeDuration = 0.15f;

    // Estado público
    public bool IsPaused => isPaused;

    // Estado interno
    private bool isPaused = false;
    private Coroutine fadeCoroutine;
    private AudioSource audioSource;

    private PlayerController cachedPlayerController;
    private VehicleController cachedVehicleController;
    private RadioPlayer cachedRadioPlayer;
    private BigMapController cachedBigMap;
    private MechanicWorkshopUI cachedMechanicUI;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.ignoreListenerPause = true; // Para sonar aun cuando el tiempo esté congelado
        }

        FindUIReferences();

        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
            panelGroup.gameObject.SetActive(false);
        }

        SetupSliders();
        SetupButtons();
    }

    void Start()
    {
        FindGameReferences();
        ApplyInitialSettings();
    }

    void Update()
    {
        CheckPauseInput();
    }

    private void CheckPauseInput()
    {
        // 1. Si el mapa grande está abierto, la tecla ESC debe cerrar el mapa, no abrir la pausa
        if (cachedBigMap == null) cachedBigMap = Object.FindAnyObjectByType<BigMapController>();
        if (cachedBigMap != null && cachedBigMap.IsOpen)
        {
            return;
        }

        // 1.5. Si el taller mecánico está abierto, la tecla ESC lo cierra a él, no abrir la pausa
        if (cachedMechanicUI == null) cachedMechanicUI = Object.FindAnyObjectByType<MechanicWorkshopUI>();
        if (cachedMechanicUI != null && cachedMechanicUI.IsOpen)
        {
            return;
        }

        bool toggleRequested = false;

        // 2. Detección con New Input System (Escape y tecla P)
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
            {
                toggleRequested = true;
            }
        }

        // 3. Soporte para mando (Start / Menu / Options)
        var gp = Gamepad.current;
        if (gp != null && gp.startButton.wasPressedThisFrame)
        {
            toggleRequested = true;
        }

        // 4. Fallback legacy por seguridad
        try
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                toggleRequested = true;
            }
        }
        catch { }

        // 5. Ejecutar alternancia
        if (toggleRequested)
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void PauseGame()
    {
        if (isPaused) return;
        isPaused = true;

        FindGameReferences();
        FindUIReferences();

        Debug.Log("[PauseMenu] Juego pausado (Time.timeScale = 0).");

        // Detener tiempo de juego
        Time.timeScale = 0f;

        // Liberar cursor para interactuar con la UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (panelGroup != null)
        {
            panelGroup.gameObject.SetActive(true);
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(1f, true));
    }

    public void ResumeGame()
    {
        if (!isPaused) return;
        isPaused = false;

        PlayClickSound();
        Debug.Log("[PauseMenu] Juego reanudado (Time.timeScale = 1).");

        // Reanudar tiempo
        Time.timeScale = 1f;

        // Bloquear cursor para juego en 1ra persona
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (panelGroup != null)
        {
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(0f, false));
    }

    public void ExitToMainMenu()
    {
        PlayClickSound();
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool isOpening)
    {
        if (panelGroup == null) yield break;

        float startAlpha = panelGroup.alpha;
        float elapsed = 0f;
        float dur = fadeDuration > 0.01f ? fadeDuration : 0.15f;

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / dur);
            yield return null;
        }

        panelGroup.alpha = targetAlpha;

        if (!isOpening)
        {
            panelGroup.gameObject.SetActive(false);
        }
    }

    private void FindUIReferences()
    {
        if (panelGroup == null)
        {
            GameObject panelGO = GameObject.Find("PauseMenuPanel");
            if (panelGO != null) panelGroup = panelGO.GetComponent<CanvasGroup>();
        }

        if (generalVolumeSlider == null)
        {
            GameObject s = GameObject.Find("Slider_General");
            if (s != null) generalVolumeSlider = s.GetComponent<Slider>();
        }

        if (radioVolumeSlider == null)
        {
            GameObject s = GameObject.Find("Slider_Radio");
            if (s != null) radioVolumeSlider = s.GetComponent<Slider>();
        }

        if (mouseSensitivitySlider == null)
        {
            GameObject s = GameObject.Find("Slider_Sensibilidad");
            if (s != null) mouseSensitivitySlider = s.GetComponent<Slider>();
        }

        if (resumeButton == null)
        {
            GameObject b = GameObject.Find("Button_Reanudar");
            if (b != null) resumeButton = b.GetComponent<Button>();
        }

        if (exitButton == null)
        {
            GameObject b = GameObject.Find("Button_Salir");
            if (b != null) exitButton = b.GetComponent<Button>();
        }
    }

    private void SetupSliders()
    {
        if (generalVolumeSlider != null)
        {
            generalVolumeSlider.minValue = 0f;
            generalVolumeSlider.maxValue = 1f;
            generalVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
            generalVolumeSlider.onValueChanged.RemoveAllListeners();
            generalVolumeSlider.onValueChanged.AddListener(OnGeneralVolumeChanged);
        }

        if (radioVolumeSlider != null)
        {
            radioVolumeSlider.minValue = 0f;
            radioVolumeSlider.maxValue = 1f;
            radioVolumeSlider.value = PlayerPrefs.GetFloat("RadioVolume", 0.8f);
            radioVolumeSlider.onValueChanged.RemoveAllListeners();
            radioVolumeSlider.onValueChanged.AddListener(OnRadioVolumeChanged);
        }

        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.minValue = 20f;
            mouseSensitivitySlider.maxValue = 250f;
            mouseSensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 100f);
            mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
            mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        }
    }

    private void SetupButtons()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(ResumeGame);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(ExitToMainMenu);
        }
    }

    private void ApplyInitialSettings()
    {
        float master = PlayerPrefs.GetFloat("MasterVolume", 1f);
        AudioListener.volume = master;

        float radio = PlayerPrefs.GetFloat("RadioVolume", 0.8f);
        if (cachedRadioPlayer != null) cachedRadioPlayer.SetVolume(radio);

        float sens = PlayerPrefs.GetFloat("MouseSensitivity", 100f);
        if (cachedPlayerController != null) cachedPlayerController.mouseSensitivity = sens;
        if (cachedVehicleController != null) cachedVehicleController.mouseSensitivity = sens;
    }

    public void OnGeneralVolumeChanged(float val)
    {
        AudioListener.volume = val;
        PlayerPrefs.SetFloat("MasterVolume", val);
    }

    public void OnRadioVolumeChanged(float val)
    {
        PlayerPrefs.SetFloat("RadioVolume", val);
        if (cachedRadioPlayer != null)
        {
            cachedRadioPlayer.SetVolume(val);
        }
    }

    public void OnMouseSensitivityChanged(float val)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", val);
        if (cachedPlayerController != null) cachedPlayerController.mouseSensitivity = val;
        if (cachedVehicleController != null) cachedVehicleController.mouseSensitivity = val;
    }

    private void FindGameReferences()
    {
        if (cachedPlayerController == null)
            cachedPlayerController = Object.FindAnyObjectByType<PlayerController>();

        if (cachedVehicleController == null)
            cachedVehicleController = Object.FindAnyObjectByType<VehicleController>();

        if (cachedRadioPlayer == null)
            cachedRadioPlayer = Object.FindAnyObjectByType<RadioPlayer>();

        if (cachedBigMap == null)
            cachedBigMap = Object.FindAnyObjectByType<BigMapController>();
    }

    public void PlayClickSound()
    {
        if (clickSound != null && audioSource != null)
            audioSource.PlayOneShot(clickSound);
    }

    public void PlayHoverSound()
    {
        if (hoverSound != null && audioSource != null)
            audioSource.PlayOneShot(hoverSound);
    }
}
