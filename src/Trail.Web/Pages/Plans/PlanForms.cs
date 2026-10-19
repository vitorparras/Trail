using Trail.Application.Planning;
using Trail.Web.Results;

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

public static class PlanFormErrors
{
    public const string NameField = "Name";
    public const string TitleField = "Title";
    public const string CodeField = "TopicCode";
    public const string ParentField = "ParentId";
    public const string MarkdownField = "Markdown";
}

public static class PlanFormFields
{
    public const string InputPrefix = "Input";

    public static readonly FieldTargetMap Plan = FieldTargetMap.Empty
        .Prefixed(InputPrefix, "Name", "Description", "DefaultMockExamQuestions")
        .Field("LowThreshold", $"{InputPrefix}.LowBandThreshold")
        .Field("HighThreshold", $"{InputPrefix}.HighBandThreshold")
        .Field("Days", $"{InputPrefix}.PeriodDays")
        .Field("EndDate", $"{InputPrefix}.PeriodEndDate")
        .Field("Kind", $"{InputPrefix}.PeriodKind");
}
