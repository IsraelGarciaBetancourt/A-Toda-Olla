using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GTADeliveryVisualsSetup
{
    private static readonly string[] HOUSE_PREFABS = new string[]
    {
        "CasaTipo-A.prefab", "CasaTipo-F.prefab", "CasaTipo-I.prefab",
        "CasaTipo-K.prefab", "CasaTipo-M.prefab", "CasaTipo-R.prefab", "CasaTipo-T.prefab"
    };

    private const string HOUSES_DIR = "Assets/Prefabs/Houses/Nivel1";
    private const string MAT_DIR = "Assets/Materials";

    [MenuItem("Tools/Delivery/Setup GTA Delivery Visuals (Ground Corona + Sky Beacon)")]
    public static void SetupAllVisuals()
    {
        Material coronaMat = GetOrCreateCoronaMaterial();
        Material ringMat = GetOrCreateRingMaterial();
        Material skyBeaconMat = GetOrCreateSkyBeaconMaterial();

        int prefabCount = SetupHousePrefabs(coronaMat, ringMat, skyBeaconMat);
        int sceneCount = SetupSceneDeliveryPoints(coronaMat, ringMat, skyBeaconMat);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[GTADeliveryVisuals] ¡Configuración completada con éxito!\nPrefabs actualizados: {prefabCount}\nInstancias en escena actualizadas: {sceneCount}");
    }

    public static Material GetOrCreateCoronaMaterial()
    {
        string path = $"{MAT_DIR}/Delivery_GTACorona.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Custom/GTA_MissionCorona");
        if (shader == null)
        {
            Debug.LogError("No se encontró el shader Custom/GTA_MissionCorona");
            return null;
        }

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        mat.SetColor("_Color", new Color(1.0f, 0.78f, 0.18f, 0.85f));
        mat.SetFloat("_EmissionPower", 2.6f);
        mat.SetFloat("_VerticalFadePower", 1.5f);
        mat.SetFloat("_RimPower", 1.8f);
        mat.SetFloat("_PulseSpeed", 2.8f);
        mat.SetFloat("_PulseAmount", 0.16f);
        mat.SetFloat("_BaseRingBoost", 2.4f);
        mat.renderQueue = 3050;

        EditorUtility.SetDirty(mat);
        return mat;
    }

    public static Material GetOrCreateRingMaterial()
    {
        string path = $"{MAT_DIR}/Delivery_GTARing.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Custom/GTA_GroundRing");
        if (shader == null)
        {
            Debug.LogError("No se encontró el shader Custom/GTA_GroundRing");
            return null;
        }

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        mat.SetColor("_Color", new Color(1.0f, 0.78f, 0.18f, 0.90f));
        mat.SetFloat("_EmissionPower", 2.5f);
        mat.SetFloat("_PulseSpeed", 2.8f);
        mat.SetFloat("_PulseAmount", 0.14f);
        mat.SetFloat("_RingWidth", 0.08f);
        mat.renderQueue = 3045;

        EditorUtility.SetDirty(mat);
        return mat;
    }

    public static Material GetOrCreateSkyBeaconMaterial()
    {
        string path = $"{MAT_DIR}/Delivery_SkyBeacon.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Custom/GTA_SkyBeacon");
        if (shader == null)
        {
            Debug.LogError("No se encontró el shader Custom/GTA_SkyBeacon");
            return null;
        }

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        // Haz de luz visible a lo largo de todo el mapa (alpha = 0.72, emisión 3.8, CoreBoost 1.5)
        mat.SetColor("_Color", new Color(1.0f, 0.84f, 0.28f, 0.72f));
        mat.SetFloat("_EmissionPower", 3.8f);
        mat.SetFloat("_EdgeSoftness", 1.8f);
        mat.SetFloat("_TopFadeHeight", 0.90f);
        mat.SetFloat("_BottomFadeHeight", 0.03f);
        mat.SetFloat("_CoreBoost", 1.5f);
        mat.renderQueue = 3030;

        EditorUtility.SetDirty(mat);
        return mat;
    }

    public static int SetupHousePrefabs(Material coronaMat, Material ringMat, Material skyBeaconMat)
    {
        int count = 0;
        foreach (string pName in HOUSE_PREFABS)
        {
            string pPath = Path.Combine(HOUSES_DIR, pName);
            if (!File.Exists(pPath)) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(pPath);
            if (root == null) continue;

            try
            {
                Transform dpT = root.transform.Find("DeliveryPoint");
                if (dpT == null) continue;

                DeliveryPoint dp = dpT.GetComponent<DeliveryPoint>();
                if (dp == null) dp = dpT.gameObject.AddComponent<DeliveryPoint>();
                dp.houseName = root.name;
                dp.activeColor = new Color(1f, 0.78f, 0.18f, 1f);

                ConfigureDeliveryPointVisuals(dpT, dp, coronaMat, ringMat, skyBeaconMat);

                PrefabUtility.SaveAsPrefabAsset(root, pPath);
                count++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return count;
    }

    public static int SetupSceneDeliveryPoints(Material coronaMat, Material ringMat, Material skyBeaconMat)
    {
        int count = 0;
        DeliveryPoint[] sceneDPs = Object.FindObjectsByType<DeliveryPoint>(FindObjectsInactive.Include);
        foreach (var dp in sceneDPs)
        {
            if (dp == null) continue;

            dp.activeColor = new Color(1f, 0.78f, 0.18f, 1f);
            ConfigureDeliveryPointVisuals(dp.transform, dp, coronaMat, ringMat, skyBeaconMat);

            EditorUtility.SetDirty(dp.gameObject);
            count++;
        }

        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        return count;
    }

    public static void ConfigureDeliveryPointVisuals(Transform dpT, DeliveryPoint dp, Material coronaMat, Material ringMat, Material skyBeaconMat)
    {
        // -------------------------------------------------------------
        // PARTE 1: GroundMarker (GTA Mission Objective Marker en el suelo)
        // -------------------------------------------------------------
        Transform gm = dpT.Find("GroundMarker");
        if (gm == null)
        {
            GameObject gmGO = new GameObject("GroundMarker");
            gmGO.transform.SetParent(dpT, false);
            gm = gmGO.transform;
        }

        gm.localPosition = Vector3.zero;
        gm.localRotation = Quaternion.identity;
        gm.localScale = Vector3.one;

        // Limpiar MeshRenderer/MeshFilter directos si existían en el contenedor
        MeshRenderer oldGmRend = gm.GetComponent<MeshRenderer>();
        if (oldGmRend != null) Object.DestroyImmediate(oldGmRend);
        MeshFilter oldGmFilter = gm.GetComponent<MeshFilter>();
        if (oldGmFilter != null) Object.DestroyImmediate(oldGmFilter);
        Collider oldGmCol = gm.GetComponent<Collider>();
        if (oldGmCol != null) Object.DestroyImmediate(oldGmCol);

        // Sub-elemento 1: FloorRing (Aro plano en el pavimento)
        Transform floorRing = gm.Find("FloorRing");
        if (floorRing == null)
        {
            GameObject ringGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ringGO.name = "FloorRing";
            ringGO.transform.SetParent(gm, false);
            floorRing = ringGO.transform;
        }
        Collider ringCol = floorRing.GetComponent<Collider>();
        if (ringCol != null) Object.DestroyImmediate(ringCol);

        floorRing.localPosition = new Vector3(0f, 0.03f, 0f);
        floorRing.localRotation = Quaternion.Euler(90f, 0f, 0f);
        floorRing.localScale = new Vector3(2.8f, 2.8f, 1f);

        MeshRenderer ringRend = floorRing.GetComponent<MeshRenderer>();
        if (ringRend != null && ringMat != null)
        {
            ringRend.sharedMaterial = ringMat;
            ringRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringRend.receiveShadows = false;
        }

        // Sub-elemento 2: CoronaWall (Cilindro vertical abierto GTA de 1.8m de altura)
        Transform coronaWall = gm.Find("CoronaWall");
        if (coronaWall == null)
        {
            GameObject coronaGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coronaGO.name = "CoronaWall";
            coronaGO.transform.SetParent(gm, false);
            coronaWall = coronaGO.transform;
        }
        Collider coronaCol = coronaWall.GetComponent<Collider>();
        if (coronaCol != null) Object.DestroyImmediate(coronaCol);

        // Cilindro en y=0.9, escala y=0.9 -> altura total = 1.8m (desde y=0 hasta y=1.8m)
        coronaWall.localPosition = new Vector3(0f, 0.9f, 0f);
        coronaWall.localRotation = Quaternion.identity;
        coronaWall.localScale = new Vector3(2.8f, 0.9f, 2.8f);

        MeshRenderer coronaRend = coronaWall.GetComponent<MeshRenderer>();
        if (coronaRend != null && coronaMat != null)
        {
            coronaRend.sharedMaterial = coronaMat;
            coronaRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            coronaRend.receiveShadows = false;
        }

        // Sub-elemento 3: CoronaLight (Luz puntual cálida que ilumina el suelo y la puerta)
        Transform lightT = gm.Find("CoronaLight");
        Light groundLight = null;
        if (lightT == null)
        {
            GameObject lightGO = new GameObject("CoronaLight");
            lightGO.transform.SetParent(gm, false);
            groundLight = lightGO.AddComponent<Light>();
            lightT = lightGO.transform;
        }
        else
        {
            groundLight = lightT.GetComponent<Light>();
            if (groundLight == null) groundLight = lightT.gameObject.AddComponent<Light>();
        }

        lightT.localPosition = new Vector3(0f, 0.6f, 0f);
        groundLight.type = LightType.Point;
        groundLight.range = 5.5f;
        groundLight.color = new Color(1.0f, 0.80f, 0.22f);
        groundLight.intensity = 2.5f;

        dp.groundMarker = gm.gameObject;
        dp.groundMarker.SetActive(false);

        // -------------------------------------------------------------
        // PARTE 2: BeaconVisual (Haz de luz vertical translúcido hacia el cielo - 80m)
        // -------------------------------------------------------------
        Transform bv = dpT.Find("BeaconVisual");
        if (bv == null)
        {
            GameObject bvGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bvGO.name = "BeaconVisual";
            bvGO.transform.SetParent(dpT, false);
            bv = bvGO.transform;
        }
        Collider bvCol = bv.GetComponent<Collider>();
        if (bvCol != null) Object.DestroyImmediate(bvCol);

        // Eliminar Light antigua de BeaconVisual si existía (ahora está en CoronaLight abajo)
        Light oldLight = bv.GetComponent<Light>();
        if (oldLight != null) Object.DestroyImmediate(oldLight);

        // Altura desde y=2.0m hasta y=82.0m: centro en y=42m, escala y=40m (2 * 40 = 80m de altura)
        // Diámetro aumentado a 2.6m para alinearse con la base y ser claramente visible por todo el mapa
        bv.localPosition = new Vector3(0f, 42f, 0f);
        bv.localRotation = Quaternion.identity;
        bv.localScale = new Vector3(2.6f, 40f, 2.6f);

        MeshRenderer bvRend = bv.GetComponent<MeshRenderer>();
        if (bvRend != null && skyBeaconMat != null)
        {
            bvRend.sharedMaterial = skyBeaconMat;
            bvRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bvRend.receiveShadows = false;
        }

        dp.beaconVisual = bv.gameObject;
        dp.beaconVisual.SetActive(false);
    }
}
