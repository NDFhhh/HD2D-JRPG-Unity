using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 破盾瞬间的全屏闪屏。
///
/// 工程已切到 Built-in 渲染管线，原来依赖 URP Volume 的那套后处理（Bloom / 暗角 / 景深 /
/// 色差 / 镜头畸变）已经不可用，这里改为纯 UI 覆盖层实现：
/// 不依赖任何渲染管线包，也不需要美术在场景里手工搭节点 —— 没手动指定覆盖层时，
/// 会在 Awake 里自动建一个铺满屏幕的 Image。
///
/// 对外接口保持原样（只暴露 Play()），所以 BattleCinematicService 不需要改动。
/// </summary>
public class BattleHitPostFX : MonoBehaviour
{
    // 覆盖层层级：拉到最高，保证盖住所有 HUD
    private const int OverlaySortingOrder = 32000;

    #region 序列化参数与运行时状态

    [Header("Flash")]
    [SerializeField, Tooltip("闪屏颜色，alpha 就是最大不透明度。破盾一般用白色")]
    private Color flashColor = new Color(1f, 1f, 1f, 0.85f);

    [Header("Intensity")]
    [SerializeField, Range(0f, 1f), Tooltip("整体强度系数，会乘到颜色的 alpha 上")]
    private float flashWeight = 1f;

    [Min(0f)]
    [SerializeField, Tooltip("闪屏保持时长（真实时间，不受慢动作影响），0 表示当帧闪一下")]
    private float flashDuration = 0.1f;

    [SerializeField, Tooltip("空闲时关掉覆盖节点，省一点 overdraw")]
    private bool disableWhenIdle = true;

    [SerializeField, Tooltip("可选：手动指定覆盖层 Image。留空则运行时自动创建")]
    private Image flashOverlay;

    // 自动创建出来的覆盖层节点（手动指定 flashOverlay 时为 null，不归我们销毁）
    private GameObject _createdOverlay;

    // 当前正在播放的短闪流程
    // 这样连续破盾时，可以先停止上一条再重新闪一次
    private Coroutine _playRoutine;

    #endregion

    #region 生命周期

    private void Awake()
    {
        ResolveOverlay();
        ResetFlash();
    }

    private void OnDestroy()
    {
        // 只销毁自己建的那个，手动指定的节点归场景管
        if (_createdOverlay != null)
            Destroy(_createdOverlay);
    }

    #endregion

    #region 对外接口

    /// <summary>播放一次破盾闪屏</summary>
    public void Play()
    {
        // 1. 先停掉上一段，避免连续破盾时把停留时间拖长
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        ResetFlash();

        // 2. 直接把覆盖层顶到目标不透明度
        ApplyAlpha(flashWeight * flashColor.a);

        // 3. 保持一个极短时间后立刻关掉，画面上就是 "闪一下"
        if (flashDuration <= 0f)
        {
            ResetFlash();
            return;
        }

        _playRoutine = StartCoroutine(FlashOnce());
    }

    #endregion

    #region 覆盖层

    /// <summary>解析覆盖层：优先用手动指定的，没有就自动建一个全屏 Image</summary>
    private void ResolveOverlay()
    {
        if (flashOverlay == null)
            flashOverlay = CreateOverlay();
    }

    private Image CreateOverlay()
    {
        // 必须是根节点：Canvas 的 renderMode 只在根 Canvas 上生效，
        // 挂到别的 Canvas 下面会变成嵌套 Canvas，渲染模式就不受控了
        _createdOverlay = new GameObject("BattleHitPostFX Overlay", typeof(RectTransform));

        Canvas canvas = _createdOverlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlaySortingOrder;

        // Image 不挂 sprite 时就是一个纯色四边形，正好当全屏色块用；
        // raycastTarget 关掉，不能挡住底下的 UI
        Image image = _createdOverlay.AddComponent<Image>();
        image.raycastTarget = false;

        RectTransform rect = (RectTransform)_createdOverlay.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        SetIdle();
        return image;
    }

    private void ApplyAlpha(float alpha)
    {
        if (flashOverlay == null)
            return;

        alpha = Mathf.Clamp01(alpha);

        if (alpha > 0f)
        {
            if (_createdOverlay != null)
                _createdOverlay.SetActive(true);
            flashOverlay.enabled = true;
        }

        Color c = flashColor;
        c.a = alpha;
        flashOverlay.color = c;

        if (alpha <= 0f)
            SetIdle();
    }

    private void ResetFlash()
    {
        ApplyAlpha(0f);
    }

    private void SetIdle()
    {
        if (flashOverlay != null)
            flashOverlay.enabled = false;
        if (_createdOverlay != null)
            _createdOverlay.SetActive(false);
    }

    #endregion

    #region 播放流程

    private IEnumerator FlashOnce()
    {
        // 用真实时间等待：破盾演出里同时有慢动作，不能用受 timeScale 影响的等待
        yield return new WaitForSecondsRealtime(flashDuration);
        ResetFlash();
        _playRoutine = null;
    }

    #endregion
}
