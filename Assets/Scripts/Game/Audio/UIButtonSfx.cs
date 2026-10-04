using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI 按钮音效组件：挂到任意 Button 上即可获得点击 / 悬停音效
/// （场景中需存在 AudioManager，否则静默跳过）
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButtonSfx : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private E_SoundId clickSound = E_SoundId.UI_Click;
    [Tooltip("悬停音效，None 表示不播")]
    [SerializeField] private E_SoundId hoverSound = E_SoundId.None;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(PlayClickSound);
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(PlayClickSound);
    }

    private void PlayClickSound()
    {
        if (clickSound != E_SoundId.None)
            AudioManager.PlaySfx(clickSound);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverSound != E_SoundId.None)
            AudioManager.PlaySfx(hoverSound, 0.6f);
    }
}
