using UnityEngine;

/// <summary>
/// Datos de una emisora de radio. Crear desde:
/// clic derecho en el Project → Create → A-Toda-Olla → Radio Station
/// </summary>
[CreateAssetMenu(fileName = "NewRadioStation", menuName = "A-Toda-Olla/Radio Station", order = 10)]
public class RadioStation : ScriptableObject
{
    [Header("Identidad de la Emisora")]
    [Tooltip("Nombre que aparece en el HUD al sintonizar esta radio.")]
    public string stationName = "Radio Olla FM";

    [Tooltip("Color del texto del nombre en el HUD.")]
    public Color stationColor = new Color(1f, 0.85f, 0.2f);

    [Header("Canciones")]
    [Tooltip("Arrastra aquí los AudioClip (.mp3 / .ogg) de esta emisora. Se reproducen en orden aleatorio sin repetir dos veces seguidas la misma canción. Deja vacío para 'Radio Apagada'.")]
    public AudioClip[] tracks;

    [Header("Radio Apagada")]
    [Tooltip("Si está activo, esta emisora es tratada como 'Radio Apagada' (silencio total). El array de tracks se ignora.")]
    public bool isSilent = false;

    /// <summary>
    /// True cuando la emisora no tiene canciones o está marcada como silenciosa.
    /// </summary>
    public bool IsEmpty => isSilent || tracks == null || tracks.Length == 0;
}
