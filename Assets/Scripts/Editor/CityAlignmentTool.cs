using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Herramienta de Editor para inspeccionar y alinear la altura Y y escalas
/// de todos los objetos hijos de "City" en la jerarquía.
/// </summary>
public static class CityAlignmentTool
{
    private const string REPORT_PATH = "Assets/City_Alignment_Report.txt";

    private static GameObject FindCityRoot()
    {
        // Buscar "City" o "CITY" en los roots de la escena activa
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            if (root.name.Equals("City", System.StringComparison.OrdinalIgnoreCase))
                return root;
        }
        return null;
    }

    [MenuItem("Tools/City Alignment/1. Inspect All City Objects")]
    public static void InspectCity()
    {
        GameObject city = FindCityRoot();
        if (city == null)
        {
            Debug.LogError("[CityAlignment] No se encontró el objeto 'City' en la raíz de la escena activa.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("=================================================================");
        sb.AppendLine($" CITY INSPECTION REPORT - {System.DateTime.Now}");
        sb.AppendLine("=================================================================\n");

        sb.AppendLine($"City Root: '{city.name}' at World Pos: {city.transform.position}, Local Pos: {city.transform.localPosition}\n");

        int directChildCount = city.transform.childCount;
        sb.AppendLine($"--- DIRECT CHILDREN OF CITY (Total: {directChildCount}) ---");

        int misalignedDirectChildren = 0;
        for (int i = 0; i < directChildCount; i++)
        {
            Transform child = city.transform.GetChild(i);
            bool yZero = Mathf.Approximately(child.localPosition.y, 0f);
            if (!yZero) misalignedDirectChildren++;

            sb.AppendLine($"[{i:D2}] '{child.name}': LocalPos={child.localPosition:F4}, WorldPos={child.position:F4}, LocalRot={child.localEulerAngles:F2}, Scale={child.localScale:F3} {(yZero ? "[OK Y=0]" : "[MISALIGNED Y!]")}");
        }

        sb.AppendLine($"\nDirect children with local Y != 0: {misalignedDirectChildren}/{directChildCount}\n");

        // Analizar todos los descendientes recursivos
        sb.AppendLine("--- ALL RECURSIVE DESCENDANTS UNDER CITY ---");
        var allTransforms = city.GetComponentsInChildren<Transform>(true);
        int totalDescendants = allTransforms.Length - 1; // excluir root

        int nonZeroLocalY = 0;
        int nonZeroWorldY = 0;
        int negativeScales = 0;
        int tiltedObjects = 0;
        int totalColliders = 0;

        List<string> negativeScaleList = new List<string>();
        List<string> misalignedWorldYList = new List<string>();
        List<string> colliderBoundIssues = new List<string>();

        foreach (var t in allTransforms)
        {
            if (t == city.transform) continue;

            string path = GetHierarchyPath(t, city.transform);

            if (Mathf.Abs(t.localPosition.y) > 0.0001f)
                nonZeroLocalY++;

            if (Mathf.Abs(t.position.y) > 0.001f)
            {
                nonZeroWorldY++;
                misalignedWorldYList.Add($"'{path}': World Y = {t.position.y:F4} (Local Y = {t.localPosition.y:F4})");
            }

            // Detectar rotaciones en X o Z (inclinaciones de terreno)
            float rx = NormalizeAngle(t.eulerAngles.x);
            float rz = NormalizeAngle(t.eulerAngles.z);
            if (Mathf.Abs(rx) > 0.05f || Mathf.Abs(rz) > 0.05f)
            {
                tiltedObjects++;
            }

            // Detectar escalas negativas (provocan 'BoxCollider does not support negative scale')
            Vector3 lossy = t.lossyScale;
            Vector3 local = t.localScale;
            if (lossy.x < 0 || lossy.y < 0 || lossy.z < 0 || local.x < 0 || local.y < 0 || local.z < 0)
            {
                negativeScales++;
                negativeScaleList.Add($"'{path}': LocalScale={local:F3}, LossyScale={lossy:F3}");
            }

            // Revisar colliders
            Collider col = t.GetComponent<Collider>();
            if (col != null)
            {
                totalColliders++;
                Bounds b = col.bounds;
                // Si la superficie superior del collider está muy por encima o debajo de 0
                if (Mathf.Abs(b.max.y) > 0.1f)
                {
                    colliderBoundIssues.Add($"'{path}' ({col.GetType().Name}): Top Y (bounds.max.y) = {b.max.y:F4}, Center Y = {b.center.y:F4}, Size Y = {b.size.y:F4}");
                }
            }
        }

        sb.AppendLine($"Total descendants: {totalDescendants}");
        sb.AppendLine($"Objects with World Y != 0: {nonZeroWorldY}");
        sb.AppendLine($"Objects with Local Y != 0: {nonZeroLocalY}");
        sb.AppendLine($"Objects with X/Z Tilt: {tiltedObjects}");
        sb.AppendLine($"Objects with Negative Scales: {negativeScales}");
        sb.AppendLine($"Total Colliders: {totalColliders}\n");

        if (negativeScaleList.Count > 0)
        {
            sb.AppendLine("--- OBJECTS WITH NEGATIVE SCALE (CRITICAL: PhysX BoxCollider fails!) ---");
            foreach (var s in negativeScaleList)
                sb.AppendLine("  " + s);
            sb.AppendLine();
        }

        if (misalignedWorldYList.Count > 0)
        {
            sb.AppendLine("--- SAMPLE OBJECTS WITH WORLD Y != 0 ---");
            int showCount = Mathf.Min(50, misalignedWorldYList.Count);
            for (int i = 0; i < showCount; i++)
                sb.AppendLine("  " + misalignedWorldYList[i]);
            if (misalignedWorldYList.Count > showCount)
                sb.AppendLine($"  ... and {misalignedWorldYList.Count - showCount} more.");
            sb.AppendLine();
        }

        if (colliderBoundIssues.Count > 0)
        {
            sb.AppendLine("--- SAMPLE COLLIDERS WITH UNUSUAL TOP SURFACE HEIGHT ---");
            int showCount = Mathf.Min(50, colliderBoundIssues.Count);
            for (int i = 0; i < showCount; i++)
                sb.AppendLine("  " + colliderBoundIssues[i]);
            if (colliderBoundIssues.Count > showCount)
                sb.AppendLine($"  ... and {colliderBoundIssues.Count - showCount} more.");
            sb.AppendLine();
        }

        string fullText = sb.ToString();
        File.WriteAllText(REPORT_PATH, fullText);
        AssetDatabase.Refresh();

        Debug.Log($"[CityAlignment] Inspección completada. Guardado en '{REPORT_PATH}'. Descendientes: {totalDescendants}, World Y!=0: {nonZeroWorldY}, NegScales: {negativeScales}");
    }

    [MenuItem("Tools/City Alignment/2. Align Direct Children Y to 0")]
    public static void AlignDirectChildrenY()
    {
        GameObject city = FindCityRoot();
        if (city == null)
        {
            Debug.LogError("[CityAlignment] No se encontró el objeto 'City' en la raíz de la escena activa.");
            return;
        }

        // Asegurar que el root City esté en Y = 0
        Undo.RecordObject(city.transform, "Align City Root Y");
        Vector3 cityPos = city.transform.position;
        city.transform.position = new Vector3(cityPos.x, 0f, cityPos.z);

        int count = city.transform.childCount;
        int alignedCount = 0;

        for (int i = 0; i < count; i++)
        {
            Transform child = city.transform.GetChild(i);
            if (!Mathf.Approximately(child.localPosition.y, 0f))
            {
                Undo.RecordObject(child, "Align Child Y to 0");
                Vector3 localPos = child.localPosition;
                localPos.y = 0f;
                child.localPosition = localPos;
                alignedCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(city.scene);
        Debug.Log($"[CityAlignment] ¡Alineados {alignedCount} hijos directos de '{city.name}' a Y = 0!");
    }

    [MenuItem("Tools/City Alignment/3. Align ALL Descendants Local Y to 0")]
    public static void AlignAllDescendantsLocalY()
    {
        GameObject city = FindCityRoot();
        if (city == null)
        {
            Debug.LogError("[CityAlignment] No se encontró el objeto 'City' en la raíz de la escena activa.");
            return;
        }

        // Asegurar que City root esté en Y = 0
        Undo.RecordObject(city.transform, "Align City Root Y");
        Vector3 cityPos = city.transform.position;
        city.transform.position = new Vector3(cityPos.x, 0f, cityPos.z);

        var allTransforms = city.GetComponentsInChildren<Transform>(true);
        int alignedCount = 0;

        foreach (var t in allTransforms)
        {
            if (t == city.transform) continue;

            // No alterar objetos si están dentro de casas o decoración que intencionalmente tengan offset vertical
            // Pero para calles y piezas viales, debe ser 0.
            // Para ser seguros, verifiquemos si tiene un collider de calle o si es un contenedor de calle.
            if (Mathf.Abs(t.localPosition.y) > 0.0001f)
            {
                Undo.RecordObject(t, "Align Descendant Local Y to 0");
                Vector3 p = t.localPosition;
                p.y = 0f;
                t.localPosition = p;
                alignedCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(city.scene);
        Debug.Log($"[CityAlignment] ¡Alineados {alignedCount} descendientes a localPosition.y = 0!");
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    private static string GetHierarchyPath(Transform t, Transform stopAt)
    {
        string path = t.name;
        Transform curr = t.parent;
        while (curr != null && curr != stopAt)
        {
            path = curr.name + "/" + path;
            curr = curr.parent;
        }
        return path;
    }

    [MenuItem("Tools/City Alignment/Inspect Road Heights in City")]
    public static void InspectRoadHeights()
    {
        GameObject city = FindCityRoot();
        if (city == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("=== DETAILED ROAD HEIGHTS UNDER CITY ===\n");

        var allTransforms = city.GetComponentsInChildren<Transform>(true);
        var roadCols = new List<BoxCollider>();

        foreach (var t in allTransforms)
        {
            if (t.name.Contains("Houses") || t.name.Contains("Casa") || t.name.Contains("building"))
                continue;

            var box = t.GetComponent<BoxCollider>();
            if (box != null)
                roadCols.Add(box);
        }

        sb.AppendLine($"Total road BoxColliders found: {roadCols.Count}\n");

        var heights = new Dictionary<string, List<string>>();

        foreach (var box in roadCols)
        {
            float topY = box.bounds.max.y;
            string key = topY.ToString("F3");

            if (!heights.ContainsKey(key))
                heights[key] = new List<string>();

            string p = GetHierarchyPath(box.transform, city.transform);
            heights[key].Add($"{p} (center.y={box.center.y:F3}, size.y={box.size.y:F3}, lossyScale.y={box.transform.lossyScale.y:F3})");
        }

        foreach (var kvp in heights)
        {
            sb.AppendLine($"--- TOP SURFACE WORLD Y = {kvp.Key} (Count: {kvp.Value.Count}) ---");
            int show = Mathf.Min(10, kvp.Value.Count);
            for (int i = 0; i < show; i++)
                sb.AppendLine("  " + kvp.Value[i]);
            if (kvp.Value.Count > show)
                sb.AppendLine($"  ... and {kvp.Value.Count - show} more.");
            sb.AppendLine();
        }

        var negativeScaleBoxes = new List<string>();
        foreach (var box in roadCols)
        {
            Vector3 ls = box.transform.lossyScale;
            if (ls.x < 0 || ls.y < 0 || ls.z < 0)
            {
                string p = GetHierarchyPath(box.transform, city.transform);
                negativeScaleBoxes.Add($"{p} -> lossyScale={ls:F3}, localScale={box.transform.localScale:F3}, parentLocalScale={box.transform.parent.localScale:F3}");
            }
        }

        sb.AppendLine($"\nBoxColliders with NEGATIVE lossyScale: {negativeScaleBoxes.Count}/{roadCols.Count}");
        foreach (var item in negativeScaleBoxes)
        {
            sb.AppendLine("  " + item);
        }

        string outPath = "Assets/Road_Heights_Report.txt";
        File.WriteAllText(outPath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"[CityAlignment] Reporte de alturas guardado en '{outPath}'. Alturas distintas: {heights.Count}");
    }

    [MenuItem("Tools/City Alignment/Fix Negative Scale Road Colliders")]
    public static void FixNegativeScaleRoadColliders()
    {
        GameObject city = FindCityRoot();
        if (city == null) return;

        // Primero eliminar cualquier _PositiveRoadCollider previo si existía para empezar limpios
        var oldFixes = city.GetComponentsInChildren<Transform>(true);
        foreach (var t in oldFixes)
        {
            if (t != null && t.name == "_PositiveRoadCollider")
            {
                // Restaurar el BoxCollider en su hermano o padre si fue destruido
                Transform p = t.parent;
                var model = p.Find("Mesh1_Group1_Model");
                if (model == null && p.name == "Mesh1_Group1_Model") model = p;
                if (model != null && model.GetComponent<BoxCollider>() == null)
                {
                    var bc = model.gameObject.AddComponent<BoxCollider>();
                    bc.size = new Vector3(3f, 0.63f, 3f);
                    bc.center = new Vector3(-1.5f, 0.315f, -1.5f);
                }
                Undo.DestroyObjectImmediate(t.gameObject);
            }
        }

        var allTransforms = city.GetComponentsInChildren<Transform>(true);
        int fixedCount = 0;

        foreach (var t in allTransforms)
        {
            if (t.name.Contains("Houses") || t.name.Contains("Casa") || t.name.Contains("building"))
                continue;

            var box = t.GetComponent<BoxCollider>();
            if (box == null) continue;

            Vector3 ls = t.lossyScale;
            if (ls.z < 0)
            {
                Bounds worldBoundsBefore = box.bounds;
                PhysicsMaterial mat = box.sharedMaterial;

                // Crear un GameObject hijo de t ('Mesh1_Group1_Model')
                // Como t tiene scale.z < 0, si su hijo tiene localScale = (1, 1, -1),
                // el lossyScale del hijo será estrictamente POSITIVO en X, Y y Z!
                GameObject colGo = new GameObject("_PositiveRoadCollider");
                Undo.RegisterCreatedObjectUndo(colGo, "Fix Negative Collider");
                colGo.transform.SetParent(t, false);
                colGo.transform.localPosition = Vector3.zero;
                colGo.transform.localRotation = Quaternion.identity;
                colGo.transform.localScale = new Vector3(1f, 1f, -1f);

                BoxCollider newBox = colGo.AddComponent<BoxCollider>();
                newBox.size = box.size;
                // Invertir center.z para compensar el localScale.z = -1
                newBox.center = new Vector3(box.center.x, box.center.y, -box.center.z);
                newBox.sharedMaterial = mat;

                // Destruir el BoxCollider de t
                Undo.DestroyObjectImmediate(box);

                Bounds worldBoundsAfter = newBox.bounds;
                float dist = Vector3.Distance(worldBoundsBefore.center, worldBoundsAfter.center);
                Debug.Log($"[FixNegativeScale] '{t.name}' en '{t.parent.name}': lossyScale={colGo.transform.lossyScale.z:F2} (POSITIVO). DeltaCenter={dist:F6}");
                fixedCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(city.scene);
        Debug.Log($"[CityAlignment] ¡Completado! {fixedCount} colliders con escala negativa convertidos.");
    }

    [MenuItem("Tools/City Alignment/Inspect Street14 CurvaEsquina")]
    public static void InspectCurvaEsquina()
    {
        var target = GameObject.Find("City/Street14/CurvaEsquina");
        if (target == null)
        {
            Debug.LogError("No se encontró City/Street14/CurvaEsquina");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== INSPECTION OF City/Street14/CurvaEsquina ===\n");
        sb.AppendLine($"Root Pos: {target.transform.position}, Rot: {target.transform.eulerAngles}, Scale: {target.transform.lossyScale}\n");

        var colliders = target.GetComponentsInChildren<Collider>(true);
        sb.AppendLine($"Total Colliders: {colliders.Length}\n");

        foreach (var col in colliders)
        {
            string p = GetHierarchyPath(col.transform, target.transform);
            var mf = col.GetComponent<MeshFilter>();
            string meshName = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.name : "none";
            var box = col as BoxCollider;
            if (box != null)
            {
                sb.AppendLine($"[BoxCollider] '{p}' (Mesh: '{meshName}'):");
                sb.AppendLine($"   World Pos={col.transform.position}, World Rot={col.transform.eulerAngles}, LossyScale={col.transform.lossyScale}");
                sb.AppendLine($"   Box size={box.size:F3}, center={box.center:F3}");
                sb.AppendLine($"   World Bounds min={box.bounds.min:F3}, max={box.bounds.max:F3}, center={box.bounds.center:F3}");
            }
            else
            {
                sb.AppendLine($"[{col.GetType().Name}] '{p}' (Mesh: '{meshName}'):");
                sb.AppendLine($"   World Bounds min={col.bounds.min:F3}, max={col.bounds.max:F3}");
            }
            sb.AppendLine();
        }

        var renderers = target.GetComponentsInChildren<MeshRenderer>(true);
        sb.AppendLine($"--- RENDERERS IN CurvaEsquina (Total: {renderers.Length}) ---");
        foreach (var r in renderers)
        {
            string p = GetHierarchyPath(r.transform, target.transform);
            sb.AppendLine($"Renderer '{p}': enabled={r.enabled}, bounds min={r.bounds.min:F2}, max={r.bounds.max:F2}");
        }

        string outPath = "Assets/CurvaEsquina_Inspection.txt";
        File.WriteAllText(outPath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"Guardado en {outPath}");
    }

    [MenuItem("Tools/City Alignment/Fix CurvaEsquina to MeshColliders")]
    public static void FixCurvaEsquinaColliders()
    {
        var slipMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Physics/VehicleSlip.physicMaterial");
        var city = FindCityRoot();
        if (city == null) return;

        var allTransforms = city.GetComponentsInChildren<Transform>(true);
        int convertedCount = 0;

        foreach (var t in allTransforms)
        {
            // Buscar todas las piezas dentro de Curva o CurvaEsquina
            bool isCurva = false;
            Transform curr = t.parent;
            while (curr != null && curr != city.transform)
            {
                if (curr.name.Contains("Curva") || curr.name.Contains("Esquina"))
                {
                    isCurva = true;
                    break;
                }
                curr = curr.parent;
            }
            if (!isCurva) continue;

            // NO convertir PistaCuadrada a MeshCollider porque su mesh obj tiene altura 0.600 en vez de 0.630
            if (t.name.StartsWith("PistaCuadrada") || (t.parent != null && t.parent.name.StartsWith("PistaCuadrada")))
                continue;

            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            // Eliminar cualquier BoxCollider existente o hijo de corrección
            var boxes = t.GetComponents<BoxCollider>();
            foreach (var b in boxes)
            {
                Undo.DestroyObjectImmediate(b);
            }

            var childFix = t.Find("_PositiveRoadCollider");
            if (childFix != null)
            {
                Undo.DestroyObjectImmediate(childFix.gameObject);
            }

            // Asegurar MeshCollider con el mesh exacto
            var mc = t.GetComponent<MeshCollider>();
            if (mc == null) mc = Undo.AddComponent<MeshCollider>(t.gameObject);

            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
            mc.sharedMaterial = slipMat;

            Debug.Log($"[CurvaFix] Convertido '{t.name}' en '{t.parent.name}' a MeshCollider con mesh '{mf.sharedMesh.name}'");
            convertedCount++;
        }

        EditorSceneManager.MarkSceneDirty(city.scene);
        Debug.Log($"[CityAlignment] ¡Convertidos {convertedCount} piezas de curva a MeshCollider!");
    }

    [MenuItem("Tools/City Alignment/Inspect Street15 Curva")]
    public static void InspectStreet15Curva()
    {
        var target = GameObject.Find("City/Street15/CurvaEsquina");
        if (target == null)
        {
            Debug.LogError("No se encontró City/Street15/CurvaEsquina");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== DETAILED INSPECTION OF City/Street15/CurvaEsquina ===\n");
        sb.AppendLine($"Root Pos: {target.transform.position:F4}, Rot: {target.transform.eulerAngles:F4}, Scale: {target.transform.lossyScale:F4}\n");

        var allTransforms = target.GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            sb.AppendLine($"--- Object: '{t.name}' (Parent: '{(t.parent != null ? t.parent.name : "null")}') ---");
            sb.AppendLine($"  LocalPos: {t.localPosition:F4}, WorldPos: {t.position:F4}");
            sb.AppendLine($"  LocalRot: {t.localEulerAngles:F4}, WorldRot: {t.eulerAngles:F4}");
            sb.AppendLine($"  LocalScale: {t.localScale:F4}, LossyScale: {t.lossyScale:F4}");

            var mf = t.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                sb.AppendLine($"  Mesh: '{mf.sharedMesh.name}', bounds local={mf.sharedMesh.bounds.min:F3} to {mf.sharedMesh.bounds.max:F3}");
            }

            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                sb.AppendLine($"  Renderer: enabled={mr.enabled}, worldBounds min={mr.bounds.min:F3}, max={mr.bounds.max:F3}");
            }

            var cols = t.GetComponents<Collider>();
            foreach (var col in cols)
            {
                var box = col as BoxCollider;
                if (box != null)
                {
                    sb.AppendLine($"  [BoxCollider] enabled={box.enabled}, size={box.size:F4}, center={box.center:F4}, mat={(box.sharedMaterial != null ? box.sharedMaterial.name : "null")}");
                    sb.AppendLine($"    WorldBounds min={box.bounds.min:F4}, max={box.bounds.max:F4}, center={box.bounds.center:F4}");
                }
                var mc = col as MeshCollider;
                if (mc != null)
                {
                    sb.AppendLine($"  [MeshCollider] enabled={mc.enabled}, convex={mc.convex}, mesh='{(mc.sharedMesh != null ? mc.sharedMesh.name : "null")}', mat={(mc.sharedMaterial != null ? mc.sharedMaterial.name : "null")}");
                    sb.AppendLine($"    WorldBounds min={mc.bounds.min:F4}, max={mc.bounds.max:F4}, center={mc.bounds.center:F4}");
                }
            }
            sb.AppendLine();
        }

        string outPathCurva = "Assets/Street15_Curva_Inspection.txt";
        File.WriteAllText(outPathCurva, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"Guardado en {outPathCurva}");
    }

    [MenuItem("Tools/City Alignment/Analyze Street15 Corners")]
    public static void AnalyzeStreet15Corners()
    {
        var curva = GameObject.Find("City/Street15/CurvaEsquina");
        if (curva == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("=== STREET 15 TOP FACE CORNERS ANALYSIS ===\n");

        var p1 = curva.transform.Find("PistaCuadrada/Mesh1_Group1_Model");
        var p2 = curva.transform.Find("PistaCuadrada (1)/Mesh1_Group1_Model");

        Vector3[] localCorners = new Vector3[]
        {
            new Vector3( 0f, 0.63f,  0f), // corner A
            new Vector3(-3f, 0.63f,  0f), // corner B
            new Vector3(-3f, 0.63f, -3f), // corner C
            new Vector3( 0f, 0.63f, -3f)  // corner D
        };

        if (p1 != null)
        {
            sb.AppendLine("--- PistaCuadrada Top Corners (World) ---");
            for (int i = 0; i < 4; i++)
            {
                Vector3 w = p1.TransformPoint(localCorners[i]);
                sb.AppendLine($"  Corner {(char)('A' + i)}: ({w.x:F3}, {w.y:F4}, {w.z:F3})");
            }
        }

        if (p2 != null)
        {
            sb.AppendLine("\n--- PistaCuadrada (1) Top Corners (World) ---");
            for (int i = 0; i < 4; i++)
            {
                Vector3 w = p2.TransformPoint(localCorners[i]);
                sb.AppendLine($"  Corner {(char)('A' + i)}: ({w.x:F3}, {w.y:F4}, {w.z:F3})");
            }
        }

        var p15 = GameObject.Find("City/Street15/Pistas15/PistaCuadrada/Mesh1_Group1_Model");
        if (p15 != null)
        {
            sb.AppendLine("\n--- Pistas15 / PistaCuadrada Top Corners (World) ---");
            for (int i = 0; i < 4; i++)
            {
                Vector3 w = p15.transform.TransformPoint(localCorners[i]);
                sb.AppendLine($"  Corner {(char)('A' + i)}: ({w.x:F3}, {w.y:F4}, {w.z:F3})");
            }
        }

        var p16 = GameObject.Find("City/Street16/Pistas16/PistaCuadrada/Mesh1_Group1_Model");
        if (p16 != null)
        {
            sb.AppendLine("\n--- Street16 / Pistas16 / PistaCuadrada Top Corners (World) ---");
            for (int i = 0; i < 4; i++)
            {
                Vector3 w = p16.transform.TransformPoint(localCorners[i]);
                sb.AppendLine($"  Corner {(char)('A' + i)}: ({w.x:F3}, {w.y:F4}, {w.z:F3})");
            }
        }

        var p14 = GameObject.Find("City/Street14/Pistas14/PistaCuadrada/Mesh1_Group1_Model");
        if (p14 != null)
        {
            sb.AppendLine("\n--- Street14 / Pistas14 / PistaCuadrada Top Corners (World) ---");
            for (int i = 0; i < 4; i++)
            {
                Vector3 w = p14.transform.TransformPoint(localCorners[i]);
                sb.AppendLine($"  Corner {(char)('A' + i)}: ({w.x:F3}, {w.y:F4}, {w.z:F3})");
            }
        }

        string outPath = "Assets/Street15_Corners_Report.txt";
        File.WriteAllText(outPath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"Guardado en {outPath}");
    }

    [MenuItem("Tools/City Alignment/Create Unified Collider For Street15 Curva")]
    public static void CreateUnifiedColliderForStreet15()
    {
        var curva = GameObject.Find("City/Street15/CurvaEsquina");
        if (curva == null)
        {
            Debug.LogError("No se encontró City/Street15/CurvaEsquina");
            return;
        }

        var slipMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Physics/VehicleSlip.physicMaterial");

        // 1. Desactivar / remover colliders viejos en los hijos de CurvaEsquina
        var oldCols = curva.GetComponentsInChildren<Collider>(true);
        foreach (var col in oldCols)
        {
            if (col.gameObject.name == "_UnifiedRoadCollider") continue;
            Undo.DestroyObjectImmediate(col);
            Debug.Log($"[UnifiedCurva] Removido collider viejo '{col.name}' en '{col.transform.parent.name}'");
        }

        // 2. Crear o encontrar GameObject _UnifiedRoadCollider
        Transform existing = curva.transform.Find("_UnifiedRoadCollider");
        GameObject unifiedGO;
        if (existing != null)
        {
            unifiedGO = existing.gameObject;
        }
        else
        {
            unifiedGO = new GameObject("_UnifiedRoadCollider");
            Undo.RegisterCreatedObjectUndo(unifiedGO, "Create Unified Road Collider");
            unifiedGO.transform.SetParent(curva.transform, false);
        }

        unifiedGO.transform.localPosition = Vector3.zero;
        unifiedGO.transform.localRotation = Quaternion.identity;
        unifiedGO.transform.localScale = Vector3.one;

        // 3. Puntos clave de las 3 secciones transversales en espacio mundo
        // Seccion 0: Entrada hacia Pistas15 / Pistas16
        Vector3 w0_left  = new Vector3(-101.646f, 0.63f, 87.966f);
        Vector3 w0_right = new Vector3(-97.644f,  0.63f, 86.187f);

        // Seccion 1: Union intermedia de la curva
        Vector3 w1_left  = new Vector3(-100.522f, 0.63f, 91.293f);
        Vector3 w1_right = new Vector3(-96.700f,  0.63f, 89.186f);

        // Seccion 2: Salida hacia Pistas14
        Vector3 w2_left  = new Vector3(-98.854f,  0.63f, 94.320f);
        Vector3 w2_right = new Vector3(-95.213f,  0.63f, 91.886f);

        // Extension de seguridad (0.4m) para solape perfecto con las calles vecinas
        Vector3 dir0_l = (w0_left - w1_left).normalized;
        Vector3 dir0_r = (w0_right - w1_right).normalized;
        Vector3 dir2_l = (w2_left - w1_left).normalized;
        Vector3 dir2_r = (w2_right - w1_right).normalized;

        float extDist = 0.4f;
        w0_left  += dir0_l * extDist;
        w0_right += dir0_r * extDist;
        w2_left  += dir2_l * extDist;
        w2_right += dir2_r * extDist;

        // Convertir a espacio local de unifiedGO
        Vector3 t0 = unifiedGO.transform.InverseTransformPoint(w0_left);
        Vector3 t1 = unifiedGO.transform.InverseTransformPoint(w0_right);
        Vector3 t2 = unifiedGO.transform.InverseTransformPoint(w1_left);
        Vector3 t3 = unifiedGO.transform.InverseTransformPoint(w1_right);
        Vector3 t4 = unifiedGO.transform.InverseTransformPoint(w2_left);
        Vector3 t5 = unifiedGO.transform.InverseTransformPoint(w2_right);

        // Puntos de la base inferior (Y = 0)
        Vector3 b0 = new Vector3(t0.x, 0f, t0.z);
        Vector3 b1 = new Vector3(t1.x, 0f, t1.z);
        Vector3 b2 = new Vector3(t2.x, 0f, t2.z);
        Vector3 b3 = new Vector3(t3.x, 0f, t3.z);
        Vector3 b4 = new Vector3(t4.x, 0f, t4.z);
        Vector3 b5 = new Vector3(t5.x, 0f, t5.z);

        // Construir prisma 3D continuo sin juntas internas
        Mesh mesh = new Mesh();
        mesh.name = "Street15_Curva_UnifiedRoadMesh";

        Vector3[] vertices = new Vector3[]
        {
            // Top (0..5)
            t0, t1, t2, t3, t4, t5,
            // Bottom (6..11)
            b0, b1, b2, b3, b4, b5
        };

        List<int> tris = new List<int>();

        // Top faces (normal UP hacia arriba)
        AddQuad(tris, 0, 2, 3, 1);
        AddQuad(tris, 2, 4, 5, 3);

        // Bottom faces (normal DOWN hacia abajo)
        AddQuad(tris, 6, 7, 9, 8);
        AddQuad(tris, 8, 9, 11, 10);

        // Side Entry (0-1)
        AddQuad(tris, 0, 1, 7, 6);
        // Side Exit (4-5)
        AddQuad(tris, 5, 4, 10, 11);
        // Side Left (0-2-4)
        AddQuad(tris, 4, 2, 8, 10);
        AddQuad(tris, 2, 0, 6, 8);
        // Side Right (1-3-5)
        AddQuad(tris, 1, 3, 9, 7);
        AddQuad(tris, 3, 5, 11, 9);

        mesh.vertices = vertices;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Guardar mesh como asset persistente
        string meshDir = "Assets/Models";
        if (!Directory.Exists(meshDir)) Directory.CreateDirectory(meshDir);
        string meshPath = $"{meshDir}/Street15_Curva_UnifiedRoadMesh.asset";
        AssetDatabase.CreateAsset(mesh, meshPath);
        AssetDatabase.SaveAssets();

        // Configurar MeshCollider con PhysX internal edge smoothing
        MeshCollider mc = unifiedGO.GetComponent<MeshCollider>();
        if (mc == null) mc = Undo.AddComponent<MeshCollider>(unifiedGO);
        mc.sharedMesh = mesh;
        mc.convex = false;
        mc.sharedMaterial = slipMat;

        EditorSceneManager.MarkSceneDirty(curva.scene);
        Debug.Log($"[UnifiedCurva] ¡Collider unificado creado con éxito en {meshPath}! Top Bounds Y={mc.bounds.max.y:F4}");
    }

    private static void AddQuad(List<int> tris, int a, int b, int c, int d)
    {
        tris.Add(a); tris.Add(b); tris.Add(c);
        tris.Add(a); tris.Add(c); tris.Add(d);
    }

    [MenuItem("Tools/City Alignment/Inspect All Curvas")]
    public static void InspectAllCurvas()
    {
        var city = FindCityRoot();
        if (city == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("=== ALL OBJECTS IN CURVA / ESQUINA CONTAINERS ===\n");

        var allTransforms = city.GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            if (t.name.Contains("Curva") || t.name.Contains("Esquina"))
            {
                sb.AppendLine($"\n>>> CONTAINER: '{GetHierarchyPath(t, city.transform)}' (pos={t.position:F2}, rot={t.eulerAngles:F2})");
                var cols = t.GetComponentsInChildren<Collider>(true);
                foreach (var c in cols)
                {
                    sb.AppendLine($"   - '{c.name}' ({c.GetType().Name}): parent='{c.transform.parent.name}', topY={c.bounds.max.y:F4}, center={c.bounds.center:F2}, size={c.bounds.size:F2}, mat={(c.sharedMaterial != null ? c.sharedMaterial.name : "null")}");
                }
            }
        }

        string outPath = "Assets/All_Curvas_Inspection.txt";
        File.WriteAllText(outPath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"Guardado en {outPath}");
    }

    [MenuItem("Tools/City Alignment/Inspect Street15 Layout")]
    public static void InspectStreet15Layout()
    {
        var st = GameObject.Find("City/Street15");
        if (st == null) return;
        var city = FindCityRoot();
        if (city == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("=== STREET 15 FULL LAYOUT ===\n");

        var colliders = st.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            string p = GetHierarchyPath(col.transform, st.transform);
            sb.AppendLine($"'{p}' ({col.GetType().Name}):");
            sb.AppendLine($"  WorldPos: {col.transform.position:F2}, WorldRot: {col.transform.eulerAngles:F2}, LossyScale: {col.transform.lossyScale:F2}");
            sb.AppendLine($"  Bounds: min={col.bounds.min:F3}, max={col.bounds.max:F3}, center={col.bounds.center:F3}");
            sb.AppendLine($"  Top Y: {col.bounds.max.y:F4}");
            sb.AppendLine();
        }

        sb.AppendLine("=== NEIGHBOR COLLIDERS NEAR Street15 Curva (-98, 94) ===");
        var allCols = city.GetComponentsInChildren<Collider>(true);
        Vector3 targetPt = new Vector3(-98f, 0.3f, 94f);
        foreach (var c in allCols)
        {
            if (c.transform.IsChildOf(st.transform)) continue;
            float d = Vector3.Distance(c.bounds.center, targetPt);
            if (d < 30f)
            {
                string p = GetHierarchyPath(c.transform, city.transform);
                sb.AppendLine($"Neighbor: '{p}' ({c.GetType().Name}): dist={d:F1}, bounds min={c.bounds.min:F2}, max={c.bounds.max:F2}, Top Y={c.bounds.max.y:F4}");
            }
        }

        string outPath = "Assets/Street15_Layout.txt";
        File.WriteAllText(outPath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"Guardado en {outPath}");
    }

    [MenuItem("Tools/City Alignment/Find All Colliders Below 0.63")]
    public static void FindAllLowColliders()
    {
        var city = FindCityRoot();
        if (city == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("=== ALL ROAD COLLIDERS WITH TOP Y != 0.630 ===\n");

        var allCols = city.GetComponentsInChildren<Collider>(true);
        int lowCount = 0;

        foreach (var col in allCols)
        {
            if (col.name.Contains("Houses") || col.name.Contains("Casa") || col.name.Contains("building") || col.name.Contains("Door") || col.name.Contains("Car"))
                continue;
            if (col.transform.IsChildOf(city.transform) == false) continue;

            float topY = col.bounds.max.y;
            if (Mathf.Abs(topY - 0.63f) > 0.005f)
            {
                string p = GetHierarchyPath(col.transform, city.transform);
                sb.AppendLine($"'{p}' ({col.GetType().Name}): Top Y = {topY:F4}, Center Y = {col.bounds.center.y:F4}, Size Y = {col.bounds.size.y:F4}");
                lowCount++;
            }
        }

        sb.AppendLine($"\nTotal non-0.630 road colliders: {lowCount}");
        string outPath = "Assets/Low_Colliders_Report.txt";
        File.WriteAllText(outPath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"[CityAlignment] Reporte guardado en {outPath}. No-0.630 encontrados: {lowCount}");
    }

    [MenuItem("Tools/City Alignment/Fix All PistaCuadrada Colliders")]
    public static void FixPistaCuadradaColliders()
    {
        var city = FindCityRoot();
        if (city == null)
        {
            Debug.LogError("No se encontró City");
            return;
        }

        var slipMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Physics/VehicleSlip.physicMaterial");
        if (slipMat == null)
        {
            Debug.LogError("No se encontró VehicleSlip.physicMaterial");
            return;
        }

        var allTransforms = city.GetComponentsInChildren<Transform>(true);
        int fixedCount = 0;

        foreach (var t in allTransforms)
        {
            if (t == null) continue;
            if (t == city.transform) continue;

            // Identificar si este transform o su padre inmediato es PistaCuadrada
            bool isPistaCuadrada = t.name.StartsWith("PistaCuadrada") ||
                                   (t.parent != null && t.parent.name.StartsWith("PistaCuadrada"));
            if (!isPistaCuadrada) continue;

            // Trabajamos sobre el GameObject que tiene el MeshFilter (Mesh1_Group1_Model o PistaCuadrada)
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null) continue;

            // Eliminar MeshCollider si lo tuviera (su altura es 0.600 en vez de 0.630)
            var mc = t.GetComponent<MeshCollider>();
            if (mc != null)
            {
                Undo.DestroyObjectImmediate(mc);
                Debug.Log($"[FixPistaCuadrada] Eliminado MeshCollider de '{t.name}' en '{t.parent.name}'");
            }

            // Eliminar _PositiveRoadCollider viejo si existiera
            var childCol = t.Find("_PositiveRoadCollider");
            if (childCol != null)
            {
                Undo.DestroyObjectImmediate(childCol.gameObject);
            }

            // Comprobar la escala para saber si requiere _PositiveRoadCollider
            Vector3 lossy = t.lossyScale;
            bool hasNegativeScale = lossy.x < 0f || lossy.y < 0f || lossy.z < 0f;

            if (hasNegativeScale)
            {
                var existingBoxes = t.GetComponents<BoxCollider>();
                foreach (var b in existingBoxes) Undo.DestroyObjectImmediate(b);

                GameObject colGo = new GameObject("_PositiveRoadCollider");
                Undo.RegisterCreatedObjectUndo(colGo, "Create Positive BoxCollider");
                colGo.transform.SetParent(t, false);
                colGo.transform.localPosition = Vector3.zero;
                colGo.transform.localRotation = Quaternion.identity;

                float scaleX = lossy.x < 0f ? -1f : 1f;
                float scaleY = lossy.y < 0f ? -1f : 1f;
                float scaleZ = lossy.z < 0f ? -1f : 1f;
                colGo.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

                BoxCollider newBox = Undo.AddComponent<BoxCollider>(colGo);
                newBox.size = new Vector3(3f, 0.63f, 3f);
                newBox.center = new Vector3(
                    -1.5f * scaleX,
                    0.315f * scaleY,
                    -1.5f * scaleZ
                );
                newBox.sharedMaterial = slipMat;
                fixedCount++;
                Debug.Log($"[FixPistaCuadrada] Creado BoxCollider en _PositiveRoadCollider para '{t.name}' en '{t.parent.name}' (topY={newBox.bounds.max.y:F4})");
            }
            else
            {
                var box = t.GetComponent<BoxCollider>();
                if (box == null) box = Undo.AddComponent<BoxCollider>(t.gameObject);

                box.size = new Vector3(3f, 0.63f, 3f);
                box.center = new Vector3(-1.5f, 0.315f, -1.5f);
                box.sharedMaterial = slipMat;
                fixedCount++;
                Debug.Log($"[FixPistaCuadrada] BoxCollider asignado a '{t.name}' en '{t.parent.name}' (topY={box.bounds.max.y:F4})");
            }
        }

        EditorSceneManager.MarkSceneDirty(city.scene);
        Debug.Log($"[FixPistaCuadrada] ¡Completado! {fixedCount} colliders de PistaCuadrada configurados a BoxCollider 0.63 con VehicleSlip.");
    }

    [MenuItem("Tools/City Alignment/Inspect Street15 Detailed")]
    public static void InspectStreet15Detailed()
    {
        var city = FindCityRoot();
        if (city == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("=== STREET 15 DETAILED INSPECTION REPORT ===\n");

        string[] targets = new string[]
        {
            "City/Street15/Pistas15",
            "City/Street15/CurvaEsquina",
            "City/Street16/Pistas16"
        };

        foreach (var path in targets)
        {
            var go = GameObject.Find(path);
            if (go == null)
            {
                sb.AppendLine($"[NOT FOUND] '{path}'\n");
                continue;
            }

            sb.AppendLine($"\n#####################################################");
            sb.AppendLine($"# TARGET: '{path}'");
            sb.AppendLine($"# Root Pos: {go.transform.position:F4}, Rot: {go.transform.eulerAngles:F4}, Scale: {go.transform.lossyScale:F4}");
            sb.AppendLine($"#####################################################\n");

            var cols = go.GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                string p = GetHierarchyPath(col.transform, city.transform);
                sb.AppendLine($"Collider: '{p}' ({col.GetType().Name}):");
                sb.AppendLine($"  Enabled: {col.enabled}");
                sb.AppendLine($"  Material: {(col.sharedMaterial != null ? col.sharedMaterial.name : "NONE")}");
                sb.AppendLine($"  Bounds: min={col.bounds.min:F4}, max={col.bounds.max:F4}, center={col.bounds.center:F4}");
                sb.AppendLine($"  Top Surface Y: {col.bounds.max.y:F4} (Delta to 0.63: {col.bounds.max.y - 0.63f:+0.0000;-0.0000;0.0000})");

                var box = col as BoxCollider;
                if (box != null)
                {
                    sb.AppendLine($"  Box Size: {box.size:F4}, Center: {box.center:F4}");
                    sb.AppendLine($"  Transform Pos: {box.transform.position:F4}, Rot: {box.transform.eulerAngles:F4}, LossyScale: {box.transform.lossyScale:F4}");
                }
                sb.AppendLine();
            }
        }

        // 1. Escanear TODOS los colliders de la escena a menos de 25m del centro de CurvaEsquina
        Vector3 centerPt = new Vector3(-98.5f, 0.3f, 90.5f);
        sb.AppendLine("=== ALL SCENE COLLIDERS WITHIN 25m OF STREET15 CURVA ===");
        var allSceneCols = UnityEngine.Object.FindObjectsByType<Collider>();
        foreach (var c in allSceneCols)
        {
            if (!c.enabled) continue;
            float d = Vector3.Distance(c.bounds.center, centerPt);
            if (d < 25f)
            {
                sb.AppendLine($"[Dist {d:F1}m] '{c.gameObject.name}' (Parent: '{(c.transform.parent != null ? c.transform.parent.name : "null")}', Type: {c.GetType().Name}):");
                sb.AppendLine($"   Bounds min={c.bounds.min:F3}, max={c.bounds.max:F3}, center={c.bounds.center:F3}, Top Y={c.bounds.max.y:F4}");
                sb.AppendLine($"   IsTrigger={c.isTrigger}, Layer={LayerMask.LayerToName(c.gameObject.layer)}, Mat={(c.sharedMaterial != null ? c.sharedMaterial.name : "null")}");
            }
        }
        sb.AppendLine();

        // 2. Sondear virtualmente la trayectoria de la carretera (desde Street15 hacia Street16 pasando por CurvaEsquina)
        sb.AppendLine("=== VIRTUAL PROBE: TRAJECTORY FROM STREET15 -> CURVA -> STREET16 ===");
        // Puntos clave de la trayectoria:
        // Pistas15 termina aprox en (-102.5, 0.63, 86.0)
        // CurvaEsquina va de (-100.2, 0.63, 91.3) a (-98.8, 0.63, 94.3)
        // Pistas16 empieza aprox en (-98.0, 0.63, 95.0) hacia (-85.0, 0.63, 120.0)
        Vector3 pStart = new Vector3(-106.0f, 2f, 80.0f);
        Vector3 pMid1  = new Vector3(-100.5f, 2f, 90.0f);
        Vector3 pMid2  = new Vector3(-98.5f,  2f, 93.5f);
        Vector3 pEnd   = new Vector3(-94.0f,  2f, 102.0f);

        List<Vector3> waypoints = new List<Vector3> { pStart, pMid1, pMid2, pEnd };
        float prevHitY = 0.63f;
        int sampleIndex = 0;

        for (int w = 0; w < waypoints.Count - 1; w++)
        {
            Vector3 a = waypoints[w];
            Vector3 b = waypoints[w + 1];
            float segLen = Vector3.Distance(a, b);
            int steps = Mathf.Max(5, Mathf.RoundToInt(segLen / 0.2f)); // cada 20 cm

            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                Vector3 rayOrigin = Vector3.Lerp(a, b, t);
                RaycastHit hit;
                if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                {
                    float stepDelta = hit.point.y - prevHitY;
                    string warning = Mathf.Abs(stepDelta) > 0.005f ? $" <<< [STEP LIP DETECTED! Delta: {stepDelta*1000f:F1} mm] >>>" : "";
                    sb.AppendLine($"Step {sampleIndex:D3} at ({hit.point.x:F2}, {hit.point.z:F2}): Hit Y={hit.point.y:F4}, Normal={hit.normal:F2}, Col='{hit.collider.name}' (parent='{(hit.collider.transform.parent != null ? hit.collider.transform.parent.name : "null")}') {warning}");
                    prevHitY = hit.point.y;
                }
                else
                {
                    sb.AppendLine($"Step {sampleIndex:D3} at ({rayOrigin.x:F2}, {rayOrigin.z:F2}): NO HIT (HOLE/GAP IN ROAD!) <<< [CRITICAL GAP] >>>");
                }
                sampleIndex++;
            }
        }

        string outPath = "Assets/Street15_Detailed_Report.txt";
        File.WriteAllText(outPath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"Guardado en {outPath}");
    }

    [MenuItem("Tools/City Alignment/Setup Delivery Points on House Prefabs")]
    public static void SetupDeliveryPointsOnHouses()
    {
        string housesDir = "Assets/Prefabs/Houses/Nivel1";
        string[] housePrefabs = new string[]
        {
            "CasaTipo-A.prefab",
            "CasaTipo-F.prefab",
            "CasaTipo-I.prefab",
            "CasaTipo-K.prefab",
            "CasaTipo-M.prefab",
            "CasaTipo-R.prefab",
            "CasaTipo-T.prefab"
        };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=================================================================");
        sb.AppendLine($" DELIVERY POINTS SETUP REPORT - {System.DateTime.Now}");
        sb.AppendLine("=================================================================\n");

        int successCount = 0;

        foreach (string prefabName in housePrefabs)
        {
            string prefabPath = System.IO.Path.Combine(housesDir, prefabName);
            if (!System.IO.File.Exists(prefabPath))
            {
                sb.AppendLine($"[ERROR] No existe el archivo: {prefabPath}");
                continue;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                sb.AppendLine($"[ERROR] No se pudo cargar: {prefabPath}");
                continue;
            }

            try
            {
                // 1. Encontrar o crear DeliveryPoint
                Transform existing = prefabRoot.transform.Find("DeliveryPoint");
                GameObject deliveryGO;
                if (existing != null)
                {
                    deliveryGO = existing.gameObject;
                }
                else
                {
                    deliveryGO = new GameObject("DeliveryPoint");
                    deliveryGO.transform.SetParent(prefabRoot.transform, false);
                }

                // 2. Localizar modelo de casa (building-type-*)
                Transform buildingModel = null;
                foreach (Transform child in prefabRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (child != prefabRoot.transform && child.name.StartsWith("building-type-", System.StringComparison.OrdinalIgnoreCase))
                    {
                        buildingModel = child;
                        break;
                    }
                }

                // 3. Posición de la puerta
                Vector3 doorPosition = Vector3.zero;
                if (buildingModel != null)
                {
                    BoxCollider box = buildingModel.GetComponent<BoxCollider>();
                    float forwardDistance = 4.2f;

                    if (box != null)
                    {
                        float halfDepth = (box.size.z * 0.5f) * Mathf.Abs(buildingModel.localScale.z);
                        forwardDistance = halfDepth + 1.2f;
                    }

                    Vector3 worldFront = buildingModel.position + buildingModel.forward * forwardDistance;
                    doorPosition = prefabRoot.transform.InverseTransformPoint(worldFront);
                    doorPosition.y = 0.05f;
                }
                else
                {
                    doorPosition = new Vector3(0f, 0.05f, -3.5f);
                }

                deliveryGO.transform.localPosition = doorPosition;
                deliveryGO.transform.localRotation = buildingModel != null ? buildingModel.localRotation : Quaternion.identity;

                // 4. Trigger BoxCollider primero (para satisfacer RequireComponent)
                BoxCollider trigger = deliveryGO.GetComponent<BoxCollider>();
                if (trigger == null) trigger = deliveryGO.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = new Vector3(0f, 1.0f, 0f);
                trigger.size = new Vector3(2.5f, 2.0f, 2.5f);

                // 5. Componente DeliveryPoint
                DeliveryPoint dp = deliveryGO.GetComponent<DeliveryPoint>();
                if (dp == null) dp = deliveryGO.AddComponent<DeliveryPoint>();
                dp.houseName = prefabRoot.name;

                // 6. DropSpot
                Transform dropSpot = deliveryGO.transform.Find("DropSpot");
                if (dropSpot == null)
                {
                    GameObject dsGO = new GameObject("DropSpot");
                    dsGO.transform.SetParent(deliveryGO.transform, false);
                    dsGO.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                    dropSpot = dsGO.transform;
                }
                dp.dropSpot = dropSpot;

                // 7. GroundMarker
                Transform groundMarker = deliveryGO.transform.Find("GroundMarker");
                if (groundMarker == null)
                {
                    GameObject gmGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    gmGO.name = "GroundMarker";
                    gmGO.transform.SetParent(deliveryGO.transform, false);
                    gmGO.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                    gmGO.transform.localScale = new Vector3(1.6f, 0.02f, 1.6f);

                    Collider col = gmGO.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);

                    groundMarker = gmGO.transform;
                }
                dp.groundMarker = groundMarker.gameObject;
                dp.groundMarker.SetActive(false);

                // 8. BeaconVisual
                Transform beacon = deliveryGO.transform.Find("BeaconVisual");
                if (beacon == null)
                {
                    GameObject bGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    bGO.name = "BeaconVisual";
                    bGO.transform.SetParent(deliveryGO.transform, false);
                    bGO.transform.localPosition = new Vector3(0f, 4.5f, 0f);
                    bGO.transform.localScale = new Vector3(0.35f, 4.5f, 0.35f);

                    Collider bCol = bGO.GetComponent<Collider>();
                    if (bCol != null) Object.DestroyImmediate(bCol);

                    beacon = bGO.transform;
                }
                dp.beaconVisual = beacon.gameObject;
                dp.beaconVisual.SetActive(false);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                successCount++;
                sb.AppendLine($"[OK] {prefabName} -> DeliveryPoint en localPos: {doorPosition:F3}, Trigger: {trigger.size}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        sb.AppendLine($"\nTotal prefabs configurados: {successCount}/{housePrefabs.Length}");
        string reportPath = "Assets/Delivery_Points_Report.txt";
        System.IO.File.WriteAllText(reportPath, sb.ToString());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[DeliverySetup] Guardado reporte en {reportPath}");
    }

    [MenuItem("Tools/City Alignment/Setup Complete Delivery System (Materials + Houses + HUD + Manager)")]
    public static void SetupDeliverySystemComplete()
    {
        // 1. Materiales GTA (Corona en suelo, Aro y Haz del cielo transparente)
        Material coronaMat = GTADeliveryVisualsSetup.GetOrCreateCoronaMaterial();
        Material ringMat = GTADeliveryVisualsSetup.GetOrCreateRingMaterial();
        Material skyBeaconMat = GTADeliveryVisualsSetup.GetOrCreateSkyBeaconMaterial();

        // 2. Configurar los 7 prefabs de casas con Marcador GTA (Suelo) + Baliza Celeste (78m translúcida)
        string housesDir = "Assets/Prefabs/Houses/Nivel1";
        string[] housePrefabs = new string[]
        {
            "CasaTipo-A.prefab", "CasaTipo-F.prefab", "CasaTipo-I.prefab",
            "CasaTipo-K.prefab", "CasaTipo-M.prefab", "CasaTipo-R.prefab", "CasaTipo-T.prefab"
        };

        foreach (string pName in housePrefabs)
        {
            string pPath = Path.Combine(housesDir, pName);
            if (!File.Exists(pPath)) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(pPath);
            if (root == null) continue;

            try
            {
                Transform dpT = root.transform.Find("DeliveryPoint");
                if (dpT == null)
                {
                    GameObject go = new GameObject("DeliveryPoint");
                    go.transform.SetParent(root.transform, false);
                    dpT = go.transform;
                }

                // Asegurar BoxCollider trigger primero (para RequireComponent)
                BoxCollider box = dpT.GetComponent<BoxCollider>();
                if (box == null) box = dpT.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0, 1.2f, 0);
                box.size = new Vector3(3f, 2.5f, 3f);

                DeliveryPoint dp = dpT.GetComponent<DeliveryPoint>();
                if (dp == null) dp = dpT.gameObject.AddComponent<DeliveryPoint>();
                dp.houseName = root.name;
                dp.activeColor = new Color(1f, 0.78f, 0.18f, 1f);

                // DropSpot
                Transform ds = dpT.Find("DropSpot");
                if (ds == null)
                {
                    GameObject dsGO = new GameObject("DropSpot");
                    dsGO.transform.SetParent(dpT, false);
                    dsGO.transform.localPosition = new Vector3(0, 0.05f, 0);
                    ds = dsGO.transform;
                }
                dp.dropSpot = ds;

                // Configurar visuales GTA (Corona en suelo + Haz transparente en el cielo)
                GTADeliveryVisualsSetup.ConfigureDeliveryPointVisuals(dpT, dp, coronaMat, ringMat, skyBeaconMat);

                PrefabUtility.SaveAsPrefabAsset(root, pPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Actualizar también las instancias de DeliveryPoint en la escena actual
        GTADeliveryVisualsSetup.SetupSceneDeliveryPoints(coronaMat, ringMat, skyBeaconMat);

        // 3. Crear el Banner de Navegación directamente en HUDCanvas en la escena
        GameObject hudCanvasGO = GameObject.Find("HUDCanvas");
        if (hudCanvasGO != null)
        {
            DeliveryNavigationHUD navHUD = hudCanvasGO.GetComponent<DeliveryNavigationHUD>();
            if (navHUD == null) navHUD = hudCanvasGO.AddComponent<DeliveryNavigationHUD>();

            // Buscar si ya existe DeliveryMissionBanner
            Transform existingBanner = hudCanvasGO.transform.Find("DeliveryMissionBanner");
            GameObject bannerGO;
            if (existingBanner != null)
            {
                bannerGO = existingBanner.gameObject;
            }
            else
            {
                bannerGO = new GameObject("DeliveryMissionBanner", typeof(RectTransform));
                bannerGO.transform.SetParent(hudCanvasGO.transform, false);
                bannerGO.layer = 5; // Layer UI
            }

            RectTransform bannerRect = bannerGO.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -45f);
            bannerRect.sizeDelta = new Vector2(850f, 65f);

            Image bannerBg = bannerGO.GetComponent<Image>();
            if (bannerBg == null) bannerBg = bannerGO.AddComponent<Image>();
            bannerBg.color = new Color(0.06f, 0.08f, 0.12f, 0.88f);

            // Texto TextMeshPro
            Transform existingText = bannerGO.transform.Find("MissionText");
            GameObject textGO;
            if (existingText != null)
            {
                textGO = existingText.gameObject;
            }
            else
            {
                textGO = new GameObject("MissionText", typeof(RectTransform));
                textGO.transform.SetParent(bannerGO.transform, false);
                textGO.layer = 5;
            }

            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-30f, -10f);

            TextMeshProUGUI tmpText = textGO.GetComponent<TextMeshProUGUI>();
            if (tmpText == null) tmpText = textGO.AddComponent<TextMeshProUGUI>();

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font != null) tmpText.font = font;
            tmpText.fontSize = 38;
            tmpText.fontStyle = FontStyles.Bold;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = new Color(1f, 0.9f, 0.25f, 1f);
            tmpText.text = "ENTREGA: Esperando pedido...";

            navHUD.missionText = tmpText;
            navHUD.missionContainer = bannerGO;
            bannerGO.SetActive(true);

            EditorUtility.SetDirty(hudCanvasGO);
            EditorUtility.SetDirty(bannerGO);
        }

        // 4. Configurar FoodDeliveryManager en la escena
        FoodDeliveryManager manager = Object.FindAnyObjectByType<FoodDeliveryManager>();
        if (manager == null)
        {
            GameObject go = new GameObject("FoodDeliveryManager");
            manager = go.AddComponent<FoodDeliveryManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create FoodDeliveryManager");
        }

        GameObject olla = GameObject.Find("OllaConComida");
        if (olla != null) manager.targetFoodItem = olla.GetComponent<PickableItem>();

        GameObject city = GameObject.Find("City");
        if (city != null) manager.cityRoot = city.transform;

        manager.autoStartOnPlay = true;
        manager.RegisterAllDeliveryPoints();
        EditorUtility.SetDirty(manager.gameObject);

        // 5. Guardar todo
        var activeScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[DeliverySetup] 🚀 ¡Sistema de entrega completamente configurado! Materiales, Balizas 70m, HUD TextMeshPro y Manager listos.");
    }
}






