using System.ComponentModel.DataAnnotations.Schema;

namespace Models;

[Table("TenantApps")]
public class TenantApp
{
    public int Id { get; set; }
}

public class userGroup
{
    public int Id { get; set; }
}

public class Endpoints
{
    public int Id { get; set; }
}
