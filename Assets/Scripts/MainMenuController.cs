using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controlador principal del Menú de Inicio (MainMenu).
/// Gestiona la transición a la escena de juego, el panel de opciones y la salida del juego.
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
    public Slider volumeSlider;

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

        if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);
        }

        // Cargar volumen guardado si existe
        if (volumeSlider != null)
        {
            float savedVol = PlayerPrefs.GetFloat("MasterVolume", 1f);
            volumeSlider.value = savedVol;
            AudioListener.volume = savedVol;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
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
            // Fallback por índice
            SceneManager.LoadScene(1);
        }
    }

    /// <summary>
    /// Abre el panel modal de opciones.
    /// </summary>
    public void OpenOptions()
    {
        PlayClickSound();
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Cierra el panel modal de opciones.
    /// </summary>
    public void CloseOptions()
    {
        PlayClickSound();
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Modifica el volumen global y guarda la preferencia.
    /// </summary>
    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("MasterVolume", volume);
        PlayerPrefs.Save();
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
