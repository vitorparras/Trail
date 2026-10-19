using System.Globalization;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Web.Tests.Performance;

internal static class PerformancePlanSeeder
{
    private const string NoteTemplate =
        "## Summary\n\nKey points for this topic, with the **main rules** and the *usual exceptions*.\n\n- First point to remember\n- Second point to remember\n- Third point to remember\n\n> Review before the next mock exam.\n";

    private static readonly string[] TitleStems =
    [
        "Fundamental principles of",
        "Legal framework for",
        "Interpretation and application of",
        "Common mistakes in",
        "Case law about",
        "Definitions and scope of",
    ];

    public static async Task<SeededPerformancePlan> SeedAsync(PerformanceSeedContext context, PlanShape shape, string planName)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(shape);

        var owner = context.OwnerId;
        var plan = StudyPlan.Create(
            owner,
            planName,
            "Seeded by the performance harness.",
            ScoringRule.Cebraspe,
            PerformancePeriod.All(),
            PerformanceBands.CreateDefault(),
            new DateOnly(2027, 6, 1),
            120,
            false).Data;

        var sections = new List<Section>(shape.Sections);
        for (var index = 0; index < shape.Sections; index++)
        {
            sections.Add(plan.AddSection($"Section {index + 1:00}").Data);
        }

        var subjects = new List<Subject>(shape.Subjects);
        var seededSubjects = new List<SeededPerformanceSubject>(shape.Subjects);
        var notes = new List<TopicNote>();
        var topicsPerSubject = shape.TopicsPerSubject();

        for (var subjectIndex = 0; subjectIndex < shape.Subjects; subjectIndex++)
        {
            var subject = Subject.Create(owner, plan.Id, $"Subject {subjectIndex + 1:00}").Data;
            var section = sections.Count == 0 ? (SectionId?)null : sections[subjectIndex % sections.Count].Id;
            Require(plan.PlaceSubject(subject.Id, section));

            var topicIds = AddTopics(subject, subjectIndex, topicsPerSubject[subjectIndex], shape, owner, notes);
            subjects.Add(subject);
            seededSubjects.Add(new SeededPerformanceSubject(subject.Id, subject.Name, topicIds));
        }

        await context.WithDbContextAsync(async dbContext =>
        {
            await dbContext.StudyPlans.AddAsync(plan, TestContext.Current.CancellationToken);
            await dbContext.Subjects.AddRangeAsync(subjects, TestContext.Current.CancellationToken);
            await dbContext.TopicNotes.AddRangeAsync(notes, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return new SeededPerformancePlan(
            plan.Id,
            plan.Name,
            [.. sections.Select(section => section.Id)],
            seededSubjects,
            notes.Count);
    }

    private static List<TopicId> AddTopics(
        Subject subject,
        int subjectIndex,
        int count,
        PlanShape shape,
        UserId owner,
        List<TopicNote> notes)
    {
        var created = new List<TopicId>(count);
        var parents = new List<(TopicId Id, string Code, int Depth)>();
        var childCounters = new Dictionary<TopicId, int>();
        var rootCounter = 0;
        var statuses = Enum.GetValues<TopicStatus>();

        for (var index = 0; index < count; index++)
        {
            var asRoot = index % 6 == 0 || parents.Count == 0;
            TopicId? parentId = null;
            string code;
            var depth = 1;

            if (asRoot)
            {
                code = (++rootCounter).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                var parent = parents[index * 7919 % parents.Count];
                parentId = parent.Id;
                depth = parent.Depth + 1;
                childCounters.TryGetValue(parent.Id, out var children);
                childCounters[parent.Id] = ++children;
                code = string.Create(CultureInfo.InvariantCulture, $"{parent.Code}.{children}");
            }

            var title = string.Create(
                CultureInfo.InvariantCulture,
                $"{TitleStems[(index + subjectIndex) % TitleStems.Length]} subject {subjectIndex + 1:00} item {code}");
            var topic = subject.AddTopic(title, TopicCode.Create(code).Data, parentId, statuses[(index + subjectIndex) % statuses.Length]).Data;
            created.Add(topic.Id);

            if (depth < shape.MaxTopicDepth)
            {
                parents.Add((topic.Id, code, depth));
            }

            if (shape.NoteEvery > 0 && index % shape.NoteEvery == 0)
            {
                notes.Add(TopicNote.Create(owner, topic.Id, NoteTemplate).Data);
            }
        }

        return created;
    }

    private static void Require(Result result)
    {
        if (!result.IsOk)
        {
            throw new InvalidOperationException("The performance seed could not be built.");
        }
    }
}
