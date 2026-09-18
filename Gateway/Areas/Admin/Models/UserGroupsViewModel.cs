namespace Gateway.Areas.Admin.Models;

public class UserGroupsViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<AssignedGroupInfo> AssignedGroups { get; set; } = new();
    public List<AvailableGroupInfo> AvailableGroups { get; set; } = new();
}

public class AssignedGroupInfo
{
    public int GroupId { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int? Level { get; set; }
}

public class AvailableGroupInfo
{
    public int GroupId { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
}
