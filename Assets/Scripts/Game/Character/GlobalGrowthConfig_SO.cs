
[CreateAssetMenu(menuName = "Game/GlobalGrowthConfig")]
public class GlobalGrowthConfig_SO : ScriptableObject
{
    [Header("成长曲线(X:等级1-99, Y:Multiplier)")]
    public AnimationCurve RankS = AnimationCurve.Linear(1, 1, 99, 4f);
    public AnimationCurve RankA = AnimationCurve.Linear(1, 1, 99, 3.5f);
    public AnimationCurve RankB = AnimationCurve.Linear(1, 1, 99, 3f);
    public AnimationCurve RankC = AnimationCurve.Linear(1, 1, 99, 2.5f);
    public AnimationCurve RankD = AnimationCurve.Linear(1, 1, 99, 2f);

    public AnimationCurve GetCurveByRank(E_GrowthRank rank)
    {
        return rank switch
        {
            E_GrowthRank.S => RankS,
            E_GrowthRank.A => RankA,
            E_GrowthRank.B => RankB,
            E_GrowthRank.C => RankC,
            E_GrowthRank.D => RankD,
            _ => RankB
        };
    }
}
