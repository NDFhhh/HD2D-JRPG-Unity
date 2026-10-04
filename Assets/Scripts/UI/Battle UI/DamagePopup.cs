using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

public class DamagePopup : MonoBehaviour
{
    [Header("Components")]
    private TMP_Text textMesh;
    
    [Header("Motion Settings (位移设置)")]
    [SerializeField] private float initialVelocityY = 10f;
    [SerializeField] private Vector2 randomHorizontalRange = new Vector2(-3f, 3f);
    [SerializeField] private float drag = 5f;
    [SerializeField] private float gravity = 0f;
    
    [Header("Scale Settings (缩放弹跳)")]
    [SerializeField] private Ease scaleCurve = Ease.OutBack;
    [SerializeField] private float scaleDuration = 0.3f;
    
    [Header("Fade Settings (淡出设置)")]
    [SerializeField] private float lifetime = 1.0f;
    [SerializeField] private float fadeOutStartTime = 0.6f;
    
    private Vector3 _velocity;
    private float _timer;
    private Color _baseColor;
    private Vector3 _baseScale;
    private ObjectPool<DamagePopup> _pool;
    public void SetPool(ObjectPool<DamagePopup> pool) => _pool = pool;

    private void Awake()
    {
        textMesh = GetComponent<TMP_Text>();
        _baseColor = textMesh.color;
        _baseScale = transform.localScale;
    }

    public void Setup(int amount, E_DamagePopupType type)
    {
        textMesh.text = ConvertNumberToSpriteString(amount, type);
        _timer = 0f;
        textMesh.color = _baseColor;
        textMesh.alpha = 1f;
        transform.localScale = _baseScale * 0f;

        _velocity = new Vector3(Random.Range(randomHorizontalRange.x, randomHorizontalRange.y), initialVelocityY, 0f);
    }

    private void Update()
    {
        float dt = Time.deltaTime <= 0 ? Time.unscaledDeltaTime : Time.deltaTime;
        _timer += dt;
        
        UpdateScale();
        UpdateMotion(dt);
        UpdateFade();

        if (_timer >= lifetime)
            Release();
    }

    private void UpdateScale()
    {
        if (scaleDuration <= 0f || _timer >= scaleDuration)
        {
            transform.localScale = _baseScale;
            return;
        }
        
        float t = Mathf.Clamp01(_timer / scaleDuration);
        float scaleValue = DOVirtual.EasedValue(0f, 1f, t, scaleCurve);
        transform.localScale = _baseScale * scaleValue;
    }

    private void UpdateMotion(float dt)
    {
        transform.position += _velocity * dt;
        _velocity -=  _velocity * drag * dt;
        
        if(gravity != 0f)
            _velocity += new Vector3(0f, -gravity, 0f) * dt;
    }

    private void UpdateFade()
    {
        if (_timer < fadeOutStartTime || lifetime <= fadeOutStartTime)
            return;

        float t = Mathf.Clamp01((_timer - fadeOutStartTime) - (lifetime - fadeOutStartTime));
        textMesh.alpha = Mathf.Lerp(1f, 0f, t);
    }

    private void Release()
    {
        _pool.Release(this);
    }

    private string ConvertNumberToSpriteString(int value, E_DamagePopupType type)
    {
        string original = Mathf.Abs(value).ToString();
        StringBuilder builder = new();
        int startIndex = (int)type * 10;
        foreach (var c in original)
        {
            int digit = c - '0';
            builder.Append($"<sprite={startIndex + digit}>");
        }

        return builder.ToString();
    }
}
