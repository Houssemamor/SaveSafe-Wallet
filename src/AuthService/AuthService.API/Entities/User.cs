using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthService.API.Entities;

[Table("users", Schema = "auth")]
public class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(255)]
    [Column("email")]
    public string Email { get; set; } = default!;

    [Required, MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = default!;

    [Required, MaxLength(60)]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = default!;

    [MaxLength(100)]
    [Column("google_id")]
    public string? GoogleId { get; set; }

    [Column("mfa_enabled")]
    public bool MfaEnabled { get; set; } = false;

    [Column("account_status")]
    public UserAccountStatus AccountStatus { get; set; } = UserAccountStatus.Active;

    [Column("role")]
    public UserRole Role { get; set; } = UserRole.User;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    public ICollection<LoginEvent> LoginEvents { get; set; } = new List<LoginEvent>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public enum UserAccountStatus { Active, Suspended, Deleted }
public enum UserRole { User, Admin }
