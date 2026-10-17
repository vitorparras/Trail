using Trail.Domain.Common;

namespace Trail.Web.Toasts;

public sealed record Toast(MessageSeverity Severity, string Text)
{
    public bool IsPersistent => Severity is MessageSeverity.Error or MessageSeverity.Warning;
}
