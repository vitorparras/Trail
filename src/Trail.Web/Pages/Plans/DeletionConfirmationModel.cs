using Trail.Application.Planning;

namespace Trail.Web.Pages.Plans;

public sealed record DeletionConfirmationModel(
    DeletionImpactView Impact,
    string Page,
    string Handler,
    string TargetField,
    Guid PlanId,
    Guid? SubjectId,
    string DescriptionId)
{
    public const string PartialViewName = "_DeletionConfirmation";
    public const string SubjectDialogDescriptionId = "delete-subject-description";
    public const string SubjectSectionDescriptionId = "subject-deletion-description";
    public const string TopicDialogDescriptionId = "delete-topic-description";
    public const string TopicSectionDescriptionId = "topic-deletion-description";

    public static DeletionConfirmationModel ForSubject(
        DeletionImpactView impact,
        Guid planId,
        string descriptionId = SubjectDialogDescriptionId) =>
        new(impact, "/Plans/Details", "DeleteSubject", "subjectId", planId, null, descriptionId);

    public static DeletionConfirmationModel ForTopic(
        DeletionImpactView impact,
        Guid planId,
        Guid subjectId,
        string descriptionId = TopicDialogDescriptionId) =>
        new(impact, "/Plans/Subject", "DeleteTopic", "topicId", planId, subjectId, descriptionId);
}
