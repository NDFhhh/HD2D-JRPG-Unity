
using Random = UnityEngine.Random;

public class StealAction : ActionBase
{
    [Header("Steal Action")]
    public List<InventoryItem> itemsToSteal;

    public override void TriggerAction(AllyDefinition_SO interactor)
    {
        EventBus.Publish(new PanelRequestEvent(this));
    }

    public bool TrySteal(ItemDefinition_SO itemDefinition)
    {
        bool success = Random.value <= Mathf.Clamp01(itemDefinition.RarityWeight / 100f);

        if (success)
        {
            InventoryManager.Instance.AddItem(itemDefinition,1);
            for (int i = 0; i < itemsToSteal.Count; i++)
            {
                if (itemsToSteal[i].ItemDefinition != itemDefinition)
                    continue;
                itemsToSteal.RemoveAt(i);
            }
        }
        
        return success;
    }
    
    public override bool CanShow(AllyDefinition_SO interactor)
    {
        if (itemsToSteal.Count == 0)
        {
            return false;
        }

        
        return base.CanShow(interactor);
    }
}
