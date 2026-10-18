using System.Globalization;
using System.Net;
using Trail.Web.Htmx;
using Trail.Web.Pages.Plans;

namespace Trail.Web.Tests.Accounts;

internal sealed record SeededPlan(
    Guid PlanId,
    Guid SectionId,
    Guid SpareSectionId,
    Guid SubjectId,
    Guid TopicId,
    Guid SubtopicId,
    Guid SiblingTopicId,
    int PlanVersion,
    int SubjectVersion,
    int NoteVersion,
    string PlanName,
    string SectionName,
    string SpareSectionName,
    string SubjectName,
    string TopicTitle,
    string SubtopicTitle,
    string SiblingTopicTitle,
    string NoteMarkdown)
{
    public IReadOnlyList<string> SensitiveTexts =>
    [
        PlanName,
        SectionName,
        SpareSectionName,
        SubjectName,
        TopicTitle,
        SubtopicTitle,
        SiblingTopicTitle,
        NoteMarkdown,
    ];

    public static SeededPlan Placeholder(string label) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        1,
        1,
        1,
        $"{label} plan",
        $"{label} section",
        $"{label} spare section",
        $"{label} subject",
        $"{label} topic",
        $"{label} subtopic",
        $"{label} sibling topic",
        $"{label} note");
}

internal sealed record PlanningRouteCase(
    string Name,
    Type Page,
    HttpMethod Method,
    string Handler,
    string Path,
    IReadOnlyDictionary<string, string> Form,
    bool Htmx,
    HttpStatusCode ExpectedStatus);

