using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Trail.Web.Forms;

public sealed record FormErrorList(string Id, IReadOnlyList<string> Errors);

public static class FormErrors
{
    public static IReadOnlyList<string> For(ModelStateDictionary modelState, bool show, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        return ForFieldsOnly(modelState, show, [.. fields, string.Empty]);
    }

    public static IReadOnlyList<string> ForFieldsOnly(ModelStateDictionary modelState, bool show, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(modelState);
        ArgumentNullException.ThrowIfNull(fields);

        if (!show)
        {
            return [];
        }

        return fields
            .SelectMany(field => ErrorsOf(modelState, field))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static bool HasErrors(ModelStateDictionary modelState, bool show, string field)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        return show && ErrorsOf(modelState, field).Any();
    }

    private static IEnumerable<string> ErrorsOf(ModelStateDictionary modelState, string field) =>
        modelState.TryGetValue(field, out var entry)
            ? entry.Errors.Select(error => error.ErrorMessage)
            : [];
}
