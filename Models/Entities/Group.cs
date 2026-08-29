using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Entities;

public class Group : Auditable
{
    public int Id{get;set;}
    public string? Name{get;set;}
    public int? Level {get;set;} = 99;

    public int TenantAppId { get; set; }

    
    public TenantApp TenantApp { get; set; } = null!;
    public ICollection<UserGroup> UserGroups { get; set; } = [];
}
