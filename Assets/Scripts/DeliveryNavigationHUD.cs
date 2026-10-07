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

    [Header("Barra de Temperatura / Contrarreloj")]
    public GameObject timerBarRoot;
    public Image timerFillBar;
    public Color boilingBarColor = new Color(0.1f, 0.95f, 0.5f, 1f);   // Verde esmeralda (> 50%)
    public Color hotBarColor = new Color(1f, 0.8f, 0.2f, 1f);          // Naranja cálido (15% - 50%)
    public Color coldBarColor = new Color(1f, 0.25f, 0.25f, 1f);       // Rojo peligro (< 15%)

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

            // Barra de termómetro bajo el banner
            GameObject barRootGO = new GameObject("OrderThermometerBar", typeof(RectTransform));
            barRootGO.transform.SetParent(container.transform, false);
            RectTransform barRect = barRootGO.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = new Vector2(0f, -3f);
            barRect.sizeDelta = new Vector2(0f, 6f);

            Image barBg = barRootGO.AddComponent<Image>();
            barBg.color = new Color(0.12f, 0.12f, 0.16f, 0.9f);

            GameObject fillGO = new GameObject("ThermometerFill", typeof(RectTransform));
            fillGO.transform.SetParent(barRootGO.transform, false);
            RectTransform fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            timerFillBar = fillGO.AddComponent<Image>();
            timerFillBar.type = Image.Type.Filled;
            timerFillBar.fillMethod = Image.FillMethod.Horizontal;
            timerFillBar.fillOrigin = 0;
            timerFillBar.fillAmount = 1f;
            timerFillBar.color = boilingBarColor;

            timerBarRoot = barRootGO;
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

        // 1. PRIORIDAD: Estados globales de ShiftManager (Retorno a base o Fin de turno)
        if (ShiftManager.Instance != null)
        {
            if (ShiftManager.Instance.CurrentState == ShiftManager.ShiftState.ReturningToBase)
            {
                if (timerBarRoot != null) timerBarRoot.SetActive(false);

                Vector3 basePos = ShiftManager.Instance.KitchenBasePosition;
                Vector3 playerPos = playerTransform != null ? playerTransform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
                float distToBase = Vector3.Distance(new Vector3(playerPos.x, basePos.y, playerPos.z), basePos);
                int meters = Mathf.Max(1, Mathf.RoundToInt(distToBase));
                int shiftSecs = Mathf.CeilToInt(ShiftManager.Instance.ShiftTimeRemaining);
                int mins = shiftSecs / 60;
                int secs = shiftSecs % 60;

                missionText.text = $"[CUOTA CUMPLIDA] REGRESA A LA COCINA  •  {meters} M  •  {mins:00}:{secs:00}";
                missionText.color = inTransitColor;
                return;
            }
            else if (ShiftManager.Instance.CurrentState == ShiftManager.ShiftState.DayCompleted)
            {
                if (timerBarRoot != null) timerBarRoot.SetActive(false);
                missionText.text = "¡JORNADA COMPLETADA CON ÉXITO!";
                missionText.color = successColor;
                return;
            }
            else if (ShiftManager.Instance.CurrentState == ShiftManager.ShiftState.DayFailed)
            {
                if (timerBarRoot != null) timerBarRoot.SetActive(false);
                missionText.text = "JORNADA FALLIDA: TIEMPO AGOTADO";
                missionText.color = coldBarColor;
                return;
            }
        }

        DeliveryPoint target = deliveryManager.CurrentDestination;

        if (target == null)
        {
            if (timerBarRoot != null) timerBarRoot.SetActive(false);
            missionText.text = "ASIGNANDO PEDIDO...";
            missionText.color = waitingColor;
            return;
        }

        Vector3 currentPos = playerTransform != null 
            ? playerTransform.position 
            : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);

        // Actualizar barra de termómetro del pedido
        if (timerBarRoot != null)
        {
            bool showTimer = deliveryManager.enableOrderTimer && deliveryManager.IsOrderTimerActive;
            if (timerBarRoot.activeSelf != showTimer) timerBarRoot.SetActive(showTimer);

            if (showTimer && timerFillBar != null)
            {
                float ratio = deliveryManager.NormalizedOrderTime;
                timerFillBar.fillAmount = ratio;
                if (ratio > 0.5f)
                {
                    timerFillBar.color = boilingBarColor;
                }
                else if (ratio >= 0.15f)
                {
                    timerFillBar.color = hotBarColor;
                }
                else
                {
                    bool pulse = Mathf.PingPong(Time.time * 5f, 1f) > 0.4f;
                    timerFillBar.color = pulse ? coldBarColor : Color.white;
                }
            }
        }

        string timerString = "";
        if (deliveryManager.enableOrderTimer && deliveryManager.IsOrderTimerActive)
        {
            int tSecs = Mathf.CeilToInt(deliveryManager.CurrentOrderTimeRemaining);
            int tMins = tSecs / 60;
            int remSecs = tSecs % 60;
            timerString = $"  •  {tMins:00}:{remSecs:00}";
        }

        switch (deliveryManager.CurrentState)
        {
            case FoodDeliveryManager.DeliveryState.WaitingForPickup:
                PickableItem nearestFood = deliveryManager.GetNearestAvailableFoodItem(currentPos);
                if (nearestFood != null)
                {
                    float distToFood = Vector3.Distance(currentPos, nearestFood.transform.position);
                    missionText.text = distToFood > 5f
                        ? $"RECOGE UNA OLLA ({Mathf.RoundToInt(distToFood)}m) -> {target.houseName.ToUpper()}{timerString}"
                        : $"RECOGE UNA OLLA -> {target.houseName.ToUpper()}{timerString}";
                }
                else
                {
                    missionText.text = $"RECOGE UNA OLLA CON COMIDA -> {target.houseName.ToUpper()}{timerString}";
                }
                missionText.color = waitingColor;
                break;

            case FoodDeliveryManager.DeliveryState.InTransit:
                float dist = Vector3.Distance(currentPos, target.transform.position);
                int metersToHouse = Mathf.Max(1, Mathf.RoundToInt(dist));

                string qualityTag = "";
                if (deliveryManager.enableOrderTimer && deliveryManager.IsOrderTimerActive)
                {
                    float norm = deliveryManager.NormalizedOrderTime;
                    if (norm > 0.5f) qualityTag = "  •  +PROPINA";
                    else if (norm >= 0.15f) qualityTag = "  •  CALIENTE";
                    else qualityTag = "  •  ¡ENFRIÁNDOSE!";
                }

                missionText.text = $"ENTREGAR EN: {target.houseName.ToUpper()}  •  {metersToHouse} M{timerString}{qualityTag}";

                // Cambiar color a rojo parpadeante si queda menos del 15% de tiempo
                if (deliveryManager.enableOrderTimer && deliveryManager.NormalizedOrderTime < 0.15f)
                {
                    bool blink = Mathf.PingPong(Time.time * 4f, 1f) > 0.5f;
                    missionText.color = blink ? coldBarColor : inTransitColor;
                }
                else
                {
                    missionText.color = inTransitColor;
                }
                break;

            case FoodDeliveryManager.DeliveryState.Completed:
                if (timerBarRoot != null) timerBarRoot.SetActive(false);
                missionText.text = $"¡ENTREGA COMPLETADA!  +${deliveryManager.rewardPerDelivery}";
                missionText.color = successColor;
                break;

            default:
                break;
        }
    }
}
