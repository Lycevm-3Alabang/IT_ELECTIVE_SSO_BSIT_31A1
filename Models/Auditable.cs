namespace Models;

public abstract class Auditable: IAuditable
{
    public bool IsActive{ get;set;} = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
