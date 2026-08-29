using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Entities;

[Table("UserGroups")]
public class UserGroup
{
    public string? UserId { get; set; }
 

    public int GroupId { get; set; }
    public Group? Group { get; set; }
}