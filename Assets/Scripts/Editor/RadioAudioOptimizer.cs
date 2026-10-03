using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Optimiza automáticamente los archivos de audio en Assets/Audio/Radio/
/// para que utilicen Streaming, 2D y compresión Vorbis equilibrada.
/// Esto evita que el motor de Unity descomprima canciones completas de 5-40 MB en la memoria RAM
/// o ejecute cálculos espaciales 3D innecesarios, eliminando sobrecalentamiento del CPU en Mac/PC.
/// </summary>
public class RadioAudioOptimizer : AssetPostprocessor
{
    private const string RADIO_AUDIO_FOLDER = "Assets/Audio/Radio";

    private void OnPreprocessAudio()
    {
        if (assetPath.StartsWith(RADIO_AUDIO_FOLDER))
        {
            AudioImporter audioImporter = (AudioImporter)assetImporter;
            ApplyRadioAudioSettings(audioImporter);
        }
    }

    [MenuItem("Tools/A-Toda-Olla/Optimizar Audios de Radio")]
    public static void OptimizeAllRadioClips()
    {
        if (!Directory.Exists(RADIO_AUDIO_FOLDER)) return;

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { RADIO_AUDIO_FOLDER });
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                if (ApplyRadioAudioSettings(importer))
                {
                    importer.SaveAndReimport();
                    count++;
                }
            }
        }

        Debug.Log($"[RadioAudioOptimizer] Se optimizaron {count} clips de radio para Streaming 2D de bajo consumo de CPU.");
    }

    private static bool ApplyRadioAudioSettings(AudioImporter importer)
    {
        bool changed = false;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;

        // 1. Streaming: Cero impacto de descompresión en memoria, carga mínima de CPU
        if (settings.loadType != AudioClipLoadType.Streaming)
        {
            settings.loadType = AudioClipLoadType.Streaming;
            changed = true;
        }

        // 2. Compresión Vorbis al 70% (calidad cristalina y decodificación ultra liviana)
        if (settings.compressionFormat != AudioCompressionFormat.Vorbis)
        {
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            changed = true;
        }

        if (Mathf.Abs(settings.quality - 0.7f) > 0.05f)
        {
            settings.quality = 0.7f;
            changed = true;
        }

        // 3. Preload en settings (Unity 6 per-platform sample settings)
        if (!settings.preloadAudioData)
        {
            settings.preloadAudioData = true;
            changed = true;
        }

        // 4. Carga en segundo plano
        if (!importer.loadInBackground)
        {
            importer.loadInBackground = true;
            changed = true;
        }

        if (changed)
        {
            importer.defaultSampleSettings = settings;
        }

        return changed;
    }
}
