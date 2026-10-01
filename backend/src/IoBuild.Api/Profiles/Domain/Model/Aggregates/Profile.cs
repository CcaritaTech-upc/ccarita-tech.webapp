namespace IoBuild.Api.Profiles.Domain.Model.Aggregates;

/// <summary>
/// Profiles BC aggregate: Profile.
/// Location: Profiles/Domain/Model/Aggregates/Profile.cs
/// Namespace preserved for compatibility.
/// </summary>
public sealed class Profile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? SecondEmail { get; set; }
    public int? Age { get; set; }
    public int? YearsInBusiness { get; set; }
    public string? PhotoReference { get; set; }
    public string? CloudinaryReference { get; set; }
    public string? PhotoUrl { get; set; }
}
