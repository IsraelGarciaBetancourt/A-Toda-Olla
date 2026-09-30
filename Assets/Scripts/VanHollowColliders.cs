using UnityEngine;

/// <summary>
/// Configura colisionadores compuestos (Compound Colliders) para la van,
/// permitiendo que el interior de la bodega sea HUECO y el jugador pueda entrar a pie.
/// 
/// USO:
/// 1. Adjunta este script al GameObject raíz "DeliveryCar".
/// 2. En el Inspector, haz clic derecho sobre el nombre del componente (o en los tres puntitos ⋮)
///    y selecciona "Generar Colisionadores Huecos".
/// 3. ¡Listo! Crea las paredes, suelo, techo y cabina automáticamente y desactiva el BoxCollider macizo viejo.
/// </summary>
public class VanHollowColliders : MonoBehaviour
{
    [Header("Ajustes de Dimensiones (Basados en la van)")]
    public float vanWidth = 1.16f;
    public float vanHeight = 1.15f;
    public float vanLength = 2.98f;
    public Vector3 vanCenter = new Vector3(0.05f, 0.85f, -0.03f);

    [Header("Grosor de Paredes")]
    public float wallThickness = 0.1f;
    public float floorThickness = 0.12f;

    [ContextMenu("Generar Colisionadores Huecos")]
    public void GenerateColliders()
    {
        // 1. Desactivar el BoxCollider macizo viejo que tapa todo
        BoxCollider oldBox = GetComponent<BoxCollider>();
        if (oldBox != null)
        {
            oldBox.enabled = false;
            Debug.Log("[VanHollowColliders] BoxCollider macizo original desactivado.");
        }

        // 2. Crear o reutilizar contenedor hijo
        Transform existingContainer = transform.Find("HollowColliders");
        if (existingContainer != null)
        {
            DestroyImmediate(existingContainer.gameObject);
        }

        GameObject container = new GameObject("HollowColliders");
        container.transform.SetParent(transform);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        container.transform.localScale = Vector3.one;

        float halfWidth = vanWidth * 0.5f;
        float halfHeight = vanHeight * 0.5f;
        float halfLength = vanLength * 0.5f;

        float cargoLength = vanLength * 0.48f; // ~48% trasero para la bodega
        float cabinLength = vanLength - cargoLength; // ~52% delantero para motor/cabina

        float cargoCenterZ = vanCenter.z - halfLength + (cargoLength * 0.5f);
        float cabinCenterZ = vanCenter.z + halfLength - (cabinLength * 0.5f);

        // A. Cabina y Motor delantero (bloque sólido para choques frontales)
        CreateBoxChild(container.transform, "Front_Cabin_Collider",
            new Vector3(vanCenter.x, vanCenter.y, cabinCenterZ),
            new Vector3(vanWidth, vanHeight, cabinLength));

        // B. Suelo de la Bodega (para que el jugador camine encima)
        float floorY = vanCenter.y - halfHeight + (floorThickness * 0.5f);
        CreateBoxChild(container.transform, "Cargo_Floor_Collider",
            new Vector3(vanCenter.x, floorY, cargoCenterZ),
            new Vector3(vanWidth, floorThickness, cargoLength));

        // C. Techo de la Bodega
        float roofY = vanCenter.y + halfHeight - (wallThickness * 0.5f);
        CreateBoxChild(container.transform, "Cargo_Roof_Collider",
            new Vector3(vanCenter.x, roofY, cargoCenterZ),
            new Vector3(vanWidth, wallThickness, cargoLength));

        // D. Pared Izquierda
        float wallY = vanCenter.y;
        float wallHeight = vanHeight - floorThickness - wallThickness;
        float leftX = vanCenter.x - halfWidth + (wallThickness * 0.5f);
        CreateBoxChild(container.transform, "Cargo_Wall_Left",
            new Vector3(leftX, wallY, cargoCenterZ),
            new Vector3(wallThickness, wallHeight, cargoLength));

        // E. Pared Derecha
        float rightX = vanCenter.x + halfWidth - (wallThickness * 0.5f);
        CreateBoxChild(container.transform, "Cargo_Wall_Right",
            new Vector3(rightX, wallY, cargoCenterZ),
            new Vector3(wallThickness, wallHeight, cargoLength));

        // F. Si existe el GameObject de la puerta, asegurarnos de que tenga su collider
        VanDoorController doorCtrl = GetComponent<VanDoorController>();
        if (doorCtrl != null && doorCtrl.doorObject != null)
        {
            BoxCollider doorCol = doorCtrl.doorObject.GetComponent<BoxCollider>();
            if (doorCol == null)
            {
                doorCol = doorCtrl.doorObject.AddComponent<BoxCollider>();
                // Ajustar al marco trasero
                doorCol.size = new Vector3(vanWidth - (wallThickness * 2f), wallHeight, 0.1f);
                doorCol.center = Vector3.zero;
            }
            Debug.Log("[VanHollowColliders] Collider vinculado a la puerta trasera.");
        }

        Debug.Log("[VanHollowColliders] ¡Colisionadores huecos generados con éxito! Ahora el jugador puede entrar por la puerta trasera.");
    }

    [ContextMenu("Restaurar Colisionador Original")]
    public void RestoreOriginal()
    {
        Transform container = transform.Find("HollowColliders");
        if (container != null)
        {
            DestroyImmediate(container.gameObject);
        }

        BoxCollider oldBox = GetComponent<BoxCollider>();
        if (oldBox != null)
        {
            oldBox.enabled = true;
        }

        Debug.Log("[VanHollowColliders] Colisionador macizo original restaurado.");
    }

    private void CreateBoxChild(Transform parent, string name, Vector3 localPos, Vector3 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = size;
        box.center = Vector3.zero;
    }
}
