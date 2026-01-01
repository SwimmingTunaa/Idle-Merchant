/// <summary>
/// Represents a stat query that passes through the modifier chain.
/// Modifiers transform the Value as it passes through.
/// </summary>
public class Query
{
    public readonly StatType StatType;
    public float Value;

    public Query(StatType statType, float value)
    {
        StatType = statType;
        Value = value;
    }
}