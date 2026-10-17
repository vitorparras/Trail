using Microsoft.AspNetCore.Mvc.ModelBinding;
using Trail.Application.Planning;

namespace Trail.Web.Pages.Plans;

public sealed record AddSectionFormModel(Guid PlanId, int PlanVersion, string? Name, bool ShowErrors);

public sealed record AddSubjectFormModel(
    Guid PlanId,
    int PlanVersion,
    IReadOnlyList<SectionView> Sections,
    string? Name,
    Guid? SectionId,
    bool ShowErrors);

public sealed record SectionNameFormModel(Guid PlanId, int PlanVersion, Guid SectionId, string Name, bool ShowErrors);

public sealed record SubjectNameFormModel(Guid PlanId, Guid SubjectId, int SubjectVersion, string Name, bool ShowErrors);

public sealed record AddTopicFormModel(
    Guid PlanId,
    Guid SubjectId,
    int SubjectVersion,
    IReadOnlyList<TopicTreeItemView> Topics,
    string? Code,
    string? Title,
    Guid? ParentId,
    bool ShowErrors);

public sealed record UpdateTopicFormModel(
    Guid PlanId,
    Guid SubjectId,
    int SubjectVersion,
    IReadOnlyList<TopicTreeItemView> Topics,
    Guid? TopicId,
    string? Code,
    string? Title,
    bool ShowErrors);

public sealed record MoveTopicFormModel(
    Guid PlanId,
    Guid SubjectId,
    int SubjectVersion,
    IReadOnlyList<TopicTreeItemView> Topics,
    Guid? TopicId,
    Guid? NewParentId,
    bool ShowErrors);

public sealed record FormErrorList(string Id, IReadOnlyList<string> Errors);

public static class PlanFormErrors
{
    public const string NameField = "Name";
    public const string TitleField = "Title";
    public const string CodeField = "TopicCode";
    public const string ParentField = "ParentId";
    public const string MarkdownField = "Markdown";

    public static IReadOnlyList<string> For(ModelStateDictionary modelState, bool show, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(modelState);
        ArgumentNullException.ThrowIfNull(fields);

        if (!show)
        {
            return [];
        }

        return fields
            .Append(string.Empty)
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
