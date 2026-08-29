namespace Models;

public interface IAuditable
{
     bool IsActive{ get;set;} 
     DateTime CreatedAt { get; set; } 
     DateTime UpdatedAt { get; set; } 
}
