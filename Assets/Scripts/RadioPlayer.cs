using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de radio estilo GTA para A-Toda-Olla.
/// 
/// OPTIMIZACIONES DE RENDIMIENTO (CERO CONSUMO DE CPU / SILENCIO DE VENTILADORES):
///   - Cero bucles en Update para emisoras inactivas en segundo plano.
///     El progreso virtual de cada emisora se calcula bajo demanda al sintonizarla,
///     eliminando ticks continuos por frame.
///   - Prevención absoluta de audio thrashing: fin de canción detectado con temporizador
///     y bandera de transición, evitando llamadas repetidas a AudioSource.Play().
///   - AudioSource configurado en modo 2D nativo sin filtros espaciales innecesarios.
///   - Fades de volumen matemáticos en Update sin creación continua de Coroutines ni GC.
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
        public float lastRecordedTime = 0f;
        public AudioClip lastPlayedClip = null;

        public void Init(RadioStation st, bool randomizeOffset)
        {
            station = st;
            playlist.Clear();
            currentTrackIndex = 0;
            playbackTime = 0f;
            lastRecordedTime = Time.time;
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

            for (int i = 0; i < station.tracks.Length; i++)
            {
                var t = station.tracks[i];
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
        /// Avanza el reloj virtual de esta emisora por dt segundos de forma instantánea.
        /// </summary>
        public void AdvanceVirtualTime(float dt)
        {
            if (dt <= 0f || station == null || station.IsEmpty || playlist.Count == 0) return;

            AudioClip clip = GetCurrentClip();
            if (clip == null || clip.length <= 0.05f) return;

            playbackTime += dt;

            // Avanzar pistas si el tiempo transcurrido superó la duración
            int safetyCounter = 0;
            while (clip != null && clip.length > 0.05f && playbackTime >= clip.length && safetyCounter < 50)
            {
                safetyCounter++;
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

            lastRecordedTime = Time.time;
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

            lastRecordedTime = Time.time;
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
    [Tooltip("Si está activo, al iniciar la partida las radios ya habrán comenzado a mitad de una canción aleatoria.")]
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
    public float fadeOutDuration = 0.6f;

    [Tooltip("Duración del fade-in al encender la radio (al entrar al vehículo).")]
    [Range(0f, 2f)]
    public float fadeInDuration = 0.35f;

    // ──────────────────────────────────────────────────────────────────────
    //  EVENTOS PÚBLICOS
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
    private bool isAdvancingTrack = false;
    private bool isSongStarting = false;

    // Temporizadores de reproducción
    private float songStartTime = 0f;
    private float expectedSongEndTime = 0f;

    // Sistema de fade sin coroutines (cero GC, cero CPU)
    private bool isFading = false;
    private float fadeTimer = 0f;
    private float fadeTotalDuration = 0.2f;
    private float fadeStartVol = 0f;
    private float fadeTargetVol = 0f;
    private bool stopAudioOnFadeEnd = false;

    // Temporizador de cambio de emisora
    private float switchWaitTimer = 0f;
    private int pendingStationIndex = -1;

    // ──────────────────────────────────────────────────────────────────────
    //  PROPIEDADES PÚBLICAS
    // ──────────────────────────────────────────────────────────────────────

    public RadioStation CurrentStation =>
        (stations != null && stations.Length > 0 && currentStationIndex >= 0 && currentStationIndex < stations.Length)
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

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        if (musicSource != null && isActive && !isSwitchingStation && !isFading)
        {
            musicSource.volume = volume;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ──────────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Configuración hiper-optimizada del AudioSource:
        // Cero efectos espaciales, máxima prioridad, bypass total de filtros
        musicSource = GetComponent<AudioSource>();
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = false;
        musicSource.playOnAwake = false;
        musicSource.volume = 0f;
        musicSource.spatialBlend = 0f; // Estéreo 2D puro
        musicSource.bypassEffects = true;
        musicSource.bypassListenerEffects = true;
        musicSource.bypassReverbZones = true;
        musicSource.priority = 0; // Prioridad máxima
        musicSource.clip = null;

        staticSource = gameObject.AddComponent<AudioSource>();
        staticSource.loop = false;
        staticSource.playOnAwake = false;
        staticSource.volume = staticVolume;
        staticSource.spatialBlend = 0f;
        staticSource.bypassEffects = true;

        currentStationIndex = Mathf.Clamp(startStationIndex, 0, Mathf.Max(0, StationCount - 1));
        EnsureStationStatesInitialized();
    }

    void Update()
    {
        // 1. Manejo ultra-ligero del Fade de volumen (sin coroutines ni allocations)
        if (isFading && musicSource != null)
        {
            fadeTimer += Time.unscaledDeltaTime;
            float t = fadeTotalDuration > 0.001f ? Mathf.Clamp01(fadeTimer / fadeTotalDuration) : 1f;
            musicSource.volume = Mathf.Lerp(fadeStartVol, fadeTargetVol, t);

            if (t >= 1f)
            {
                isFading = false;
                if (stopAudioOnFadeEnd)
                {
                    musicSource.Stop();
                    musicSource.clip = null;
                }
            }
        }

        // 2. Manejo de transición de cambio de emisora (estática)
        if (isSwitchingStation)
        {
            switchWaitTimer -= Time.unscaledDeltaTime;
            if (switchWaitTimer <= 0f)
            {
                CompleteStationSwitch();
            }
            return;
        }

        // Si la radio está apagada, no gastar ni un solo ciclo de CPU
        if (!isActive) return;

        // 3. Controles del jugador (R / Shift+R o D-Pad)
        HandleInput();

        // 4. Avance automático y controlado de canción en la emisora activa
        // IMPORTANTE: Se usa temporizador + verificación de buffer para evitar audio-thrashing
        if (!isSwitchingStation && !isAdvancingTrack && musicSource != null && musicSource.clip != null)
        {
            if (isSongStarting)
            {
                // Margen de gracia inicial de 0.5s para que FMOD comience el playback sin falsos positivos
                if (Time.time - songStartTime >= 0.5f)
                {
                    isSongStarting = false;
                }
            }
            else
            {
                bool timeCompleted = (Time.time >= expectedSongEndTime);
                bool naturallyEnded = (!musicSource.isPlaying && musicSource.time <= 0.05f);

                if (timeCompleted || naturallyEnded)
                {
                    isAdvancingTrack = true;
                    StationPlaybackState activeState = GetCurrentState();
                    if (activeState != null && activeState.station != null && !activeState.station.IsEmpty)
                    {
                        activeState.AdvanceToNextTrack();
                        PlayCurrentStationTrack(activeState);
                    }
                }
            }
        }
    }

    private void HandleInput()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
                PrevStation();
            else
                NextStation();
            return;
        }

        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.dpad.right.wasPressedThisFrame)
                NextStation();
            else if (gp.dpad.left.wasPressedThisFrame)
                PrevStation();
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  API PÚBLICA
    // ──────────────────────────────────────────────────────────────────────

    public void SetActive(bool active)
    {
        isActive = active;
        EnsureStationStatesInitialized();

        if (active)
        {
            StationPlaybackState state = GetCurrentState();
            RadioStation station = state != null ? state.station : null;

            OnStationChanged?.Invoke(station, currentStationIndex);

            if (station != null && !station.IsEmpty)
            {
                // Sincronizar tiempo virtual transcurrido mientras estuvo apagada
                float elapsed = Time.time - state.lastRecordedTime;
                if (elapsed > 0.05f)
                {
                    state.AdvanceVirtualTime(elapsed);
                }

                PlayCurrentStationTrack(state);
                StartVolumeFade(0f, volume, fadeInDuration, false);
            }
            else
            {
                if (musicSource != null)
                {
                    musicSource.Stop();
                    musicSource.clip = null;
                }
                OnTrackChanged?.Invoke(string.Empty);
            }
        }
        else
        {
            // Guardar punto de reproducción antes de apagar
            StationPlaybackState state = GetCurrentState();
            if (state != null && musicSource != null && musicSource.isPlaying)
            {
                state.playbackTime = musicSource.time;
                state.lastRecordedTime = Time.time;
            }

            StartVolumeFade(musicSource != null ? musicSource.volume : 0f, 0f, fadeOutDuration, true);
        }
    }

    public void NextStation()
    {
        if (StationCount == 0 || isSwitchingStation) return;
        int next = (currentStationIndex + 1) % StationCount;
        TuneToStation(next);
    }

    public void PrevStation()
    {
        if (StationCount == 0 || isSwitchingStation) return;
        int prev = (currentStationIndex - 1 + StationCount) % StationCount;
        TuneToStation(prev);
    }

    public void TuneToStation(int index)
    {
        if (StationCount == 0) return;
        index = Mathf.Clamp(index, 0, StationCount - 1);
        if (index == currentStationIndex && !isSwitchingStation) return;

        // 1. Guardar estado de la emisora saliente
        StationPlaybackState prevState = GetCurrentState();
        if (prevState != null)
        {
            if (musicSource != null && musicSource.isPlaying)
            {
                prevState.playbackTime = musicSource.time;
            }
            prevState.lastRecordedTime = Time.time;
        }

        // 2. Parar audio actual y reproducir estática
        if (musicSource != null)
        {
            musicSource.Stop();
            musicSource.clip = null;
        }

        PlayStatic();

        // 3. Preparar cambio
        pendingStationIndex = index;
        isSwitchingStation = true;
        switchWaitTimer = (staticClip != null)
            ? Mathf.Clamp(staticClip.length * 0.45f, 0.15f, 0.8f)
            : 0.22f;

        // Notificar al HUD inmediatamente del cambio de emisora
        RadioStation pendingStation = (pendingStationIndex < stations.Length) ? stations[pendingStationIndex] : null;
        OnStationChanged?.Invoke(pendingStation, pendingStationIndex);
    }

    private void CompleteStationSwitch()
    {
        isSwitchingStation = false;
        currentStationIndex = pendingStationIndex;
        pendingStationIndex = -1;

        StationPlaybackState newState = GetCurrentState();
        RadioStation station = newState != null ? newState.station : null;

        if (station == null || station.IsEmpty)
        {
            OnTrackChanged?.Invoke(string.Empty);
            return;
        }

        // Sincronizar tiempo virtual transcurrido en segundo plano
        float elapsed = Time.time - newState.lastRecordedTime;
        if (elapsed > 0.05f)
        {
            newState.AdvanceVirtualTime(elapsed);
        }

        PlayCurrentStationTrack(newState);
    }

    private void PlayCurrentStationTrack(StationPlaybackState state)
    {
        isAdvancingTrack = false;

        if (state == null || state.station == null || state.station.IsEmpty)
        {
            if (musicSource != null)
            {
                musicSource.Stop();
                musicSource.clip = null;
            }
            OnTrackChanged?.Invoke(string.Empty);
            return;
        }

        AudioClip clip = state.GetCurrentClip();
        if (clip == null)
        {
            if (musicSource != null)
            {
                musicSource.Stop();
                musicSource.clip = null;
            }
            OnTrackChanged?.Invoke(string.Empty);
            return;
        }

        // Si faltaba menos de 0.3s para terminar, avanzar a la siguiente
        if (state.playbackTime >= clip.length - 0.3f)
        {
            state.AdvanceToNextTrack();
            clip = state.GetCurrentClip();
            if (clip == null) return;
        }

        float seekTime = Mathf.Clamp(state.playbackTime, 0f, Mathf.Max(0f, clip.length - 0.1f));

        musicSource.clip = clip;
        musicSource.time = seekTime;
        musicSource.volume = volume;
        musicSource.Play();

        songStartTime = Time.time;
        expectedSongEndTime = Time.time + Mathf.Max(0.5f, clip.length - seekTime);
        isSongStarting = true;
        state.lastRecordedTime = Time.time;

        OnTrackChanged?.Invoke(clip.name);
    }

    private void PlayStatic()
    {
        if (staticClip == null || staticSource == null) return;
        staticSource.volume = staticVolume;
        staticSource.clip = staticClip;
        staticSource.Play();
    }

    private void StartVolumeFade(float from, float to, float duration, bool stopAfter)
    {
        isFading = true;
        fadeTimer = 0f;
        fadeTotalDuration = Mathf.Max(0.01f, duration);
        fadeStartVol = from;
        fadeTargetVol = to;
        stopAudioOnFadeEnd = stopAfter;
        if (musicSource != null) musicSource.volume = from;
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
}
