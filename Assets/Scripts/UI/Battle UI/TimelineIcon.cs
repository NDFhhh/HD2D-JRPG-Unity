using DG.Tweening;
using UnityEngine.UI;

public class TimelineIcon : MonoBehaviour
{
    [SerializeField] private Image portraitImage;
    [SerializeField] private Image borderImage;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Sprite allyFrame;
    [SerializeField] private Sprite enemyFrame;

    private Vector3 _visualInitPos;
    
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 1f;
        _visualInitPos = visualRoot.localPosition;
    }

    public void Setup(BattleEntity entity)
    {
        portraitImage.sprite = entity.RuntimeData.Definition.Portrait;
        borderImage.sprite = entity.IsPlayer ? allyFrame : enemyFrame;
    }

    #region 动画入口

    public void PlayEntranceAnimation(float duration, float offsetX, Ease ease)
    {
        StopVisualTweens();
        
        _canvasGroup.alpha = 0f;
        visualRoot.localPosition = _visualInitPos + new Vector3(offsetX, 0f, 0f);
        visualRoot.DOLocalMoveX(_visualInitPos.x, duration).SetEase(ease);
        _canvasGroup.DOFade(1f, duration).SetEase(ease);
    }

    public void PlayExitAnimation(float duration, float offsetX, Ease ease)
    {
        StopVisualTweens();
        
        visualRoot.DOLocalMove(_visualInitPos + new Vector3(offsetX, 0f, 0f), duration).SetEase(ease);
        _canvasGroup.DOFade(0f, duration).SetEase(ease);
    }

    private void StopVisualTweens()
    {
        visualRoot.DOKill();
        _canvasGroup.DOKill();
    }

    #endregion
    
}
