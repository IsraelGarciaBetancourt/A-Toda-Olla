using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra en pantalla (HUD) la misión de entrega activa, la casa de destino y la distancia en metros.
/// Se vincula automáticamente con FoodDeliveryManager y crea un banner limpio en el Canvas si no existe uno.
/// </summary>
public class DeliveryNavigationHUD : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("Texto TextMeshPro donde se muestra el objetivo y la distancia.")]
    public TMP_Text missionText;

    [Tooltip("Panel o contenedor del cartel de misión para controlar su visibilidad.")]
    public GameObject missionContainer;

    [Header("Iconos / Colores")]
    public Color inTransitColor = new Color(1f, 0.85f, 0.2f, 1f);   // Amarillo dorado
    public Color successColor = new Color(0.3f, 1f, 0.4f, 1f);     // Verde brillante
    public Color waitingColor = new Color(0.9f, 0.9f, 0.9f, 1f);   // Blanco suave

    private FoodDeliveryManager deliveryManager;
    private Transform playerTransform;

    void Awake()
    {
        EnsureUI();
    }

    void Start()
    {
        FindReferences();
    }

    void Update()
    {
        if (deliveryManager == null || playerTransform == null)
        {
            FindReferences();
            return;
        }

        UpdateHUD();
    }

    private void FindReferences()
    {
        if (deliveryManager == null)
        {
            deliveryManager = Object.FindAnyObjectByType<FoodDeliveryManager>();
        }

        if (playerTransform == null)
        {
            PlayerPickup player = Object.FindAnyObjectByType<PlayerPickup>();
            if (player != null) playerTransform = player.transform;
            else if (Camera.main != null) playerTransform = Camera.main.transform;
        }
    }

    private void EnsureUI()
    {
        if (missionText != null) return;

        // Si ya hay un TMP_Text en este mismo GameObject o sus hijos, usarlo
        missionText = GetComponentInChildren<TMP_Text>();
        if (missionText != null)
        {
            missionContainer = missionText.transform.parent != null ? missionText.transform.parent.gameObject : missionText.gameObject;
            return;
        }

        // Buscar el HUDCanvas de la escena para auto-generar un banner elegante
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            foreach (var c in allCanvases)
            {
                if (c.name.Contains("HUD"))
                {
                    canvas = c;
                    break;
                }
            }
            if (canvas == null && allCanvases.Length > 0) canvas = allCanvases[0];
        }

        if (canvas != null)
        {
            GameObject container = new GameObject("DeliveryMissionBanner", typeof(RectTransform));
            container.transform.SetParent(canvas.transform, false);

            RectTransform rect = container.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -25f);
            rect.sizeDelta = new Vector2(480f, 44f);

            // Fondo translúcido
            Image bg = container.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.1f, 0.75f);

            // Texto TextMeshPro
            GameObject textGO = new GameObject("MissionText", typeof(RectTransform));
            textGO.transform.SetParent(container.transform, false);

            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-20f, -6f);

            missionText = textGO.AddComponent<TextMeshProUGUI>();
            missionText.alignment = TextAlignmentOptions.Center;
            missionText.fontSize = 20;
            missionText.fontStyle = FontStyles.Bold;
            missionText.color = Color.white;

            missionContainer = container;
        }
    }

    private void UpdateHUD()
    {
        if (missionText == null) return;

        if (deliveryManager == null)
        {
            deliveryManager = Object.FindAnyObjectByType<FoodDeliveryManager>();
            if (deliveryManager == null)
            {
                missionText.text = "Esperando FoodDeliveryManager...";
                return;
            }
        }

        if (missionContainer != null && !missionContainer.activeSelf)
            missionContainer.SetActive(true);

        DeliveryPoint target = deliveryManager.CurrentDestination;

        if (target == null)
        {
            missionText.text = "ASIGNANDO PEDIDO...";
            missionText.color = waitingColor;
            return;
        }

        Vector3 playerPos = playerTransform != null 
            ? playerTransform.position 
            : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);

        switch (deliveryManager.CurrentState)
        {
            case FoodDeliveryManager.DeliveryState.WaitingForPickup:
                PickableItem nearestFood = deliveryManager.GetNearestAvailableFoodItem(playerPos);
                if (nearestFood != null)
                {
                    float distToFood = Vector3.Distance(playerPos, nearestFood.transform.position);
                    missionText.text = distToFood > 5f
                        ? $"RECOGE UNA OLLA CON COMIDA ({Mathf.RoundToInt(distToFood)}m) -> DESTINO: {target.houseName.ToUpper()}"
                        : $"RECOGE UNA OLLA CON COMIDA -> DESTINO: {target.houseName.ToUpper()}";
                }
                else
                {
                    missionText.text = $"RECOGE UNA OLLA CON COMIDA -> DESTINO: {target.houseName.ToUpper()}";
                }
                missionText.color = waitingColor;
                break;

            case FoodDeliveryManager.DeliveryState.InTransit:
                float dist = Vector3.Distance(playerPos, target.transform.position);
                int meters = Mathf.Max(1, Mathf.RoundToInt(dist));
                missionText.text = $"ENTREGAR EN: {target.houseName.ToUpper()}  •  {meters} M";
                missionText.color = inTransitColor;
                break;

            case FoodDeliveryManager.DeliveryState.Completed:
                missionText.text = $"¡ENTREGA COMPLETADA!  +${deliveryManager.rewardPerDelivery}";
                missionText.color = successColor;
                break;

            default:
                break;
        }
    }
}
