namespace Trail.Domain.Common;

public sealed record Message
{
    public Message(string code, MessageSeverity severity, string? target = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (target is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(target);
        }

        Code = code;
        Severity = severity;
        Target = target;
    }

    public string Code { get; }

    public MessageSeverity Severity { get; }

    public string? Target { get; }

    public static Message Error(string code, string? target = null) => new(code, MessageSeverity.Error, target);

    public static Message Warning(string code, string? target = null) => new(code, MessageSeverity.Warning, target);

    public static Message Info(string code, string? target = null) => new(code, MessageSeverity.Info, target);

    public static Message Success(string code, string? target = null) => new(code, MessageSeverity.Success, target);
}
