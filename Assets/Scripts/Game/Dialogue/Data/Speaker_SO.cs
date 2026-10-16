/// <summary>
/// 对话说话人定义：姓名、姓名牌颜色、可选立绘、可选打字机音效。
/// 通过 CreateAssetMenu 创建：右键 → Create → CodePath → Dialogue → Speaker
/// </summary>
[CreateAssetMenu(fileName = "Speaker_", menuName = "CodePath/Dialogue/Speaker")]
public class Speaker_SO : ScriptableObject
{
    [Header("显示信息")]
    [Tooltip("Yarn 脚本里的说话人标识（即 CharacterName，如 traveller）。留空则回落到资产文件名")]
    public string Id;
    public string SpeakerName = "???";

    [Tooltip("姓名牌文字颜色，默认为歧路旅人风格的金色")]
    public Color NameColor = new(0.91f, 0.79f, 0.48f, 1f);

    [Header("可选表现")]
    [Tooltip("可选。设置后显示在对话框左侧，正文自动右移")]
    public Sprite Portrait;

    [Tooltip("可选。打字机逐字播放的音效")]
    public AudioClip VoiceBlip;
}
