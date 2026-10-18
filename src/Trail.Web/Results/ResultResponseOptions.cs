namespace Trail.Web.Results;

public sealed class ResultResponseOptions
{
    public string? PartialViewName { get; set; }

    public object? PartialModel { get; set; }

    public string? FormPartialViewName { get; set; }

    public object? FormPartialModel { get; set; }

    public string? RedirectPage { get; set; }

    public object? RedirectRouteValues { get; set; }

    public bool RedirectGetRequests { get; set; }

    public FieldTargetMap? Fields { get; set; }
}

public sealed class ResultResponseOptions<T>
    where T : notnull
{
    public string? PartialViewName { get; set; }

    public Func<T, object>? PartialModelFactory { get; set; }

    public string? FormPartialViewName { get; set; }

    public object? FormPartialModel { get; set; }

    public string? RedirectPage { get; set; }

    public Func<T, object>? RedirectRouteValuesFactory { get; set; }

    public object? RedirectRouteValues { get; set; }

    public bool RedirectGetRequests { get; set; }

    public FieldTargetMap? Fields { get; set; }
}
