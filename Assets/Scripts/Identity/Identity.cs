using System;

/// <summary>
/// Gender for identity generation.
/// </summary>
public enum Gender
{
    Male,
    Female
}

/// <summary>
/// Immutable identity for an adventurer.
/// Generated once on hiring, persists forever.
/// </summary>
[Serializable]
public sealed class Identity
{
    public Gender gender;
    public string firstName;
    public string epithet; // Can be null/empty for units with no traits
    public string description;
    public int appearanceSeed;
    
    public string DisplayName => string.IsNullOrEmpty(epithet) 
        ? firstName 
        : $"{firstName} {epithet}";
    
    public Identity(Gender gender, string firstName, string epithet, string description, int appearanceSeed)
    {
        this.gender = gender;
        this.firstName = firstName;
        this.epithet = epithet;
        this.description = description;
        this.appearanceSeed = appearanceSeed;
    }
}