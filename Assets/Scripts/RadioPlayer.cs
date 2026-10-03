using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de radio estilo GTA para A-Toda-Olla.
///
/// SIMULACIÓN DE RADIO REAL EN SEGUNDO PLANO:
///   - Cada emisora mantiene su propia transmisión continua en tiempo real.
///   - Si te bajas del vehículo a entregar un pedido a mitad de una canción,
///     el reloj virtual de la emisora sigue avanzando en segundo plano.
///   - Al volver a subirte al vehículo, la canción continuará exactamente
///     en el punto en el que va (o habrá avanzado a la siguiente si ya terminó).
///   - Lo mismo ocurre al cambiar entre emisoras: cada una transmite independientemente.
///   - Sistema aleatorio Fisher-Yates que garantiza que una misma canción
///     nunca se repita dos veces consecutivas.
///
/// CONTROLES:
///   - Teclado: R (Siguiente emisora), Shift+R (Emisora anterior)
///   - Mando: D-Pad Derecha (Siguiente), D-Pad Izquierda (Anterior)
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class RadioPlayer : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────────
    //  CLASE DE ESTADO DE EMISORA (TRANSMISIÓN VIRTUAL INDEPENDIENTE)
    // ──────────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class StationPlaybackState
    {
        public RadioStation station;
        public List<AudioClip> playlist = new List<AudioClip>();
        public int currentTrackIndex = 0;
        public float playbackTime = 0f;
        public AudioClip lastPlayedClip = null;

        public void Init(RadioStation st, bool randomizeOffset)
        {
            station = st;
            playlist.Clear();
            currentTrackIndex = 0;
            playbackTime = 0f;
            lastPlayedClip = null;

            if (st == null || st.IsEmpty) return;

            RebuildPlaylist();

            if (playlist.Count > 0 && randomizeOffset)
            {
                currentTrackIndex = Random.Range(0, playlist.Count);
                AudioClip clip = playlist[currentTrackIndex];
                if (clip != null && clip.length > 2f)
                {
                    playbackTime = Random.Range(0f, clip.length * 0.75f);
                }
            }
        }

        public void RebuildPlaylist()
        {
            playlist.Clear();
            if (station == null || station.tracks == null) return;

            foreach (var t in station.tracks)
            {
                if (t != null) playlist.Add(t);
            }

            if (playlist.Count == 0) return;

            // Fisher-Yates Shuffle
            for (int i = playlist.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                AudioClip tmp = playlist[i];
                playlist[i] = playlist[j];
                playlist[j] = tmp;
            }

            // Evitar que la primera canción de la nueva lista sea igual a la última reproducida
            if (lastPlayedClip != null && playlist.Count > 1 && playlist[0] == lastPlayedClip)
            {
                int swapIndex = Random.Range(1, playlist.Count);
                AudioClip tmp = playlist[0];
                playlist[0] = playlist[swapIndex];
                playlist[swapIndex] = tmp;
            }
        }

        public AudioClip GetCurrentClip()
        {
            if (playlist.Count == 0) return null;
            if (currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
                currentTrackIndex = 0;
            return playlist[currentTrackIndex];
        }

        /// <summary>
        /// Avanza el reloj virtual de esta emisora por dt segundos.
        /// Si la canción termina, avanza a la siguiente canción en la lista.
        /// </summary>
        public void AdvanceVirtualTime(float dt)
        {
            if (station == null || station.IsEmpty || playlist.Count == 0) return;

            AudioClip clip = GetCurrentClip();
            if (clip == null || clip.length <= 0.05f) return;

            playbackTime += dt;

            // Si sobrepasa la duración de la canción actual, pasar a las siguientes
            while (clip != null && clip.length > 0.05f && playbackTime >= clip.length)
            {
                playbackTime -= clip.length;
                lastPlayedClip = clip;
                currentTrackIndex++;

                if (currentTrackIndex >= playlist.Count)
                {
                    AudioClip prevLast = playlist[playlist.Count - 1];
                    RebuildPlaylist();
                    if (prevLast != null && playlist.Count > 1 && playlist[0] == prevLast)
                    {
                        int swapIndex = Random.Range(1, playlist.Count);
                        AudioClip tmp = playlist[0];
                        playlist[0] = playlist[swapIndex];
                        playlist[swapIndex] = tmp;
                    }
                    currentTrackIndex = 0;
                }

                clip = GetCurrentClip();
            }
        }

        public void AdvanceToNextTrack()
        {
            if (playlist.Count == 0) return;

            lastPlayedClip = GetCurrentClip();
            currentTrackIndex++;
            playbackTime = 0f;

            if (currentTrackIndex >= playlist.Count)
            {
                AudioClip prevLast = playlist[playlist.Count - 1];
                RebuildPlaylist();
                if (prevLast != null && playlist.Count > 1 && playlist[0] == prevLast)
                {
                    int swapIndex = Random.Range(1, playlist.Count);
                    AudioClip tmp = playlist[0];
                    playlist[0] = playlist[swapIndex];
                    playlist[swapIndex] = tmp;
                }
                currentTrackIndex = 0;
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  INSPECTOR
    // ──────────────────────────────────────────────────────────────────────

    [Header("Emisoras")]
    [Tooltip("Lista de emisoras en orden. Arrastra RadioStation assets aquí.")]
    public RadioStation[] stations;

    [Tooltip("Índice de la emisora activa al arrancar el juego.")]
    [Min(0)]
    public int startStationIndex = 0;

    [Header("Simulación de Radio en Vivo")]
    [Tooltip("Si está activo, al iniciar la partida las radios ya habrán comenzado a mitad de una canción aleatoria (como una radio real en directo). Si está desactivado, comienzan desde el segundo 0 al iniciar el juego.")]
    public bool randomizeInitialOffset = false;

    [Header("Audio")]
    [Tooltip("Volumen de la radio (0 = mudo, 1 = máximo).")]
    [Range(0f, 1f)]
    public float volume = 0.8f;

    [Tooltip("Clip de ruido/estática que suena al cambiar de emisora. Asigna un .ogg/.mp3.")]
    public AudioClip staticClip;

    [Tooltip("Volumen del ruido de estática al cambiar.")]
    [Range(0f, 1f)]
    public float staticVolume = 1f;

    [Tooltip("Duración del fade-out al apagar la radio (al salir del vehículo).")]
    [Range(0f, 3f)]
    public float fadeOutDuration = 0.8f;

    [Tooltip("Duración del fade-in al encender la radio (al entrar al vehículo).")]
    [Range(0f, 2f)]
    public float fadeInDuration = 0.4f;

    /// <summary>
    /// Ajusta el volumen de la radio en tiempo real (0 a 1).
    /// </summary>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        if (musicSource != null && isActive && !isSwitchingStation)
        {
            musicSource.volume = volume;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  EVENTOS PÚBLICOS (el RadioHUD los escucha)
    // ──────────────────────────────────────────────────────────────────────

    public delegate void StationChangedHandler(RadioStation station, int index);
    public event StationChangedHandler OnStationChanged;

    public delegate void TrackChangedHandler(string trackName);
    public event TrackChangedHandler OnTrackChanged;

    // ──────────────────────────────────────────────────────────────────────
    //  ESTADO INTERNO
    // ──────────────────────────────────────────────────────────────────────

    private AudioSource musicSource;
    private AudioSource staticSource;

    private int currentStationIndex = 0;
    private StationPlaybackState[] stationStates;

    private bool isActive = false;
    private bool isSwitchingStation = false;

    private Coroutine fadeCoroutine;
    private Coroutine switchCoroutine;
    private float lastVirtualUpdateTime = 0f;

    // ──────────────────────────────────────────────────────────────────────
    //  PROPIEDADES PÚBLICAS (para el HUD)
    // ──────────────────────────────────────────────────────────────────────

    public RadioStation CurrentStation =>
        (stations != null && stations.Length > 0 && currentStationIndex < stations.Length)
            ? stations[currentStationIndex]
            : null;

    public string CurrentTrackName
    {
        get
        {
            StationPlaybackState state = GetCurrentState();
            if (state != null)
            {
                AudioClip clip = state.GetCurrentClip();
                if (clip != null) return clip.name;
            }
            return string.Empty;
        }
    }

    public int StationCount => stations != null ? stations.Length : 0;
    public bool IsActive => isActive;

    // ──────────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ──────────────────────────────────────────────────────────────────────

    void Awake()
    {
        // AudioSource principal (música de radio)
        musicSource = GetComponent<AudioSource>();
        musicSource.loop = false;
        musicSource.playOnAwake = false;
        musicSource.volume = 0f;
        musicSource.spatialBlend = 0f;
        musicSource.clip = null;

        // AudioSource secundario (estática de cambio)
        staticSource = gameObject.AddComponent<AudioSource>();
        staticSource.loop = false;
        staticSource.playOnAwake = false;
        staticSource.volume = staticVolume;
        staticSource.spatialBlend = 0f;

        currentStationIndex = Mathf.Clamp(startStationIndex, 0, Mathf.Max(0, StationCount - 1));
        EnsureStationStatesInitialized();
        lastVirtualUpdateTime = Time.time;
    }

    void Update()
    {
        // ──────────────────────────────────────────────────────────────────
        // 1. TRANSMISIÓN EN TIEMPO REAL CONTINUA PARA TODAS LAS EMISORAS
        // ──────────────────────────────────────────────────────────────────
        EnsureStationStatesInitialized();

        float dt = Time.time - lastVirtualUpdateTime;
        lastVirtualUpdateTime = Time.time;
        if (dt > 1f) dt = Time.deltaTime; // Protección contra pausas largas o pantallas de carga

        for (int i = 0; i < stationStates.Length; i++)
        {
            var stState = stationStates[i];
            if (stState == null) continue;

            // Si esta es la emisora actualmente sonando en vivo por el AudioSource
            if (isActive && i == currentStationIndex && musicSource != null && musicSource.isPlaying)
            {
                stState.playbackTime = musicSource.time;
            }
            else
            {
                // Emisoras en segundo plano avanzan virtualmente en tiempo real
                stState.AdvanceVirtualTime(dt);
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // 2. CONTROLES Y REPRODUCCIÓN (SOLO DENTRO DEL VEHÍCULO)
        // ──────────────────────────────────────────────────────────────────
        if (!isActive) return;

        // Entrada de teclado (R = Siguiente, Shift+R = Anterior)
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
                PrevStation();
            else
                NextStation();
        }

        // Entrada de mando (D-Pad Derecha = Siguiente, D-Pad Izquierda = Anterior)
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.dpad.right.wasPressedThisFrame)
                NextStation();
            else if (gp.dpad.left.wasPressedThisFrame)
                PrevStation();
        }

        // ──────────────────────────────────────────────────────────────────
        // 3. AVANCE AUTOMÁTICO DE CANCIÓN EN LA EMISORA ACTIVA
        // ──────────────────────────────────────────────────────────────────
        if (!isSwitchingStation && musicSource != null && musicSource.clip != null && !musicSource.isPlaying)
        {
            StationPlaybackState activeState = GetCurrentState();
            if (activeState != null && activeState.station != null && !activeState.station.IsEmpty)
            {
                activeState.AdvanceToNextTrack();
                PlayCurrentStationTrack(activeState);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  API PÚBLICA
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enciende o apaga la radio. Llamar al entrar o salir del vehículo.
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;
        EnsureStationStatesInitialized();

        if (active)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

            StationPlaybackState state = GetCurrentState();
            RadioStation station = state != null ? state.station : null;

            OnStationChanged?.Invoke(station, currentStationIndex);

            if (station != null && !station.IsEmpty)
            {
                PlayCurrentStationTrack(state);
                fadeCoroutine = StartCoroutine(FadeIn(fadeInDuration));
            }
            else
            {
                musicSource.Stop();
                musicSource.clip = null;
                OnTrackChanged?.Invoke(string.Empty);
            }
        }
        else
        {
            // Sincronizar el tiempo actual antes de apagar el audio
            StationPlaybackState state = GetCurrentState();
            if (state != null && musicSource != null && musicSource.isPlaying)
            {
                state.playbackTime = musicSource.time;
            }

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeOut(fadeOutDuration, stopAfter: true));
        }
    }

    /// <summary>Cambia a la siguiente emisora con efecto de estática.</summary>
    public void NextStation()
    {
        if (StationCount == 0) return;
        int next = (currentStationIndex + 1) % StationCount;
        TuneToStation(next);
    }

    /// <summary>Cambia a la emisora anterior con efecto de estática.</summary>
    public void PrevStation()
    {
        if (StationCount == 0) return;
        int prev = (currentStationIndex - 1 + StationCount) % StationCount;
        TuneToStation(prev);
    }

    /// <summary>Sintoniza una emisora por su índice.</summary>
    public void TuneToStation(int index)
    {
        if (StationCount == 0) return;
        index = Mathf.Clamp(index, 0, StationCount - 1);

        if (switchCoroutine != null) StopCoroutine(switchCoroutine);
        switchCoroutine = StartCoroutine(SwitchStationRoutine(index));
    }

    // ──────────────────────────────────────────────────────────────────────
    //  LÓGICA INTERNA DE SINTONIZACIÓN Y REPRODUCCIÓN
    // ──────────────────────────────────────────────────────────────────────

    private IEnumerator SwitchStationRoutine(int newIndex)
    {
        isSwitchingStation = true;

        // 1. Guardar la posición de la emisora anterior
        StationPlaybackState prevState = GetCurrentState();
        if (prevState != null && musicSource != null && musicSource.isPlaying)
        {
            prevState.playbackTime = musicSource.time;
        }

        // 2. Parar música actual y reproducir estática
        musicSource.Stop();
        musicSource.clip = null;
        PlayStatic();

        // 3. Cambiar índice y notificar al HUD
        currentStationIndex = newIndex;
        StationPlaybackState newState = GetCurrentState();
        RadioStation station = newState != null ? newState.station : null;

        OnStationChanged?.Invoke(station, currentStationIndex);

        if (station == null || station.IsEmpty)
        {
            OnTrackChanged?.Invoke(string.Empty);
            isSwitchingStation = false;
            yield break;
        }

        // 4. Esperar brevemente durante el efecto de estática
        float waitTime = (staticClip != null)
            ? Mathf.Clamp(staticClip.length * 0.45f, 0.15f, 1.2f)
            : 0.25f;
        yield return new WaitForSeconds(waitTime);

        // 5. Reproducir la nueva emisora en su punto de transmisión en vivo
        PlayCurrentStationTrack(newState);

        isSwitchingStation = false;
    }

    private void PlayCurrentStationTrack(StationPlaybackState state)
    {
        if (state == null || state.station == null || state.station.IsEmpty)
        {
            musicSource.Stop();
            musicSource.clip = null;
            OnTrackChanged?.Invoke(string.Empty);
            return;
        }

        AudioClip clip = state.GetCurrentClip();
        if (clip == null)
        {
            musicSource.Stop();
            musicSource.clip = null;
            OnTrackChanged?.Invoke(string.Empty);
            return;
        }

        // Si faltaba menos de 0.2s para terminar la canción, pasar a la siguiente de inmediato
        if (state.playbackTime >= clip.length - 0.2f)
        {
            state.AdvanceToNextTrack();
            clip = state.GetCurrentClip();
            if (clip == null) return;
        }

        float seekTime = Mathf.Clamp(state.playbackTime, 0f, Mathf.Max(0f, clip.length - 0.05f));

        musicSource.clip = clip;
        musicSource.time = seekTime;
        musicSource.volume = volume;
        musicSource.Play();

        // En ciertos códecs/formatos en Unity, reasignar el seek post-Play previene resets a 0
        if (seekTime > 0.05f && Mathf.Abs(musicSource.time - seekTime) > 0.5f)
        {
            musicSource.time = seekTime;
        }

        OnTrackChanged?.Invoke(clip.name);
    }

    private void PlayStatic()
    {
        if (staticClip == null || staticSource == null) return;
        staticSource.volume = staticVolume;
        staticSource.clip = staticClip;
        staticSource.Play();
    }

    private StationPlaybackState GetCurrentState()
    {
        EnsureStationStatesInitialized();
        if (stationStates != null && currentStationIndex >= 0 && currentStationIndex < stationStates.Length)
        {
            return stationStates[currentStationIndex];
        }
        return null;
    }

    private void EnsureStationStatesInitialized()
    {
        if (stationStates == null || stationStates.Length != StationCount)
        {
            stationStates = new StationPlaybackState[StationCount];
            for (int i = 0; i < StationCount; i++)
            {
                stationStates[i] = new StationPlaybackState();
                stationStates[i].Init(stations[i], randomizeInitialOffset);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  FADES DE VOLUMEN
    // ──────────────────────────────────────────────────────────────────────

    private IEnumerator FadeIn(float duration)
    {
        float elapsed = 0f;
        musicSource.volume = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0f, volume, elapsed / duration);
            yield return null;
        }
        musicSource.volume = volume;
    }

    private IEnumerator FadeOut(float duration, bool stopAfter = false)
    {
        float startVol = musicSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
            yield return null;
        }
        musicSource.volume = 0f;
        if (stopAfter) musicSource.Stop();
    }
}
