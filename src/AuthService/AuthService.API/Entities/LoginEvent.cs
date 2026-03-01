using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthService.API.Entities;

[Table("login_events", Schema = "auth")]
public class LoginEvent
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [MaxLength(45)]
    [Column("ip")]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [MaxLength(100)]
    [Column("country")]
    public string? Country { get; set; }

    [Column("success")]
    public bool Success { get; set; }

    [MaxLength(200)]
    [Column("failure_reason")]
    public string? FailureReason { get; set; }

    [Column("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Column("is_flagged")]
    public bool IsFlagged { get; set; } = false;

    [ForeignKey("UserId")]
    public User User { get; set; } = default!;
}
