using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SceneLoader
{
    public static string targetScene;
}

public class LoadingScene : MonoBehaviour
{
    public static LoadingScene Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Slider slider;
    [SerializeField] private Image fill;
    [SerializeField] private TMP_Text percentText;
    [SerializeField] private TMP_Text tipText;

    [Header("Loading Tips")]
    [TextArea]
    [SerializeField] private string[] loadingTips;

    private float displayProgress;
    private bool isLoading;
    private string currentLoadingStep = "Preparing game session...";
    private string currentTip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        GameSession.OnGameplayCachePreloadStep += HandleGameplayCachePreloadStep;
    }

    private void OnDisable()
    {
        GameSession.OnGameplayCachePreloadStep -= HandleGameplayCachePreloadStep;
    }

    private void Start()
    {
        if (isLoading)
            return;

        isLoading = true;

        if (string.IsNullOrEmpty(SceneLoader.targetScene))
        {
            Debug.LogError("SceneLoader.targetScene is null or empty!");
            return;
        }

        StartCoroutine(ChangeTipsRoutine());
        StartCoroutine(InitializeAndLoad(SceneLoader.targetScene));
    }

    private IEnumerator InitializeAndLoad(string sceneName)
    {
        SetLoadingStep("Preparing game session...");
        SetProgressUI(0f);

        if (GameSession.IsGameplayScene(sceneName))
        {
            SetLoadingStep("Build Cache: preparing gameplay data...");
            SetProgressUI(0f);
            yield return GameSession.Instance.PreloadGameplayCacheAsync();

            if (!GameSession.Instance.IsGameplayCacheReady)
            {
                SetLoadingStep("Build Cache failed");
                SetProgressUI(0f);
                Debug.LogError("[LoadingScene] Gameplay cache preload failed. Scene activation cancelled.");
                yield break;
            }
        }
        else
        {
            SetLoadingStep("Refreshing persistent session data...");
            GameSession.Instance.ReloadPersistentProgress();
            GameSession.Instance.ClearGameplayCache();
        }

        yield return LoadTargetScene(sceneName);
    }

    private IEnumerator ChangeTipsRoutine()
    {
        while (true)
        {
            if (loadingTips != null && loadingTips.Length > 0 && tipText != null)
            {
                int index = Random.Range(0, loadingTips.Length);
                currentTip = loadingTips[index];
                RefreshTipText();
            }

            yield return new WaitForSeconds(2f);
        }
    }

    private IEnumerator LoadTargetScene(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);
            displayProgress = Mathf.MoveTowards(displayProgress, targetProgress, Time.deltaTime * 0.5f);
            SetLoadingStep("Entering " + sceneName + "...");
            SetProgressUI(displayProgress);

            if (displayProgress >= 1f && targetProgress >= 1f)
            {
                yield return new WaitForSeconds(0.5f);
                operation.allowSceneActivation = true;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void SetProgressUI(float value)
    {
        if (slider != null)
            slider.value = value;

        if (fill != null)
            fill.fillAmount = value;

        if (percentText != null)
            percentText.text = (value * 100f).ToString("F0") + "%\n" + currentLoadingStep;
    }

    private void HandleGameplayCachePreloadStep(string message, float progress)
    {
        displayProgress = Mathf.Max(displayProgress, progress);
        SetLoadingStep(message);
        SetProgressUI(displayProgress);
    }

    private void SetLoadingStep(string message)
    {
        currentLoadingStep = string.IsNullOrWhiteSpace(message) ? "Loading..." : message;
        RefreshTipText();
    }

    private void RefreshTipText()
    {
        if (tipText == null)
            return;

        string sanitizedTip = TextSanitizer.Sanitize(currentTip);
        tipText.text = string.IsNullOrWhiteSpace(sanitizedTip) ? string.Empty : "TIP: " + sanitizedTip;
    }
}

