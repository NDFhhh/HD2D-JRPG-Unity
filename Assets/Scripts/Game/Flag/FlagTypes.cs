using System;

/// <summary>
/// 全局变量（Flag）的条件与写入操作类型。
///
/// 原先定义在 <c>DialogueAsset_SO.cs</c> 里、与对话节点图绑在一起。
/// 对话改为 Yarn 驱动后，Flag 体系依然是**对话 / 任务 / 剧情三个系统共用的基础设施**，
/// 因此独立成文件保留（任务系统的 QuestObjective / UnlockConditions 复用这些类型）。
/// </summary>

/// <summary>Flag 比较运算符</summary>
public enum E_CompareOp
{
    Equal,
    NotEqual,
    Greater,
    Less,
    GreaterOrEqual,
    LessOrEqual
}

/// <summary>Flag 写入操作类型</summary>
public enum E_FlagOp
{
    Set,
    Add,
    Toggle
}

/// <summary>全局变量条件：读取 GameFlagManager 并比较</summary>
[Serializable]
public class FlagCondition
{
    public string Key;
    public E_CompareOp Op = E_CompareOp.Equal;
    public int Value;

    public bool Evaluate()
    {
        int current = GameFlagManager.Instance is null ? 0 : GameFlagManager.Instance.Get(Key);
        return Op switch
        {
            E_CompareOp.Equal => current == Value,
            E_CompareOp.NotEqual => current != Value,
            E_CompareOp.Greater => current > Value,
            E_CompareOp.Less => current < Value,
            E_CompareOp.GreaterOrEqual => current >= Value,
            E_CompareOp.LessOrEqual => current <= Value,
            _ => false
        };
    }
}

/// <summary>全局变量写入操作</summary>
[Serializable]
public class FlagOperation
{
    public string Key;
    public E_FlagOp Op = E_FlagOp.Set;
    public int Value;

    public void Apply()
    {
        GameFlagManager manager = GameFlagManager.EnsureInstance();
        switch (Op)
        {
            case E_FlagOp.Set:
                manager.Set(Key, Value);
                break;
            case E_FlagOp.Add:
                manager.Add(Key, Value);
                break;
            case E_FlagOp.Toggle:
                manager.Toggle(Key);
                break;
        }
    }
}