[IsolationSweep("Trail.Web.Pages.Plans")]
internal static class PlanningIsolationSweep
{
    public static IReadOnlyList<PlanningRouteCase> Build(SeededPlan victim, SeededPlan attacker)
    {
        ArgumentNullException.ThrowIfNull(victim);
        ArgumentNullException.ThrowIfNull(attacker);

        var v = victim;
        var a = attacker;

        return
        [
            Get<IndexModel>("List plans shows only own plans", string.Empty, Guid.Empty, null, expected: HttpStatusCode.OK),
            Get<CreateModel>("Create plan form has no foreign data", string.Empty, Guid.Empty, null, expected: HttpStatusCode.OK),

            Get<EditModel>("Edit form of foreign plan", string.Empty, v.PlanId, null),
            Post<EditModel>("Update foreign plan", string.Empty, v.PlanId, null, new()
            {
                ["Input.ExpectedVersion"] = Version(v.PlanVersion),
                ["Input.Name"] = "Changed by attacker",
                ["Input.ScoringRule"] = "Cebraspe",
                ["Input.PeriodKind"] = "All",
                ["Input.LowBandThreshold"] = "50",
                ["Input.HighBandThreshold"] = "75",
                ["Input.DefaultMockExamQuestions"] = "120",
            }),

            Get<DetailsModel>("Details of foreign plan", string.Empty, v.PlanId, null),
            Get<DetailsModel>("Details of foreign plan as partial", string.Empty, v.PlanId, null, htmx: true),
            Get<DetailsModel>("Own plan details filtered by foreign subject", string.Empty, a.PlanId, null, new()
            {
                ["filterSubjectId"] = v.SubjectId.ToString(),
            }),
            Get<DetailsModel>("Search topics of foreign plan", "SearchTopics", v.PlanId, null, new()
            {
                ["q"] = v.TopicTitle,
            }),
            Get<DetailsModel>("Search topics of foreign plan as partial", "SearchTopics", v.PlanId, null, new()
            {
                ["q"] = v.TopicTitle,
            }, htmx: true),
            Get<DetailsModel>("Search own plan filtered by foreign subject", "SearchTopics", a.PlanId, null, new()
            {
                ["filterSubjectId"] = v.SubjectId.ToString(),
            }),
            Get<DetailsModel>("Search own plan filtered by foreign subject as partial", "SearchTopics", a.PlanId, null, new()
            {
                ["filterSubjectId"] = v.SubjectId.ToString(),
            }, htmx: true),
            Post<DetailsModel>("Add section to foreign plan", "AddSection", v.PlanId, null, new()
            {
                ["name"] = "Attacker section",
                ["expectedPlanVersion"] = Version(v.PlanVersion),
            }),
            Post<DetailsModel>("Update section of foreign plan", "UpdateSection", v.PlanId, null, new()
            {
                ["sectionId"] = v.SectionId.ToString(),
                ["name"] = "Attacker section name",
                ["expectedPlanVersion"] = Version(v.PlanVersion),
            }),
            Post<DetailsModel>("Update foreign section through own plan", "UpdateSection", a.PlanId, null, new()
            {
                ["sectionId"] = v.SectionId.ToString(),
                ["name"] = "Attacker section name",
                ["expectedPlanVersion"] = Version(a.PlanVersion),
            }),
            Post<DetailsModel>("Reorder sections of foreign plan", "ReorderSections", v.PlanId, null, new()
            {
                ["sectionId"] = v.SectionId.ToString(),
                ["direction"] = "down",
                ["expectedPlanVersion"] = Version(v.PlanVersion),
            }),
            Post<DetailsModel>("Reorder foreign section through own plan", "ReorderSections", a.PlanId, null, new()
            {
                ["sectionId"] = v.SectionId.ToString(),
                ["direction"] = "down",
                ["expectedPlanVersion"] = Version(a.PlanVersion),
            }),
            Post<DetailsModel>("Delete section of foreign plan", "DeleteSection", v.PlanId, null, new()
            {
                ["sectionId"] = v.SpareSectionId.ToString(),
                ["expectedPlanVersion"] = Version(v.PlanVersion),
            }),
            Post<DetailsModel>("Delete foreign section through own plan", "DeleteSection", a.PlanId, null, new()
            {
                ["sectionId"] = v.SpareSectionId.ToString(),
                ["expectedPlanVersion"] = Version(a.PlanVersion),
            }),
            Post<DetailsModel>("Add subject to foreign plan", "AddSubject", v.PlanId, null, new()
            {
                ["name"] = "Attacker subject",
                ["sectionId"] = v.SectionId.ToString(),
                ["expectedPlanVersion"] = Version(v.PlanVersion),
            }),
            Post<DetailsModel>("Add subject to own plan inside foreign section", "AddSubject", a.PlanId, null, new()
            {
                ["name"] = "Attacker subject",
                ["sectionId"] = v.SectionId.ToString(),
                ["expectedPlanVersion"] = Version(a.PlanVersion),
            }),
            Post<DetailsModel>("Move subject of foreign plan", "MoveSubject", v.PlanId, null, new()
            {
                ["subjectId"] = v.SubjectId.ToString(),
                ["newSectionId"] = v.SpareSectionId.ToString(),
                ["expectedPlanVersion"] = Version(v.PlanVersion),
            }),
            Post<DetailsModel>("Move foreign subject inside own plan", "MoveSubject", a.PlanId, null, new()
            {
                ["subjectId"] = v.SubjectId.ToString(),
                ["newSectionId"] = a.SpareSectionId.ToString(),
                ["expectedPlanVersion"] = Version(a.PlanVersion),
            }),
            Post<DetailsModel>("Move own subject into foreign section", "MoveSubject", a.PlanId, null, new()
            {
                ["subjectId"] = a.SubjectId.ToString(),
                ["newSectionId"] = v.SpareSectionId.ToString(),
                ["expectedPlanVersion"] = Version(a.PlanVersion),
            }),
            Post<DetailsModel>("Reorder subjects of foreign plan", "ReorderSubjects", v.PlanId, null, new()
            {
                ["sectionId"] = v.SectionId.ToString(),
                ["subjectId"] = v.SubjectId.ToString(),
                ["direction"] = "up",
                ["expectedPlanVersion"] = Version(v.PlanVersion),
            }),
            Post<DetailsModel>("Reorder foreign subject inside own section", "ReorderSubjects", a.PlanId, null, new()
            {
                ["sectionId"] = a.SectionId.ToString(),
                ["subjectId"] = v.SubjectId.ToString(),
                ["direction"] = "up",
                ["expectedPlanVersion"] = Version(a.PlanVersion),
            }),
            Post<DetailsModel>("Reorder own subject inside foreign section", "ReorderSubjects", a.PlanId, null, new()
            {
                ["sectionId"] = v.SectionId.ToString(),
                ["subjectId"] = a.SubjectId.ToString(),
                ["direction"] = "up",
                ["expectedPlanVersion"] = Version(a.PlanVersion),
            }),
            Post<DetailsModel>("Delete subject of foreign plan", "DeleteSubject", v.PlanId, null, new()
            {
                ["subjectId"] = v.SubjectId.ToString(),
            }),
            Post<DetailsModel>("Delete foreign subject through own plan", "DeleteSubject", a.PlanId, null, new()
            {
                ["subjectId"] = v.SubjectId.ToString(),
            }),
            Post<DetailsModel>("Delete foreign plan", "DeletePlan", v.PlanId, null, []),

            .. SubjectRouteCases("foreign plan and subject", v.PlanId, v.SubjectId, v, v, includeSubjectLevelHandlers: true),
            .. SubjectRouteCases("foreign subject under own plan", a.PlanId, v.SubjectId, v, v, includeSubjectLevelHandlers: true),
            .. SubjectRouteCases("own subject under foreign plan", v.PlanId, a.SubjectId, a, a, includeSubjectLevelHandlers: true),
            .. SubjectRouteCases("foreign children inside own subject", a.PlanId, a.SubjectId, v, a, includeSubjectLevelHandlers: false),

            Get<SubjectModel>("Topic note of foreign plan and subject as partial", "TopicNote", v.PlanId, v.SubjectId, new()
            {
                ["topicId"] = v.TopicId.ToString(),
            }, htmx: true),
            Get<SubjectModel>("Foreign topic note inside own subject as partial", "TopicNote", a.PlanId, a.SubjectId, new()
            {
                ["topicId"] = v.TopicId.ToString(),
            }, htmx: true),
            Post<SubjectModel>("Change status in foreign plan and subject as partial", "ChangeStatus", v.PlanId, v.SubjectId, new()
            {
                ["topicId"] = v.TopicId.ToString(),
                ["newStatus"] = "Studying",
            }, htmx: true),
            Post<SubjectModel>("Change status of foreign topic inside own subject as partial", "ChangeStatus", a.PlanId, a.SubjectId, new()
            {
                ["topicId"] = v.TopicId.ToString(),
                ["newStatus"] = "Studying",
            }, htmx: true),
            Post<SubjectModel>("Add subtopic to own subject under foreign parent", "AddTopic", a.PlanId, a.SubjectId, new()
            {
                ["title"] = "Attacker subtopic",
                ["code"] = "1.9",
                ["parentId"] = v.TopicId.ToString(),
                ["initialStatus"] = "ToStudy",
                ["expectedSubjectVersion"] = Version(a.SubjectVersion),
            }),
            Post<SubjectModel>("Move own subtopic under foreign parent", "MoveTopic", a.PlanId, a.SubjectId, new()
            {
                ["topicId"] = a.SubtopicId.ToString(),
                ["newParentId"] = v.TopicId.ToString(),
                ["expectedSubjectVersion"] = Version(a.SubjectVersion),
            }),
        ];
    }

