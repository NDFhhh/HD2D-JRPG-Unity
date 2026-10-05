using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 开场背景动画视频控制器。
/// 把 VideoPlayer 的画面渲染到铺满屏幕的 RawImage 上，作为主菜单背景循环播放。
/// 挂在带 VideoPlayer + RawImage 的同一物体上（或手动引用）。
/// </summary>
public class MenuVideoBackground : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoImage;

    [Header("播放设置")]
    [SerializeField] private bool playOnAwake = true;
    [SerializeField] private bool loop = true;

    private void Awake()
    {
        // 若未手动引用，自动从自身/子物体查找
        if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
        if (videoImage == null) videoImage = GetComponent<RawImage>();

        if (videoPlayer == null || videoImage == null)
        {
            Debug.LogError("[MenuVideoBackground] 缺少 VideoPlayer 或 RawImage 引用，请检查挂载。", this);
            return;
        }

        // RenderTexture 引用兜底：若一边引用丢失，自动接上；两边都丢则运行时创建，
        // 保证即使 RT 资源引用断开，视频背景也能正常显示
        if (videoPlayer.targetTexture == null && videoImage.texture is RenderTexture existing)
        {
            videoPlayer.targetTexture = existing;
        }
        if (videoPlayer.targetTexture == null)
        {
            var runtimeRt = new RenderTexture(1920, 1080, 0) { name = "MenuVideoRT_Runtime" };
            videoPlayer.targetTexture = runtimeRt;
        }
        videoImage.texture = videoPlayer.targetTexture;

        // 默认隐藏画面，等真正开始播放再显示，避免首帧黑屏/花屏
        videoImage.enabled = false;

        videoPlayer.isLooping = loop;
        videoPlayer.playOnAwake = false; // 统一由本脚本接管播放时机
        videoPlayer.prepareCompleted += OnPrepared;
    }

    private void Start()
    {
        if (videoPlayer == null) return;

        if (playOnAwake)
        {
            videoPlayer.Prepare();
        }
    }

    private void OnPrepared(VideoPlayer player)
    {
        videoImage.enabled = true;
        player.Play();
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnPrepared;
        }
    }

    /// <summary>手动开始播放（供外部调用，例如需要先做其他初始化时）。</summary>
    public void PlayVideo()
    {
        if (videoPlayer == null) return;
        if (!videoPlayer.isPrepared)
        {
            videoPlayer.Prepare();
            return;
        }
        videoImage.enabled = true;
        videoPlayer.Play();
    }

    /// <summary>停止播放并隐藏画面。</summary>
    public void StopVideo()
    {
        if (videoPlayer == null) return;
        videoPlayer.Stop();
        if (videoImage != null) videoImage.enabled = false;
    }
}
