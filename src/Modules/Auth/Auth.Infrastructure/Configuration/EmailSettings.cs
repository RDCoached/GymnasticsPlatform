namespace Auth.Infrastructure.Configuration;

public sealed class EmailSettings
{
    public string FromEmail { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
}
