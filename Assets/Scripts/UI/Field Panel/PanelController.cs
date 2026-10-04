using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PanelController : MonoBehaviour
{
    [FormerlySerializedAs("CurrrentAction")] [Header("Action")]
    public ActionBase CurrentAction;

    [Header("Focus Navigation")]
    public Button FirstButton;
    
    [Header("Action Icon")]
    [SerializeField] private Image actionIcon;

    public virtual Type PanelActionType => null;

    public virtual void SetupPanel(ActionBase actionBase)
    {
        CurrentAction = actionBase;
        
        if (actionIcon is not null)
            actionIcon.sprite = actionBase.CommandInfo.Icon;
    }

    public virtual void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    protected virtual void OnCancel()
    {
        GameModeManager.Instance.RequestChangeMode(E_GameMode.Explore);
        ClosePanel();
    }

    protected virtual void OnConfirm()
    {
        CurrentAction.Execute();
        ClosePanel();
    }

    /// <summary>
    /// 面板自己关闭的逻辑
    /// </summary>
    /// <returns></returns>
    public virtual bool HandleCancelInput()
    {
        return false;
    }
    
    /// <summary>
    /// 设置默认选中的UI元素
    /// 此方法用于在界面初始化时自动选择第一个按钮
    /// </summary>
    public void SetDefaultSelection()
    {
        FirstButton.Select();
        EventSystem.current.SetSelectedGameObject(FirstButton.gameObject);
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    protected void RebindButtons(Button button,UnityAction unityAction)
    {
        if (button is null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(unityAction);
    }
}
