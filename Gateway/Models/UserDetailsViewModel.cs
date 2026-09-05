namespace Gateway.Areas.Admin.Models;

public class UserDetailsViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<UserGroupInfo> Groups { get; set; } = new();
}

public class UserGroupInfo
{
    public string AppName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int? Level { get; set; }
}