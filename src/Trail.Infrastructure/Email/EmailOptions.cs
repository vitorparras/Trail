namespace Trail.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email:Smtp";

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "noreply@trail.local";

    public string FromName { get; set; } = "Trail";

    public bool UseSsl { get; set; }

    public bool UseStartTls { get; set; }
}
