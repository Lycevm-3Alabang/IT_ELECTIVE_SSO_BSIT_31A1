namespace Gateway.Areas.Admin.Models;

public class DashboardViewModel
{
    public int TotalUsers { get; set; }
    public int NewUsersThisMonth { get; set; }

    public int TotalGroups { get; set; }
    public int NewGroupsThisMonth { get; set; }

    public int TotalApplications { get; set; }
    public int NewApplicationsThisMonth { get; set; }

    public int ActiveUsers { get; set; }
    public int NewlyActivatedThisMonth { get; set; }

    public List<RecentActivityItem> RecentActivities { get; set; } = new();

    public List<string> ActivityChartLabels { get; set; } = new();
    public List<int> LoginAttemptsSeries { get; set; } = new();
    public List<int> PasswordResetsSeries { get; set; } = new();
    public List<int> UsersCreatedSeries { get; set; } = new();
    public List<int> UsersDeactivatedSeries { get; set; } = new();

    public List<NamedCount> TopGroups { get; set; } = new();

    public int ActiveUserCount { get; set; }
    public int InactiveUserCount { get; set; }

    public List<NamedCount> TopApplications { get; set; } = new();
}

public class RecentActivityItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string IconType { get; set; } = "default";
}

public class NamedCount
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}