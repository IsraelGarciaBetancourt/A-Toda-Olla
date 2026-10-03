using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Elimina automáticamente e inmediatamente todas las esferas 'Bevel_' de la escena
/// y de cualquier prefab en el Editor de Unity.
/// </summary>
[InitializeOnLoad]
public static class CleanBevelSpheresTool
{
    static CleanBevelSpheresTool()
    {
        EditorApplication.delayCall += CleanAllBevelSpheresInScene;
    }

    [MenuItem("Tools/A-Toda-Olla/Eliminar Esferas Bevel Ahora")]
    public static void CleanAllBevelSpheresInScene()
    {
        int deletedCount = 0;

        // 1. Buscar todos los objetos que comiencen por 'Bevel_' en toda la escena activa
        var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        List<GameObject> toDestroy = new List<GameObject>();

        foreach (var go in allGOs)
        {
            if (go != null && go.name.StartsWith("Bevel_"))
            {
                toDestroy.Add(go);
            }
        }

        // 2. Buscar específicamente bajo cualquier DeliveryCar o VehicleController
        foreach (var vc in Object.FindObjectsByType<VehicleController>(FindObjectsInactive.Include))
        {
            if (vc == null) continue;
            for (int i = vc.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = vc.transform.GetChild(i);
                if (child != null && child.name.StartsWith("Bevel_"))
                {
                    if (!toDestroy.Contains(child.gameObject))
                    {
                        toDestroy.Add(child.gameObject);
                    }
                }
            }
        }

        foreach (var go in toDestroy)
        {
            if (go != null)
            {
                Undo.DestroyObjectImmediate(go);
                deletedCount++;
            }
        }

        if (deletedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[CleanBevelSpheresTool] Se eliminaron con éxito {deletedCount} esferas Bevel del vehículo.");
        }
    }
}
