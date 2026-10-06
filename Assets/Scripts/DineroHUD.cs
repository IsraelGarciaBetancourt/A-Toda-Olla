using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// HUD del dinero del jugador.
/// Muestra el total ganado usando el sprite DineroUI como fondo
/// y un TextMeshPro superpuesto en el area negra del panel.
/// Se conecta automaticamente al evento OnScoreOrMoneyChanged del FoodDeliveryManager.
/// </summary>
public class DineroHUD : MonoBehaviour
{
    [Header("Referencias UI")]
    [Tooltip("Texto TMP donde se muestra la cantidad de dinero.")]
    public TextMeshProUGUI moneyText;

    [Tooltip("El RectTransform raiz del panel de dinero (para la animacion de pop).")]
    public RectTransform panelRoot;

    [Header("Animacion de Pop")]
    [Tooltip("Escala maxima al recibir dinero (1.0 = sin efecto).")]
    [Range(1f, 1.5f)]
    public float popScale = 1.18f;

    [Tooltip("Duracion total de la animacion de pop en segundos.")]
    public float popDuration = 0.35f;

    [Tooltip("Color de destello del texto al ganar dinero.")]
    public Color earnColor = new Color(1f, 0.92f, 0.25f, 1f);

    // Estado interno
    private int displayedMoney = 0;
    private Color originalTextColor;
    private Vector3 originalScale;
    private Coroutine popCoroutine;

    void Awake()
    {
        if (moneyText == null)
            moneyText = GetComponentInChildren<TextMeshProUGUI>();

        if (panelRoot == null)
            panelRoot = GetComponent<RectTransform>();

        if (moneyText != null)
            originalTextColor = moneyText.color;

        if (panelRoot != null)
            originalScale = panelRoot.localScale;
    }

    void Start()
    {
        if (FoodDeliveryManager.Instance != null)
        {
            FoodDeliveryManager.Instance.OnScoreOrMoneyChanged.AddListener(OnMoneyChanged);
            displayedMoney = FoodDeliveryManager.Instance.TotalMoneyEarned;
            RefreshText();
        }
        else
        {
            RefreshText();
            StartCoroutine(LateSubscribe());
        }
    }

    void OnDestroy()
    {
        if (FoodDeliveryManager.Instance != null)
        {
            FoodDeliveryManager.Instance.OnScoreOrMoneyChanged.RemoveListener(OnMoneyChanged);
        }
    }

    private void OnMoneyChanged(int newTotal)
    {
        int gained = newTotal - displayedMoney;
        displayedMoney = newTotal;
        RefreshText();

        if (gained > 0)
        {
            if (popCoroutine != null) StopCoroutine(popCoroutine);
            popCoroutine = StartCoroutine(PlayPopAnimation());
        }
    }

    private void RefreshText()
    {
        if (moneyText != null)
            moneyText.text = "$" + displayedMoney.ToString("N0");
    }

    private IEnumerator PlayPopAnimation()
    {
        if (panelRoot == null) yield break;

        if (moneyText != null)
            moneyText.color = earnColor;

        float half = popDuration * 0.5f;
        float elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / half;
            panelRoot.localScale = Vector3.Lerp(originalScale, originalScale * popScale, t);
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / half;
            panelRoot.localScale = Vector3.Lerp(originalScale * popScale, originalScale, t);
            yield return null;
        }

        panelRoot.localScale = originalScale;

        if (moneyText != null)
            moneyText.color = originalTextColor;

        popCoroutine = null;
    }

    private IEnumerator LateSubscribe()
    {
        yield return null;
        if (FoodDeliveryManager.Instance != null)
        {
            FoodDeliveryManager.Instance.OnScoreOrMoneyChanged.AddListener(OnMoneyChanged);
            displayedMoney = FoodDeliveryManager.Instance.TotalMoneyEarned;
            RefreshText();
        }
        else
        {
            Debug.LogWarning("[DineroHUD] No se encontro FoodDeliveryManager en la escena.");
        }
    }

    public void ForceRefresh()
    {
        if (FoodDeliveryManager.Instance != null)
        {
            displayedMoney = FoodDeliveryManager.Instance.TotalMoneyEarned;
            RefreshText();
        }
    }
}
