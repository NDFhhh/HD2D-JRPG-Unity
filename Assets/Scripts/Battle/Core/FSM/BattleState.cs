public abstract class BattleState
{
    protected readonly BattleController _controller;

    public BattleState(BattleController controller)
    {
        _controller = controller;
    }

    public virtual IEnumerator Enter()
    {
        yield break;
    }
    
    public abstract IEnumerator Execute();
    
    public virtual IEnumerator Exit()
    {
        yield break;
    }
    
}
