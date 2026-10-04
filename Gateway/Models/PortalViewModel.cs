namespace Gateway.Models;

public class PortalViewModel
{
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public List<PortalApp> Apps { get; set; } = new();
}

public class PortalApp
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Groups { get; set; } = new();
}