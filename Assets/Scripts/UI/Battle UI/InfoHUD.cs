using TMPro;
using UnityEngine.UI;

public class InfoHUD : MonoBehaviour
{
    #region 信息栏组件绑定

    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image characterImage;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text expText;
    [SerializeField] private Slider expSlider;

    [SerializeField, Min(0.01f)] private float expTweenDuration = 0.9f;

    #endregion

    #region 运行时缓存

    private Coroutine _expRoutine;
    private int _startLevel;
    private int _startExp;

    #endregion
    
    public void SetInfo(string displayName, int level, int currentExp, int targetExp, float expProgress01, Sprite portrait)
    {
        _startExp = currentExp;
        _startLevel = level;
        
        int shownTargetExp = targetExp > 0 ? targetExp : 1;

        if (portrait is not null)
            characterImage.sprite = portrait;
        nameText.text = displayName;
        levelText.text = "lv." + level.ToString();
        expText.text = $"{currentExp}/{targetExp}";
        expSlider.minValue = 0f;
        expSlider.maxValue = 1f;
        expSlider.value = Mathf.Clamp01(expProgress01);
    }

    public void PlayExpGainAnimation(CharacterRuntimeData member, int gainedExp, float delay = 0f)
    {
        // 1. 先停掉上一次残留的经验动画，避免同一个条目重复播
        StopExpRoutine();
        
        // 2. 本次没有真正写进经验条的经验就直接结束，不再启动协程
        if (gainedExp == 0)
            return;
        
        // 3. 这里只会收到队伍成员，所以直接读盟友成长配置
        AllyDefinition_SO allyDef = (AllyDefinition_SO)member.Definition;

        _expRoutine = StartCoroutine(CoPlayExpGainAnimation(member, allyDef, gainedExp, delay));
    }

    private IEnumerator CoPlayExpGainAnimation(CharacterRuntimeData member, AllyDefinition_SO allyDef, int gainedExp,
        float delay = 0f)
    {
        // 1. 如果需要错峰，就先等一小段时间再开始播
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float elapsed = 0f;
        int lastAppliedExp = -1;
        
        // 2. 按持续时间逐帧推进经验显示
        while (elapsed < expTweenDuration)
        {
            elapsed += Time.deltaTime;
            int appliedExp = Mathf.RoundToInt(gainedExp * Mathf.Clamp01(elapsed / expTweenDuration));
            if (appliedExp != lastAppliedExp)
            {
                BuildExpPreview(allyDef, _startLevel, _startExp, appliedExp,
                    out int level, out int exp, out int targetExp, out float progress);
                
                levelText.text = "lv." + level.ToString();
                expText.text = $"{exp}/{targetExp}";
                expSlider.value = progress;
                lastAppliedExp = appliedExp;
            }
            yield return null;
        }

        SetInfo(member.Definition.Name, member.Level, member.CurrentExp, member.GetExpRequiredToNextLevel(),
            member.GetExpProgress01(), member.Definition.Portrait);
        _expRoutine = null;
    }

    private void BuildExpPreview(AllyDefinition_SO allyDef, int startLevel, int startExp, int gainedExp, 
        out int level, out int exp, out int targetExp, out float progress)
    {
        // 1. 先从结算开始时的等级和经验作为基准。
        level = startLevel;
        exp = startExp;

        // 2. remaining 表示“这次动画还没有演出来的经验值”。
        // 后面会按升级门槛一点一点吃掉它。
        int remaining = gainedExp;

        // 3. 只要还有经验没分配，就持续推进预览结果。
        while (remaining > 0)
        {
            // 4. 先查当前等级升到下一级需要多少经验。
            targetExp = allyDef.GetExpRequiredToNextLevel(level);
            if (targetExp == 0)
            {
                // 5. 满级后直接停在 0 / 1 的满进度状态。
                exp = 0;
                progress = 1f;
                return;
            }

            // 6. 计算当前等级还差多少经验就能升级。
            int need = targetExp - exp;
            if (need <= 0)
            {
                // 7. 如果当前经验已经够升级，先把等级抬上去，
                // 再把经验条重置到新等级的起点继续算。
                level++;
                exp = 0;
                continue;
            }

            // 8. 本轮先吃掉“距离升级还差的经验”和“剩余待结算经验”中的较小值。
            int take = Mathf.Min(need, remaining);
            exp += take;
            remaining -= take;

            // 9. 如果刚好吃满当前等级，就进入下一等级继续结算。
            if (exp >= targetExp)
            {
                level++;
                exp = 0;
            }
        }

        // 10.所有经验都分配完后，再取一次当前等级的升级门槛。
        targetExp = allyDef.GetExpRequiredToNextLevel(level);
        if (targetExp == 0)
        {
            // 11. 如果结算后已经是满级，进度条直接拉满。
            progress = 1f;
            return;
        }

        // 12. 最终进度就是当前经验占升级需求的比例。
        progress = exp / (float)targetExp;
    }

    private void StopExpRoutine()
    {
        if (_expRoutine == null)
            return;
        
        StopCoroutine(_expRoutine);
        _expRoutine = null;
    }

}
