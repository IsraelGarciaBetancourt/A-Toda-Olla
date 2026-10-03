using UnityEngine;
using TMPro;

/// <summary>
/// HUD de radio estilo GTA Vice City para A-Toda-Olla.
/// 
/// OPTIMIZACIONES DE RENDIMIENTO:
///   - Cero Coroutines: control de fade mediante temporizadores en LateUpdate.
///   - Cero allocations de memoria (evita recolector de basura de Unity).
///   - Comprobación de texto antes de reasignar a TextMeshPro para no reconstruir mallas innecesariamente.
/// </summary>
public class RadioHUD : MonoBehaviour
{
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
    public float displayDuration = 3.8f;

    [Tooltip("Duración del fade-in al aparecer.")]
    [Range(0.05f, 1f)]
    public float fadeInTime = 0.2f;

    [Tooltip("Duración del fade-out al desaparecer.")]
    [Range(0.1f, 2f)]
    public float fadeOutTime = 0.5f;

    [Header("Textos")]
    [Tooltip("Texto mostrado cuando la radio está apagada.")]
    public string offStationLabel = "Radio Apagada";

    [Tooltip("Prefijo mostrado antes del nombre de la canción.")]
    public string trackPrefix = "♪  ";

    private RadioPlayer radioPlayer;
    private float displayTimer = 0f;
    private string lastDisplayedTrack = null;

    void Awake()
    {
        if (panelGroup == null) panelGroup = GetComponent<CanvasGroup>();
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }
    }

    void Start()
    {
        radioPlayer = Object.FindAnyObjectByType<RadioPlayer>();
        if (radioPlayer != null)
        {
            radioPlayer.OnStationChanged += HandleStationChanged;
            radioPlayer.OnTrackChanged += HandleTrackChanged;
        }
    }

    void OnDestroy()
    {
        if (radioPlayer != null)
        {
            radioPlayer.OnStationChanged -= HandleStationChanged;
            radioPlayer.OnTrackChanged -= HandleTrackChanged;
        }
    }

    void LateUpdate()
    {
        if (panelGroup == null) return;

        // Temporizador de visualización
        if (displayTimer > 0f)
        {
            displayTimer -= Time.unscaledDeltaTime;
        }

        // Fade suave hacia 1 o hacia 0
        float targetAlpha = (displayTimer > 0f) ? 1f : 0f;
        float speed = (targetAlpha > panelGroup.alpha) ? (1f / Mathf.Max(0.05f, fadeInTime)) : (1f / Mathf.Max(0.05f, fadeOutTime));

        panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, targetAlpha, Time.unscaledDeltaTime * speed);
    }

    private void HandleStationChanged(RadioStation station, int index)
    {
        if (stationNameText != null)
        {
            if (station == null || station.IsEmpty)
            {
                if (stationNameText.text != offStationLabel)
                    stationNameText.text = offStationLabel;
                stationNameText.color = Color.gray;
            }
            else
            {
                if (stationNameText.text != station.stationName)
                    stationNameText.text = station.stationName;
                stationNameText.color = station.stationColor;
            }
        }

        if (trackNameText != null)
        {
            trackNameText.text = string.Empty;
        }

        lastDisplayedTrack = null;
        displayTimer = displayDuration;
    }

    private void HandleTrackChanged(string trackName)
    {
        if (trackName == lastDisplayedTrack) return;
        lastDisplayedTrack = trackName;

        if (trackNameText != null)
        {
            trackNameText.text = string.IsNullOrEmpty(trackName)
                ? string.Empty
                : trackPrefix + CleanTrackName(trackName);
        }

        if (!string.IsNullOrEmpty(trackName))
        {
            displayTimer = displayDuration;
        }
    }

    private string CleanTrackName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        int dot = raw.LastIndexOf('.');
        if (dot > 0) raw = raw.Substring(0, dot);
        return raw.Replace('_', ' ');
    }
}