    public static IEnumerable<PlanningRouteCase> SubjectRouteCases(
        string scenario,
        Guid planId,
        Guid subjectId,
        SeededPlan children,
        SeededPlan versions,
        bool includeSubjectLevelHandlers)
    {
        yield return Get<SubjectModel>($"Subject page with {scenario}", string.Empty, planId, subjectId, new()
        {
            ["noteTopicId"] = children.TopicId.ToString(),
        });
        yield return Get<SubjectModel>($"Topic note with {scenario}", "TopicNote", planId, subjectId, new()
        {
            ["topicId"] = children.TopicId.ToString(),
        });
        yield return Get<SubjectModel>($"Topic deletion impact with {scenario}", "DeleteTopicImpact", planId, subjectId, new()
        {
            ["topicId"] = children.TopicId.ToString(),
        });
        yield return Post<SubjectModel>($"Save note with {scenario}", "SaveNote", planId, subjectId, new()
        {
            ["topicId"] = children.TopicId.ToString(),
            ["markdown"] = "Attacker note",
            ["expectedVersion"] = Version(children.NoteVersion),
        });
        if (includeSubjectLevelHandlers)
        {
            yield return Post<SubjectModel>($"Rename subject with {scenario}", "UpdateSubject", planId, subjectId, new()
            {
                ["name"] = "Attacker subject name",
                ["expectedVersion"] = Version(versions.SubjectVersion),
            });
            yield return Post<SubjectModel>($"Add topic with {scenario}", "AddTopic", planId, subjectId, new()
            {
                ["title"] = "Attacker topic",
                ["code"] = "9",
                ["initialStatus"] = "ToStudy",
                ["expectedSubjectVersion"] = Version(versions.SubjectVersion),
            });
        }

        yield return Post<SubjectModel>($"Change status with {scenario}", "ChangeStatus", planId, subjectId, new()
        {
            ["topicId"] = children.TopicId.ToString(),
            ["newStatus"] = "Studying",
        });
        yield return Post<SubjectModel>($"Update topic with {scenario}", "UpdateTopic", planId, subjectId, new()
        {
            ["topicId"] = children.TopicId.ToString(),
            ["code"] = "1",
            ["title"] = "Attacker topic title",
            ["expectedSubjectVersion"] = Version(versions.SubjectVersion),
        });
        yield return Post<SubjectModel>($"Move topic with {scenario}", "MoveTopic", planId, subjectId, new()
        {
            ["topicId"] = children.SubtopicId.ToString(),
            ["expectedSubjectVersion"] = Version(versions.SubjectVersion),
        });
        yield return Post<SubjectModel>($"Reorder topic with {scenario}", "ReorderTopic", planId, subjectId, new()
        {
            ["topicId"] = children.TopicId.ToString(),
            ["direction"] = "down",
            ["expectedSubjectVersion"] = Version(versions.SubjectVersion),
        });
        yield return Post<SubjectModel>($"Delete topic with {scenario}", "DeleteTopic", planId, subjectId, new()
        {
            ["topicId"] = children.TopicId.ToString(),
        });
    }

    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        PlanningRouteCase routeCase,
        string? csrfHeader,
        string? csrfToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(routeCase);

