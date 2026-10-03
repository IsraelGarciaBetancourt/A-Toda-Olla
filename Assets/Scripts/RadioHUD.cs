using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD de radio estilo GTA Vice City para A-Toda-Olla.
///
/// Aparece en la parte superior central de la pantalla al cambiar de emisora
/// (o al entrar al vehículo con la radio activa).
/// Muestra:
///   - Nombre de la emisora  (en el color configurado en RadioStation)
///   - Nombre de la canción  (en blanco/gris claro)
///
/// Se desvanece automáticamente tras displayDuration segundos.
/// Si cambia la emisora antes de desvanecerse, reinicia el timer.
/// Al cambiar de canción dentro de la misma emisora, solo actualiza el texto
/// sin reiniciar el timer completo.
/// </summary>
public class RadioHUD : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────────
    //  INSPECTOR
    // ──────────────────────────────────────────────────────────────────────

    [Header("Referencias UI")]
    [Tooltip("Panel raíz. Debe tener un CanvasGroup para el fade.")]
    public CanvasGroup panelGroup;

    [Tooltip("Texto del nombre de la emisora (TMP).")]
    public TMP_Text stationNameText;

    [Tooltip("Texto del nombre de la canción (TMP).")]
    public TMP_Text trackNameText;

    [Header("Comportamiento")]
    [Tooltip("Tiempo en segundos que el panel permanece visible.")]
    [Range(1f, 10f)]
    public float displayDuration = 4f;

    [Tooltip("Duración del fade-in al aparecer.")]
    [Range(0.05f, 1f)]
    public float fadeInTime = 0.25f;

    [Tooltip("Duración del fade-out al desaparecer.")]
    [Range(0.1f, 2f)]
    public float fadeOutTime = 0.6f;

    [Header("Textos")]
    [Tooltip("Texto mostrado cuando la radio está apagada.")]
    public string offStationLabel = "Radio Apagada";

    [Tooltip("Prefijo mostrado antes del nombre de la canción.")]
    public string trackPrefix = "♪  ";

    // ──────────────────────────────────────────────────────────────────────
    //  ESTADO INTERNO
    // ──────────────────────────────────────────────────────────────────────

    private RadioPlayer radioPlayer;
    private Coroutine showCoroutine;

    // ──────────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ──────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }
    }

    void Start()
    {
        // Buscar RadioPlayer en escena
        radioPlayer = Object.FindAnyObjectByType<RadioPlayer>();
        if (radioPlayer == null)
        {
            Debug.LogWarning("[RadioHUD] No se encontró RadioPlayer en la escena. El HUD no funcionará.");
            return;
        }

        radioPlayer.OnStationChanged += HandleStationChanged;
        radioPlayer.OnTrackChanged  += HandleTrackChanged;
    }

    void OnDestroy()
    {
        if (radioPlayer != null)
        {
            radioPlayer.OnStationChanged -= HandleStationChanged;
            radioPlayer.OnTrackChanged  -= HandleTrackChanged;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  CALLBACKS DEL RadioPlayer
    // ──────────────────────────────────────────────────────────────────────

    private void HandleStationChanged(RadioStation station, int index)
    {
        // Actualizar nombre de emisora
        if (stationNameText != null)
        {
            if (station == null || station.IsEmpty)
            {
                stationNameText.text = offStationLabel;
                stationNameText.color = Color.gray;
            }
            else
            {
                stationNameText.text = station.stationName;
                stationNameText.color = station.stationColor;
            }
        }

        // Limpiar canción hasta que el RadioPlayer notifique cuál toca
        if (trackNameText != null)
            trackNameText.text = string.Empty;

        // Mostrar el panel (reinicia el timer si ya estaba visible)
        ShowPanel();
    }

    private void HandleTrackChanged(string trackName)
    {
        if (trackNameText != null)
        {
            trackNameText.text = string.IsNullOrEmpty(trackName)
                ? string.Empty
                : trackPrefix + CleanTrackName(trackName);
        }

        // Si comienza una nueva canción válida, mostrar brevemente el HUD (estilo GTA)
        if (!string.IsNullOrEmpty(trackName))
        {
            ShowPanel();
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  MOSTRAR / OCULTAR PANEL
    // ──────────────────────────────────────────────────────────────────────

    private void ShowPanel()
    {
        if (showCoroutine != null) StopCoroutine(showCoroutine);
        showCoroutine = StartCoroutine(ShowAndHide());
    }

    private IEnumerator ShowAndHide()
    {
        // Fade-in
        yield return StartCoroutine(FadeTo(1f, fadeInTime));

        // Esperar tiempo visible
        yield return new WaitForSeconds(displayDuration);

        // Fade-out
        yield return StartCoroutine(FadeTo(0f, fadeOutTime));
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        if (panelGroup == null) yield break;

        float start = panelGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            panelGroup.alpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        panelGroup.alpha = target;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  UTILIDADES
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Elimina la extensión del nombre del archivo del clip si la tuviera
    /// (Unity normalmente ya la quita, pero por si acaso).
    /// </summary>
    private string CleanTrackName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        // Quitar extensión si quedó (por ej. "cancion.mp3")
        int dot = raw.LastIndexOf('.');
        if (dot > 0) raw = raw.Substring(0, dot);
        // Reemplazar guiones bajos por espacios
        raw = raw.Replace('_', ' ');
        return raw;
    }
}
