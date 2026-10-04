
public abstract class ActionBase : MonoBehaviour
{
    public E_PlayerJob MatchJob = E_PlayerJob.Any;
    
    public ActionCommandInfo CommandInfo;
    
    public virtual bool CanShow(AllyDefinition_SO interactor)
    {
        return IsJobMatch(interactor);
    }

    public virtual bool CanExecute(AllyDefinition_SO interactor)
    {
        return true;
    }

    public virtual void TriggerAction(AllyDefinition_SO interactor)
    {
        // 需要打开面板再操作的就重写
        // 默认执行命令
        Execute();
    }

    public virtual void Execute() { }

    protected virtual bool IsJobMatch(AllyDefinition_SO interactor)
    {
        if (MatchJob == E_PlayerJob.Any)
            return true;
        
        return interactor.Job == MatchJob;
    }
    
}

[System.Serializable]
public struct ActionCommandInfo
{
    public string DisplayName;
    public Sprite Icon;
    public int Order;
}