        using var request = new HttpRequestMessage(routeCase.Method, routeCase.Path);

        if (routeCase.Method == HttpMethod.Post)
        {
            request.Content = new FormUrlEncodedContent(routeCase.Form);
            if (!string.IsNullOrWhiteSpace(csrfHeader) && !string.IsNullOrWhiteSpace(csrfToken))
            {
                request.Headers.Add(csrfHeader, csrfToken);
            }
        }

        if (routeCase.Htmx)
        {
            request.Headers.Add(HtmxIntegration.RequestTypeHeaderName, HtmxIntegration.PartialRequestType);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static string RouteOf(Type page, Guid planId, Guid? subjectId)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (page == typeof(IndexModel))
        {
            return "/planos";
        }

        if (page == typeof(CreateModel))
        {
            return "/planos/novo";
        }

        if (page == typeof(EditModel))
        {
            return $"/planos/{planId}/editar";
        }

        if (page == typeof(DetailsModel))
        {
            return $"/planos/{planId}";
        }

        if (page == typeof(SubjectModel))
        {
            return $"/planos/{planId}/disciplinas/{subjectId}";
        }

        throw new ArgumentOutOfRangeException(nameof(page), page, "The page has no known route.");
    }

    private static PlanningRouteCase Get<TPage>(
        string name,
        string handler,
        Guid planId,
        Guid? subjectId,
        Dictionary<string, string>? query = null,
        bool htmx = false,
        HttpStatusCode expected = HttpStatusCode.NotFound)
    {
        var parameters = new List<KeyValuePair<string, string>>();
        if (handler.Length > 0)
        {
            parameters.Add(new("handler", handler));
        }

        parameters.AddRange(query ?? []);

        var path = RouteOf(typeof(TPage), planId, subjectId);
        if (parameters.Count > 0)
        {
            path += "?" + string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        }

        return new PlanningRouteCase(name, typeof(TPage), HttpMethod.Get, handler, path, new Dictionary<string, string>(), htmx, expected);
    }

    private static PlanningRouteCase Post<TPage>(
        string name,
        string handler,
        Guid planId,
        Guid? subjectId,
        Dictionary<string, string> form,
        bool htmx = false)
    {
        var path = RouteOf(typeof(TPage), planId, subjectId);
        if (handler.Length > 0)
        {
            path += $"?handler={handler}";
        }

        return new PlanningRouteCase(name, typeof(TPage), HttpMethod.Post, handler, path, form, htmx, HttpStatusCode.NotFound);
    }

    private static string Version(int version) => version.ToString(CultureInfo.InvariantCulture);
}
