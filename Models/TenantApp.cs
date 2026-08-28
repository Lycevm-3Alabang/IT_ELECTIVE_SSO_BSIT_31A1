using System.ComponentModel.DataAnnotations.Schema;

namespace Models;

[Table("TenantApps")]
public class TenantApp
{
    public int Id { get; set; }
    public string? Name{get;set;}
    public string? ReturnUrl{get;set;}
    public bool IsActive{ get;set;} = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    
}
