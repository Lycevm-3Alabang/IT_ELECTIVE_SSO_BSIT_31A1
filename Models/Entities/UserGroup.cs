using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Entities;

// Composite key is (UserId, GroupId) — a user can hold many rows here,
// including groups that belong to different TenantApps. Do not add a
// unique index on UserId alone; that would cap a user at one group total.
[Table("UserGroups")]
public class UserGroup
{
    public string? UserId { get; set; }

    public int GroupId { get; set; }
    public Group? Group { get; set; }
}