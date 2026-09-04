using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Entities;

[Table("AuditLogs")]
public class AuditLog
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string? Action { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}