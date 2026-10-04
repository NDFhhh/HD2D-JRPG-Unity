using MFramework.Event;
using TMPro;
using UnityEngine.UI;

public class WeaknessBar : MonoBehaviour,
    IEventReceiver<EntityShieldChangedEvent>,
    IEventReceiver<EntityWeaknessChangedEvent>,
    IEventReceiver<EntityRecoverFromBreakEvent>
{
    #region 弱点条配置与缓存

    [Header("Shield")] 
    [SerializeField] private TMP_Text shieldText;

    [Header("Weakness")] 
    [SerializeField] private RectTransform weakRoot;
    [SerializeField] private GameObject weakIconPrefab;

    private readonly List<GameObject> _spawnedIcons = new();
    private BattleEntity _targetEntity;
    private DamageTypeIconSet_SO _iconSet;
    
    #endregion

    private void OnEnable()
    {
        EventBus.Subscribe<EntityShieldChangedEvent>(this);
        EventBus.Subscribe<EntityWeaknessChangedEvent>(this);
        EventBus.Subscribe<EntityRecoverFromBreakEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<EntityShieldChangedEvent>(this);
        EventBus.Unsubscribe<EntityWeaknessChangedEvent>(this);
        EventBus.Unsubscribe<EntityRecoverFromBreakEvent>(this);
    }

    public void Setup(BattleEntity targetEntity, DamageTypeIconSet_SO iconSet)
    {
        _targetEntity = targetEntity;
        _iconSet = iconSet;
        
        // 更新护盾
        RefreshShield();
        // 更新弱点
        RebuildWeaknessIcons();
    }

    private void RefreshShield()
    {
        shieldText.text = _targetEntity.CurrentShield.ToString();
    }

    private void RebuildWeaknessIcons()
    {
        // 清空
        foreach (var icon in _spawnedIcons)
        {
            Destroy(icon);
        }
        _spawnedIcons.Clear();
        
        var weaknesses = _targetEntity.GetWeaknesses();
        if (weaknesses.Count == 0) return;

        for (int i = 0; i < weaknesses.Count; i++)
        {
            var icon = _iconSet.GetIcon(weaknesses[i]);
            GameObject instance = Instantiate(weakIconPrefab, weakRoot);
            instance.SetActive(true);
            _spawnedIcons.Add(instance);
            
            var iconImage = instance.transform.Find("Weakness Icon").GetComponent<Image>();
            iconImage.sprite = icon;
        }
    }

    #region 事件

    public void OnEvent(EntityShieldChangedEvent evt)
    {
        if (evt.Target != _targetEntity) return;
        RefreshShield();
    }

    public void OnEvent(EntityWeaknessChangedEvent evt)
    {
        if (evt.Target != _targetEntity) return;
        RebuildWeaknessIcons();
    }
    
    public void OnEvent(EntityRecoverFromBreakEvent evt)
    {
        if(evt.Target != _targetEntity) return;
        RefreshShield();
    }

    #endregion
    
    
    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf == visible) return;
        gameObject.SetActive(visible);

        if (visible)
        {
            RefreshShield();
            RebuildWeaknessIcons();
        }
    }

    public void SetScreenPosition(Vector2 position)
    {
        ((RectTransform)transform).anchoredPosition = position;
    }
    
}
