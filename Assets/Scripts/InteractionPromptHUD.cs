using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sistema UNIFICADO de interacción para la UI en pantalla (HUD).
/// Un solo GameObject en el Canvas gestiona todos los avisos de "E + TEXTO":
///  - "Para entrar" (Puerta del conductor)
///  - "Para abrir" / "Para cerrar" (Puerta trasera de la van)
///  - "Para guardar" (Cargar objeto en la van abierta)
///  - "Para recoger" (Al mirar un objeto del suelo)
///  - "¡Manos ocupadas!" (Alertas contextuales)
/// </summary>
public class InteractionPromptHUD : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("Contenedor raíz del prompt.")]
    public RectTransform promptContainer;

    [Tooltip("CanvasGroup para animación suave de opacidad.")]
    public CanvasGroup canvasGroup;

    [Tooltip("Imagen del ícono de la tecla (keyboard_e).")]
    public Image keyIcon;

    [Tooltip("Texto TextMeshPro donde se muestra la acción.")]
    public TMP_Text actionText;

    [Header("Textos Personalizables")]
    public string enterVehicleText = "Para entrar";
    public string openDoorText = "Para abrir";
    public string closeDoorText = "Para cerrar";
    public string loadCargoText = "Para guardar";
    public string unloadCargoText = "Para sacar";
    public string pickupItemText = "Para recoger";
    public string exitVehicleText = "Para salir";
    public string handsOccupiedText = "¡Manos ocupadas! Presiona [G] para soltar";
    public string cargoFullText = "Van llena: Sin espacio disponible";

    [Header("Opciones")]
    [Tooltip("¿Mostrar el prompt de recoger al apuntar a objetos del suelo?")]
    public bool showPickupPrompt = true;

    [Tooltip("¿Mostrar 'Para salir' mientras se conduce?")]
    public bool showExitWhileDriving = false;

    [Header("Colores")]
    public Color normalTextColor = Color.white;
    public Color alertTextColor = new Color(1f, 0.45f, 0.45f, 1f); // Rojo suave / coral
    public Color normalKeyColor = Color.white;
    public Color alertKeyColor = new Color(1f, 1f, 1f, 0.3f);

    [Header("Animación")]
    [Tooltip("Multiplicador de escala global del prompt (ej: 1.3).")]
    public float promptScale = 1.3f;

    [Tooltip("Velocidad de fundido y animación.")]
    public float transitionSpeed = 14f;

    [Tooltip("¿Animar con ligero pop de escala al aparecer?")]
    public bool useScaleAnimation = true;

    // Referencias a los subsistemas del juego
    private VehicleInteraction vehicle;
    private VanDoorController vanDoor;
    private CargoZone cargoZone;
    private PlayerPickup playerPickup;

    private float targetAlpha = 0f;
    private Vector3 targetScale = Vector3.one;

    void Awake()
    {
        if (promptContainer == null) promptContainer = GetComponent<RectTransform>();

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null && promptContainer != null)
            {
                canvasGroup = promptContainer.gameObject.AddComponent<CanvasGroup>();
            }
        }

        // Buscar el icono en los hijos para evitar tomar la imagen de fondo
        if (keyIcon == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject != gameObject)
                {
                    keyIcon = img;
                    break;
                }
            }
        }

        if (actionText == null) actionText = GetComponentInChildren<TMP_Text>();

        // Iniciar oculto
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        if (promptContainer != null && useScaleAnimation)
        {
            promptContainer.localScale = Vector3.one * (promptScale * 0.85f);
        }
    }

    void Start()
    {
        FindGameReferences();
    }

    void LateUpdate()
    {
        EnsureReferences();
        EvaluateInteractions();
        UpdateAnimation();
    }

    /// <summary>
    /// Evalúa en orden de prioridad qué interacción está ocurriendo y actualiza el texto/icono.
    /// </summary>
    private void EvaluateInteractions()
    {
        bool hasItem = playerPickup != null && playerPickup.IsCarryingItem;

        // ── 1. PRIORIDAD 1: Zona Trasera de la Van (Puerta y Carga) ──────
        if (vanDoor != null && vanDoor.PlayerInRearZone)
        {
            if (vanDoor.IsOpen)
            {
                // Puerta ABIERTA
                if (hasItem)
                {
                    // Llevamos objeto: ¿Podemos guardarlo en la van?
                    if (cargoZone != null && cargoZone.CanLoadItem())
                    {
                        ShowPrompt(loadCargoText, normalTextColor, true, normalKeyColor);
                        return;
                    }
                    else if (cargoZone != null && !cargoZone.CanLoadItem())
                    {
                        ShowPrompt(cargoFullText, alertTextColor, false, alertKeyColor);
                        return;
                    }
                }
                else
                {
                    // Manos libres:
                    // ¿Estamos apuntando a algún objeto recogible (en el suelo o dentro de la van)?
                    if (playerPickup != null && playerPickup.HoveredItem != null)
                    {
                        if (playerPickup.HoveredItem.IsStoredInCargo)
                        {
                            ShowPrompt(unloadCargoText, normalTextColor, true, normalKeyColor);
                        }
                        else
                        {
                            ShowPrompt(pickupItemText, normalTextColor, true, normalKeyColor);
                        }
                        return;
                    }

                    // Si no apuntamos a ningún objeto, el botón cierra la puerta
                    ShowPrompt(closeDoorText, normalTextColor, true, normalKeyColor);
                    return;
                }
            }
            else
            {
                // Puerta CERRADA
                if (hasItem)
                {
                    // Manos ocupadas: no puede abrir la puerta
                    ShowPrompt(handsOccupiedText, alertTextColor, false, alertKeyColor);
                    return;
                }
                else
                {
                    // Manos libres: ¿Está apuntando a un objeto en el suelo fuera de la van?
                    if (playerPickup != null && playerPickup.HoveredItem != null && !playerPickup.HoveredItem.IsStoredInCargo)
                    {
                        ShowPrompt(pickupItemText, normalTextColor, true, normalKeyColor);
                        return;
                    }

                    // Manos libres: puede abrir con [E]
                    ShowPrompt(openDoorText, normalTextColor, true, normalKeyColor);
                    return;
                }
            }
        }

        // ── 2. PRIORIDAD 2: Conduciendo (Salir del auto) ─────────────────
        if (vehicle != null && vehicle.PlayerIsInside)
        {
            if (showExitWhileDriving)
            {
                ShowPrompt(exitVehicleText, normalTextColor, true, normalKeyColor);
            }
            else
            {
                HidePrompt();
            }
            return;
        }

        // ── 3. PRIORIDAD 3: Zona de Entrada del Vehículo (Conductor) ─────
        if (vehicle != null && vehicle.CanInteract)
        {
            if (hasItem)
            {
                ShowPrompt(handsOccupiedText, alertTextColor, false, alertKeyColor);
            }
            else
            {
                ShowPrompt(enterVehicleText, normalTextColor, true, normalKeyColor);
            }
            return;
        }

        // ── 4. PRIORIDAD 4: Mirando un objeto recogible del suelo ────────
        if (showPickupPrompt && playerPickup != null && !hasItem && playerPickup.HoveredItem != null)
        {
            ShowPrompt(pickupItemText, normalTextColor, true, normalKeyColor);
            return;
        }

        // ── 5. NINGUNA INTERACCIÓN ACTIVA: Ocultar ────────────────────────
        HidePrompt();
    }

    private void ShowPrompt(string text, Color textColor, bool showKey, Color keyColor)
    {
        if (actionText != null)
        {
            actionText.text = text;
            actionText.color = textColor;
        }

        if (keyIcon != null)
        {
            keyIcon.gameObject.SetActive(showKey);
            keyIcon.color = keyColor;
        }

        targetAlpha = 1f;
        targetScale = Vector3.one * promptScale;
    }

    private void HidePrompt()
    {
        targetAlpha = 0f;
        targetScale = Vector3.one * (promptScale * 0.85f);
    }

    private void UpdateAnimation()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * transitionSpeed);
            canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.5f;
        }

        if (promptContainer != null && useScaleAnimation)
        {
            promptContainer.localScale = Vector3.Lerp(
                promptContainer.localScale,
                targetScale,
                Time.deltaTime * transitionSpeed
            );
        }
    }

    private void EnsureReferences()
    {
        if (vehicle == null || vanDoor == null || cargoZone == null || playerPickup == null)
        {
            FindGameReferences();
        }
    }

    private void FindGameReferences()
    {
        if (vehicle == null) vehicle = Object.FindFirstObjectByType<VehicleInteraction>();
        if (vanDoor == null) vanDoor = Object.FindFirstObjectByType<VanDoorController>();
        if (cargoZone == null) cargoZone = Object.FindFirstObjectByType<CargoZone>();
        if (playerPickup == null) playerPickup = Object.FindFirstObjectByType<PlayerPickup>();
    }
}
