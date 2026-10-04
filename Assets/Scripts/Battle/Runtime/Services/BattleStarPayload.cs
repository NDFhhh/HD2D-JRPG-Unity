public class BattleStartPayload
{
    public List<CharacterRuntimeData> Allies { get; }
    public List<CharacterRuntimeData> Enemies { get; }
    public E_EnemyLayoutFormation Formation { get; }

    public BattleStartPayload(List<CharacterRuntimeData> allies, List<CharacterRuntimeData> enemies,
        E_EnemyLayoutFormation formation)
    {
        Allies = allies;
        Enemies = enemies;
        Formation = formation;
    }
}
