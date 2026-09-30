using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de Editor: reemplaza MeshColliders en los prefabs de carretera
/// por BoxColliders alineados perfectamente a los Bounds del mesh.
///
/// Menu: Tools > Fix Road Colliders
/// </summary>
public static class RoadColliderFixer
{
    private const string ROAD_PREFAB_FOLDER = "Assets/Prefabs/Roads";

    [MenuItem("Tools/Fix Road Colliders")]
    public static void FixAll()
    {
        int totalFixed   = 0;
        int totalSkipped = 0;
        var report       = new System.Text.StringBuilder();
        report.AppendLine("=== Road Collider Fix Report ===\n");

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ROAD_PREFAB_FOLDER });

        foreach (string guid in guids)
        {
            string path   = AssetDatabase.GUIDToAssetPath(guid);
            string result = ProcessPrefab(path);

            if (result.StartsWith("FIXED"))
            {
                totalFixed++;
                report.AppendLine($"  CHECK {System.IO.Path.GetFileName(path)}: {result}");
            }
            else if (result.StartsWith("SKIP"))
            {
                totalSkipped++;
                report.AppendLine($"  SKIP {System.IO.Path.GetFileName(path)}: {result}");
            }
            else
            {
                report.AppendLine($"  ERROR {System.IO.Path.GetFileName(path)}: {result}");
            }
        }

        report.AppendLine($"\nTotal: {totalFixed} fixed, {totalSkipped} skipped.");
        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog(
            "Fix Road Colliders - Done",
            $"Procesados: {totalFixed + totalSkipped} prefabs\n" +
            $"Corregidos: {totalFixed}\n" +
            $"Sin cambios: {totalSkipped}\n\n" +
            "Revisa la Consola para el reporte detallado.",
            "OK");
    }

    private static string ProcessPrefab(string assetPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
        if (root == null)
            return "ERROR: No se pudo cargar el prefab.";

        try
        {
            MeshCollider[] meshCols = root.GetComponentsInChildren<MeshCollider>(true);

            if (meshCols.Length == 0)
            {
                BoxCollider[] existingBoxes = root.GetComponentsInChildren<BoxCollider>(true);
                return $"SKIP: Ya tiene {existingBoxes.Length} BoxCollider(s), sin MeshColliders.";
            }

            var details = new List<string>();

            foreach (MeshCollider mc in meshCols)
            {
                GameObject go = mc.gameObject;
                Bounds bounds = GetMeshBounds(mc, go);

                Vector3 localCenter = bounds.center;
                Vector3 localSize   = bounds.size;

                // Garantizar que el techo del collider sea Y=0 (nivel del suelo)
                // y el box baje hacia abajo (para tiles planos la Y deberia ser negativa o 0)
                // Solo corregir si el centro esta muy alto o el size es sospechoso
                if (localSize.y < 0.01f) localSize.y = 0.5f;

                BoxCollider existingBox = go.GetComponent<BoxCollider>();

                if (existingBox != null)
                {
                    existingBox.center    = localCenter;
                    existingBox.size      = localSize;
                    existingBox.isTrigger = mc.isTrigger;
                    Object.DestroyImmediate(mc);
                    details.Add($"'{go.name}': actualizado (center={localCenter:F2} size={localSize:F2})");
                }
                else
                {
                    BoxCollider newBox    = go.AddComponent<BoxCollider>();
                    newBox.center         = localCenter;
                    newBox.size           = localSize;
                    newBox.isTrigger      = mc.isTrigger;
                    newBox.sharedMaterial = mc.sharedMaterial;
                    Object.DestroyImmediate(mc);
                    details.Add($"'{go.name}': nuevo BoxCollider (center={localCenter:F2} size={localSize:F2})");
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            return $"FIXED {meshCols.Length} collider(s): {string.Join(" | ", details)}";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Bounds GetMeshBounds(MeshCollider mc, GameObject go)
    {
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            return mf.sharedMesh.bounds;

        if (mc.sharedMesh != null)
            return mc.sharedMesh.bounds;

        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            Bounds wb = rend.bounds;
            Vector3 localMin = go.transform.InverseTransformPoint(wb.min);
            Vector3 localMax = go.transform.InverseTransformPoint(wb.max);
            Bounds local = new Bounds();
            local.SetMinMax(localMin, localMax);
            return local;
        }

        Debug.LogWarning($"[RoadColliderFixer] Sin bounds para '{go.name}', usando box por defecto.");
        return new Bounds(Vector3.zero, Vector3.one);
    }

    [MenuItem("Tools/Fix Road Colliders (Preview Only)")]
    public static void PreviewAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ROAD_PREFAB_FOLDER });
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== PREVIEW: Road Collider Fix (sin modificar) ===\n");

        int withMesh = 0, withBox = 0, noCol = 0;

        foreach (string guid in guids)
        {
            string path  = AssetDatabase.GUIDToAssetPath(guid);
            string pName = System.IO.Path.GetFileName(path);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            try
            {
                var meshCols = root.GetComponentsInChildren<MeshCollider>(true);
                var boxCols  = root.GetComponentsInChildren<BoxCollider>(true);

                if (meshCols.Length > 0)
                {
                    withMesh++;
                    sb.AppendLine($"  [MESH->BOX] {pName}:");
                    foreach (var mc in meshCols)
                    {
                        Bounds b = GetMeshBounds(mc, mc.gameObject);
                        sb.AppendLine($"     '{mc.gameObject.name}' convex={mc.convex} -> BoxCollider(center={b.center:F3}, size={b.size:F3})");
                    }
                }
                else if (boxCols.Length > 0)
                {
                    withBox++;
                    sb.AppendLine($"  [OK-BOX]  {pName}: {boxCols.Length} BoxCollider(s), sin cambios");
                    foreach (var bc in boxCols)
                        sb.AppendLine($"     '{bc.gameObject.name}' center={bc.center:F3} size={bc.size:F3}");
                }
                else
                {
                    noCol++;
                    sb.AppendLine($"  [SIN-COL] {pName}: sin colliders");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        sb.AppendLine($"\nResumen: {withMesh} con MeshCollider, {withBox} con BoxCollider, {noCol} sin collider.");
        Debug.Log(sb.ToString());
    }
}
