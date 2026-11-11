using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// シーンをまたいで常駐し、画面下からせり上がる共通トランジションを表示します。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-2000)]
public sealed class ScreenTransition : MonoBehaviour
{
    private static readonly Color32 PanelColor = new Color32(60, 60, 60, 255);
    private static ScreenTransition instance;

    [Header("画面遷移アニメーション")]
    [Min(0f)] [SerializeField] private float coverDuration = 0.35f;
    [Min(0f)] [SerializeField] private float revealDuration = 0.35f;
    [Min(0f)] [SerializeField] private float revealHoldDuration = 0.05f;

    private Canvas overlayCanvas;
    private RectTransform panelRect;
    private GameObject panelObject;
    private Coroutine transitionRoutine;
    private bool isTransitioning;
    private bool revealOnNextSceneLoad = true;

    public static bool IsTransitioning => instance != null && instance.isTransitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject transitionObject = new GameObject(nameof(ScreenTransition));
        transitionObject.AddComponent<ScreenTransition>();
    }

    /// <summary>共通アニメーションを再生してから指定シーンへ移動します。</summary>
    public static void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

        if (instance == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        instance.BeginSceneLoad(sceneName);
    }

    public static void LoadSceneWithIris(string sceneName, Transform target, Camera camera, Shader shader)
    {
        if (instance == null || shader == null) { LoadScene(sceneName); return; }
        if (instance.isTransitioning || string.IsNullOrEmpty(sceneName)) return;
        instance.isTransitioning = true;
        Time.timeScale = 1f;
        instance.transitionRoutine = instance.StartCoroutine(instance.IrisAndLoad(sceneName, target, camera, shader));
    }

    private IEnumerator IrisAndLoad(string sceneName, Transform target, Camera camera, Shader shader)
    {
        Image image = panelObject.GetComponent<Image>();
        Material material = new Material(shader);
        panelObject.SetActive(true);
        panelRect.anchoredPosition = Vector2.zero;
        image.color = Color.white;
        image.material = material;
        Canvas.ForceUpdateCanvases();
        float elapsed = 0f;
        const float duration = 0.45f;
        try
        {
            while (elapsed < duration)
            {
                Vector2 center = new Vector2(0.5f, 0.5f);
                if (target != null && target.gameObject.activeInHierarchy && camera != null)
                {
                    Vector2 screen = camera.WorldToScreenPoint(target.position);
                    center = new Vector2(Mathf.Clamp01(screen.x / Screen.width), Mathf.Clamp01(screen.y / Screen.height));
                }
                float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
                float radius = new Vector2(Mathf.Max(center.x, 1f - center.x) * aspect,
                    Mathf.Max(center.y, 1f - center.y)).magnitude + 0.01f;
                float t = Mathf.Clamp01(elapsed / duration);
                material.SetColor("_Color", PanelColor);
                material.SetVector("_Center", new Vector4(center.x, center.y, 0f, 0f));
                material.SetFloat("_Aspect", aspect);
                material.SetFloat("_Radius", Mathf.Lerp(radius, -0.01f, t * t * t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            material.SetFloat("_Radius", -0.01f);
            yield return null;
        }
        finally
        {
            image.material = null;
            image.color = PanelColor;
            Destroy(material);
        }
        yield return CoverAndLoad(sceneName, true);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        CreateOverlay();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void CreateOverlay()
    {
        overlayCanvas = gameObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = short.MaxValue;
        gameObject.AddComponent<GraphicRaycaster>();

        panelObject = new GameObject("TransitionPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(transform, false);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;
    }

    private void BeginSceneLoad(string sceneName)
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        Time.timeScale = 1f;
        transitionRoutine = StartCoroutine(CoverAndLoad(sceneName));
    }

    private IEnumerator CoverAndLoad(string sceneName, bool alreadyCovered = false)
    {
        panelObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        float hiddenY = GetHiddenPositionY();
        if (!alreadyCovered)
        {
            panelRect.anchoredPosition = new Vector2(0f, hiddenY);
            yield return AnimatePanel(hiddenY, 0f, coverDuration);
        }

        panelRect.anchoredPosition = Vector2.zero;
        revealOnNextSceneLoad = false;

        // 常駐オブジェクト上の同じコルーチンでロード完了まで待つことで、
        // sceneLoadedの実行順に依存せず、新しい画面側の下降演出を必ず開始します。
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
        if (loadOperation == null)
        {
            transitionRoutine = null;
            isTransitioning = false;
            yield break;
        }

        yield return loadOperation;
        yield return RevealScene();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!revealOnNextSceneLoad)
        {
            return;
        }

        revealOnNextSceneLoad = false;
        isTransitioning = true;
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        transitionRoutine = StartCoroutine(RevealScene());
    }

    private IEnumerator RevealScene()
    {
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = short.MaxValue;
        panelRect.SetAsLastSibling();
        panelObject.SetActive(true);
        panelRect.anchoredPosition = Vector2.zero;
        Canvas.ForceUpdateCanvases();

        // 新しい画面を覆った状態を実際に描画してから下降を始めます。
        yield return new WaitForEndOfFrame();
        if (revealHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(revealHoldDuration);
        }

        float hiddenY = GetHiddenPositionY();
        yield return AnimatePanel(0f, hiddenY, revealDuration);

        panelRect.anchoredPosition = new Vector2(0f, hiddenY);
        panelObject.SetActive(false);
        transitionRoutine = null;
        isTransitioning = false;
    }

    private IEnumerator AnimatePanel(float fromY, float toY, float duration)
    {
        if (duration <= 0f)
        {
            panelRect.anchoredPosition = new Vector2(0f, toY);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            // ResultBG / PauseBGと同じEase.OutCubicです。
            float remaining = 1f - progress;
            float easedProgress = 1f - remaining * remaining * remaining;
            panelRect.anchoredPosition = new Vector2(
                0f,
                Mathf.LerpUnclamped(fromY, toY, easedProgress));
            yield return null;
        }

        panelRect.anchoredPosition = new Vector2(0f, toY);
    }

    private static float GetHiddenPositionY() => -Mathf.Max(1f, Screen.height);

    private void OnValidate()
    {
        coverDuration = Mathf.Max(0f, coverDuration);
        revealDuration = Mathf.Max(0f, revealDuration);
        revealHoldDuration = Mathf.Max(0f, revealHoldDuration);
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        instance = null;
    }
}
