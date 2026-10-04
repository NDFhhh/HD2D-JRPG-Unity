using MFramework.Event;
using TMPro;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour,
    IEventReceiver<ActiveEntityChangedEvent>,
    IEventReceiver<EntityStatChangedEvent>
{
    [Header("UI Elements")] 
    [SerializeField] private TMP_Text characterName;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Slider spSlider;
    [SerializeField] private TMP_Text spText;
    [SerializeField] private Slider bpSlider;
    
    [Header("HighLight")]
    [SerializeField] private Transform highLightRoot;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite activeBackground;
    [SerializeField] private float activeScale = 1.25f;
    
    private BattleEntity _targetEntity;
    private Sprite _normalBackground;
    private Vector3 _baseScale = Vector3.one;

    private bool _isActive;

    private void Awake()
    {
        _baseScale = highLightRoot.localScale;
        _normalBackground = backgroundImage.sprite;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<ActiveEntityChangedEvent>(this);
        EventBus.Subscribe<EntityStatChangedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ActiveEntityChangedEvent>(this);
        EventBus.Unsubscribe<EntityStatChangedEvent>(this);
    }

    public void Setup(BattleEntity entity)
    {
        _targetEntity = entity;
        characterName.text = entity.RuntimeData.Definition.Name;
        
        var stats = entity.RuntimeData.GetTotalStats();

        hpSlider.maxValue = stats.MaxHp;
        spSlider.maxValue = stats.MaxSp;
        bpSlider.maxValue = 5;
        
        RefreshUI();
    }

    private void RefreshUI()
    {
        var data =  _targetEntity.RuntimeData;
        StatBlock stats = data.GetTotalStats();
        
        hpSlider.value = data.CurrentHP;
        hpText.text = $"{data.CurrentHP}/{stats.MaxHp}";
        spSlider.value = data.CurrentSP;
        spText.text = $"{data.CurrentSP}/{stats.MaxSp}";
        bpSlider.value = data.CurrentBP;
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    #region 事件监听

    public void OnEvent(ActiveEntityChangedEvent evt)
    {
        if (_targetEntity == null)
        {
            SetActiveVisual(false);
            return;
        }

        SetActiveVisual(evt.Entity == _targetEntity);
    }

    private void SetActiveVisual(bool active)
    {
        if (_isActive == active)
            return;
        
        _isActive = active;
        backgroundImage.sprite = active ? activeBackground : _normalBackground;
        backgroundImage.SetNativeSize();
        
        highLightRoot.localScale = active ? _baseScale * activeScale : _baseScale;
        highLightRoot.localPosition = active
            ? highLightRoot.localPosition + Vector3.up * 6f
            : highLightRoot.localPosition - Vector3.up * 6f;
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    public void OnEvent(EntityStatChangedEvent evt)
    {
        if (evt.Entity != _targetEntity) return;
        RefreshUI();
    }
    
    #endregion
    
}
