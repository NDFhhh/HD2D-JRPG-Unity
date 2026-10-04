using UnityEngine.UI;

public class FadeController : Singleton<FadeController>
{
    private static readonly int Progress = Shader.PropertyToID("_Progress");

    [Header("Default Style")] 
    [SerializeField] private E_FadeStyle defaultStyle = E_FadeStyle.PanelFade;

    [Header("Fade Panel")] 
    [SerializeField] private Image panelImage;
    
    [Header("Wipe Mask")]
    [SerializeField] private RawImage wipeRawImage;
    
    [Header("Timing")] 
    [SerializeField, Range(0.05f, 3f)] private float panelFadeDuration = 0.35f;
    [SerializeField, Range(0.05f, 3f)] private float wipeFadeDuration = 0.6f;

    private E_FadeStyle _currentStyle;
    private Coroutine _fadeRoutine;
    
    private float _nextFadeOutDurationOverride = -1f;
    private float _nextFadeInDurationOverride = -1f;

    private Material _wipeRuntimeMaterial;
    
    protected override void Awake()
    {
        base.Awake();
        _currentStyle = defaultStyle;
        
        panelImage.enabled = false;
        SetPanelAlpha(0);
        
        wipeRawImage.enabled = false;
        _wipeRuntimeMaterial = Instantiate(wipeRawImage.material);
        wipeRawImage.material = _wipeRuntimeMaterial;
        SetWipeProgress(0);
    }
    
    public void SetStyle(E_FadeStyle style)
    {
        _currentStyle = style;
    }

    public void SetNextFadeDurations(float fadeInDuration, float fadeOutDuration)
    {
        _nextFadeInDurationOverride = fadeInDuration > 0f ? fadeInDuration : -1f;
        _nextFadeOutDurationOverride = fadeOutDuration > 0f ? fadeOutDuration : -1f;
    }

    public void FadeOut(Action onDone = null)
    {
        StartFade(1f,ResolveDuration(true),onDone);
    }

    public void FadeIn(Action onDone = null)
    {
        StartFade(0f,ResolveDuration(false),onDone);
    }

    private void StartFade(float target, float duration, Action onDone)
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
        }
        
        _fadeRoutine = StartCoroutine(FadeRoutine(target, duration, onDone));
    }

    private IEnumerator FadeRoutine(float target, float duration, Action onDone)
    {
        panelImage.enabled = _currentStyle == E_FadeStyle.PanelFade;
        wipeRawImage.enabled = _currentStyle == E_FadeStyle.WipeMask;

        float start = ReadCurrentValue();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float alpha = Mathf.Lerp(start, target, elapsed / duration);
            ApplyValue(alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        ApplyValue(target);
        if (target < 0.001f)
        {
            panelImage.enabled = false;
            wipeRawImage.enabled = false;
            SetPanelAlpha(0);
            SetWipeProgress(0);
        }
        onDone?.Invoke();
        _fadeRoutine = null;
    }

    private float ReadCurrentValue()
    {
        if (_currentStyle == E_FadeStyle.PanelFade)
            return panelImage.color.a;
        return _wipeRuntimeMaterial.GetFloat(Progress);
    }

    private void ApplyValue(float value01)
    {
        value01 = Mathf.Clamp01(value01);
        if (_currentStyle == E_FadeStyle.PanelFade)
        {
            SetPanelAlpha(value01);
            return;
        }
        SetWipeProgress(value01);
    }

    private void SetPanelAlpha(float alpha)
    {
        Color color = panelImage.color;
        color.a = alpha;
        panelImage.color = color;
    }

    private void SetWipeProgress(float progress)
    {
        _wipeRuntimeMaterial.SetFloat(Progress, Mathf.Clamp01(progress));
    }

    private float ResolveDuration(bool isFadeOut)
    {
        if (isFadeOut && _nextFadeOutDurationOverride >= 0f)
        {
            float value = _nextFadeOutDurationOverride;
            _nextFadeOutDurationOverride = -1f;
            return value;
        }
        
        if (!isFadeOut && _nextFadeInDurationOverride >= 0f)
        {
            float value = _nextFadeInDurationOverride;
            _nextFadeInDurationOverride = -1f;
            return value;
        }

        return _currentStyle == E_FadeStyle.PanelFade ? panelFadeDuration : wipeFadeDuration;
    }
}
