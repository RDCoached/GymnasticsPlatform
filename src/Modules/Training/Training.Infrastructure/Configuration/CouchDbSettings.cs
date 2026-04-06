namespace Training.Infrastructure.Configuration;

public sealed class CouchDbSettings
{
    public string ServerUrl { get; set; } = "http://localhost:5984";
    public string DatabaseName { get; set; } = "programmes";
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "changeme";
}
