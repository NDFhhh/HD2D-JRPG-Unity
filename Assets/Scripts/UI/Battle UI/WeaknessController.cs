using System.Numerics;
using MFramework.Event;
using Vector2 = UnityEngine.Vector2;

public class WeaknessController : MonoBehaviour,
    IEventReceiver<BattleStartedEvent>,
    IEventReceiver<BattleEndedEvent>
{
    [Header("Prefab")]
    [SerializeField] private WeaknessBar weaknessBarPrefab;
    [SerializeField] private DamageTypeIconSet_SO damageTypeIconSet;

    [Header("Follow")] 
    [SerializeField] private RectTransform containerRoot;
    [SerializeField] private Vector2 screenOffset =  Vector2.zero;
    
    private readonly Dictionary<BattleEntity, WeaknessBar> _barByEntity = new();

    private void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(this);
        EventBus.Subscribe<BattleEndedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(this);
        EventBus.Unsubscribe<BattleEndedEvent>(this);
        
        ClearBars();
    }

    private void LateUpdate()
    {
        if (_barByEntity.Count == 0)
            return;

        foreach (var kv in _barByEntity)
        {
            BattleEntity entity = kv.Key;
            WeaknessBar bar = kv.Value;

            if (!entity.IsAlive)
            {
                bar.SetVisible(false);
                continue;
            }

            var screenPos = Camera.main.WorldToScreenPoint(entity.Unit.transform.position);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(containerRoot, screenPos, null,
                out Vector2 localPos);
            
            bar.SetVisible(true);
            bar.SetScreenPosition(localPos + screenOffset);
        }
    }

    private void RebuildBars()
    {
        ClearBars();
        BattleUnit[] allUnits = FindObjectsOfType<BattleUnit>(true);

        for (int i = 0; i < allUnits.Length; i++)
        {
            BattleUnit unit = allUnits[i];
            if (unit.Entity.IsPlayer)
                continue;

            WeaknessBar bar = Instantiate(weaknessBarPrefab, containerRoot);
            bar.Setup(unit.Entity, damageTypeIconSet);

            _barByEntity[unit.Entity] = bar;
        }
    }

    private void ClearBars()
    {
        foreach (WeaknessBar bar in _barByEntity.Values)
        {
            Destroy(bar.gameObject);
        }
        _barByEntity.Clear();
    }

    public void OnEvent(BattleStartedEvent evt)
    {
        RebuildBars();
    }

    public void OnEvent(BattleEndedEvent evt)
    {
        ClearBars();
    }
}
