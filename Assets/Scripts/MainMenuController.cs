using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador principal del Menú de Inicio (MainMenu).
/// Gestiona la transición a la escena de juego, el panel de opciones y la salida del juego.
/// Mantiene sincronizados los ajustes de Audio (General, Radio) y Sensibilidad del mouse con PlayerPrefs.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Configuración de Escenas")]
    [Tooltip("Nombre de la escena de juego a cargar.")]
    public string gameSceneName = "Game";

    [Header("Referencias de UI")]
    [Tooltip("Panel modal de opciones.")]
    public GameObject optionsPanel;

    [Tooltip("Slider para volumen general en opciones.")]
    public Slider generalVolumeSlider;

    [Tooltip("Slider para volumen de la radio.")]
    public Slider radioVolumeSlider;

    [Tooltip("Slider para sensibilidad del mouse.")]
    public Slider mouseSensitivitySlider;

    [Tooltip("Referencia legacy para volumen general.")]
    public Slider volumeSlider;

    [Tooltip("Botón Volver dentro del panel de opciones.")]
    public Button backButton;

    [Header("Audio")]
    [Tooltip("Sonido al presionar cualquier botón.")]
    public AudioClip buttonClickSound;

    [Tooltip("Sonido al pasar el cursor sobre un botón.")]
    public AudioClip buttonHoverSound;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Compatibilidad legacy
        if (generalVolumeSlider == null && volumeSlider != null)
        {
            generalVolumeSlider = volumeSlider;
        }
        else if (volumeSlider == null && generalVolumeSlider != null)
        {
            volumeSlider = generalVolumeSlider;
        }

        FindUIReferences();

        if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);
        }

        SetupSliders();
        ApplyInitialSettings();
    }

    void Update()
    {
        CheckInput();
    }

    private void CheckInput()
    {
        // Si el panel de opciones está abierto, la tecla ESC lo cierra
        if (optionsPanel != null && optionsPanel.activeSelf)
        {
            var kb = Keyboard.current;
            bool escPressed = false;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                escPressed = true;
            }

            try
            {
                if (Input.GetKeyDown(KeyCode.Escape)) escPressed = true;
            }
            catch { }

            if (escPressed)
            {
                CloseOptions();
            }
        }
    }

    private void FindUIReferences()
    {
        if (optionsPanel != null)
        {
            Slider[] sliders = optionsPanel.GetComponentsInChildren<Slider>(true);
            foreach (var s in sliders)
            {
                if (s.name.Contains("General") && generalVolumeSlider == null) generalVolumeSlider = s;
                else if (s.name.Contains("Radio") && radioVolumeSlider == null) radioVolumeSlider = s;
                else if (s.name.Contains("Sensibilidad") && mouseSensitivitySlider == null) mouseSensitivitySlider = s;
            }

            if (backButton == null)
            {
                Button[] buttons = optionsPanel.GetComponentsInChildren<Button>(true);
                foreach (var b in buttons)
                {
                    if (b.name.Contains("Volver") || b.name.Contains("Cerrar") || b.name.Contains("Close"))
                    {
                        backButton = b;
                        break;
                    }
                }
            }
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

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(CloseOptions);
        }
    }

    private void ApplyInitialSettings()
    {
        float master = PlayerPrefs.GetFloat("MasterVolume", 1f);
        AudioListener.volume = master;
    }

    /// <summary>
    /// Inicia la partida cargando la escena del juego.
    /// </summary>
    public void PlayGame()
    {
        PlayClickSound();
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            SceneManager.LoadScene(1);
        }
    }

    /// <summary>
    /// Abre el panel modal de opciones sincronizando sus sliders.
    /// </summary>
    public void OpenOptions()
    {
        PlayClickSound();
        if (optionsPanel != null)
        {
            // Sincronizar con los últimos valores de PlayerPrefs
            if (generalVolumeSlider != null)
                generalVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
            if (radioVolumeSlider != null)
                radioVolumeSlider.value = PlayerPrefs.GetFloat("RadioVolume", 0.8f);
            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 100f);

            optionsPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Cierra el panel modal de opciones y guarda preferencias.
    /// </summary>
    public void CloseOptions()
    {
        PlayClickSound();
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);
        }
        PlayerPrefs.Save();
    }

    public void OnGeneralVolumeChanged(float val)
    {
        AudioListener.volume = val;
        PlayerPrefs.SetFloat("MasterVolume", val);
    }

    public void OnRadioVolumeChanged(float val)
    {
        PlayerPrefs.SetFloat("RadioVolume", val);
    }

    public void OnMouseSensitivityChanged(float val)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", val);
    }

    /// <summary>
    /// Modifica el volumen global (compatibilidad).
    /// </summary>
    public void SetVolume(float volume)
    {
        OnGeneralVolumeChanged(volume);
    }

    /// <summary>
    /// Sale de la aplicación.
    /// </summary>
    public void QuitGame()
    {
        PlayClickSound();
        Debug.Log("[MainMenu] Saliendo del juego...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void PlayHoverSound()
    {
        if (audioSource != null && buttonHoverSound != null)
        {
            audioSource.PlayOneShot(buttonHoverSound, 0.6f);
        }
    }

    public void PlayClickSound()
    {
        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.PlayOneShot(buttonClickSound, 0.9f);
        }
    }
}
