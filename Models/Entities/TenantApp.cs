using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Entities;

[Table("TenantApps")]
public class TenantApp : Auditable
{
    public int Id { get; set; }
    public string? Name{get;set;}
    public string? ReturnUrl{get;set;}

    public ICollection<Group> Groups { get; set; } = [];
 
}
