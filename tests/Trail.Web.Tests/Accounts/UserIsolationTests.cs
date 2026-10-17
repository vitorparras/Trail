using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Trail.Domain.Common;
using Trail.Domain.Planning;
using Trail.Infrastructure.Identity;
using Trail.Infrastructure.Persistence;
using Trail.Infrastructure.Persistence.PostgreSql;
using Trail.Infrastructure.Persistence.Sqlite;
using Trail.Web.Pages.Plans;
using Trail.Web.Tests.Persistence;

namespace Trail.Web.Tests.Accounts;

public sealed partial class UserIsolationTests
{
    private const string UserAEmail = "admin@trail.local";
    private const string UserAPassword = "AdminPass123!";
    private const string UserBEmail = "userb@trail.local";
    private const string UserBPassword = "UserBPass123!";

    private static readonly (Type Page, string MethodName)[] HandlersWithoutResourceIdentifiers =
    [
        (typeof(CreateModel), nameof(CreateModel.OnPostAsync)),
    ];

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task CrossUserIsolation_AcrossQueryAndHttp_EnforcesIsolationAndDeactivation(DatabaseProvider provider)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var factory = TrailWebApplicationFactory.Create(provider, new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = "",
            ["Registration:Mode"] = "InviteOnly"
        });

        using var clientA = CreateClient(factory);
        using var clientB = CreateClient(factory);

        using var registerPostRespA = await AccountTestHelper.RegisterAsync(clientA, UserAEmail, UserAPassword);
        Assert.Equal(HttpStatusCode.Redirect, registerPostRespA.StatusCode);

        using var loginPostRespA = await AccountTestHelper.SignInAsync(clientA, UserAEmail, UserAPassword);
        Assert.Equal(HttpStatusCode.Redirect, loginPostRespA.StatusCode);

        var (settingsRespA, settingsDocA, settingsHeaderA, settingsTokenA) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientA, "/Account/Settings");
        var settingsFormTokenA = settingsDocA.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var updateSettingsDataA = new Dictionary<string, string>
        {
            ["SettingsInput.TimeZoneId"] = "America/Manaus",
            ["SettingsInput.PomodoroFocusMinutes"] = "50",
            ["SettingsInput.PomodoroBreakMinutes"] = "10"
        };
        if (!string.IsNullOrEmpty(settingsFormTokenA))
        {
            updateSettingsDataA["__RequestVerificationToken"] = settingsFormTokenA;
        }

        using var updateSettingsRespA = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            clientA,
            "/Account/Settings?handler=Settings",
            updateSettingsDataA,
            settingsHeaderA,
            settingsTokenA);

        Assert.True(updateSettingsRespA.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

        var (adminRespA, adminDocA, adminHeaderA, adminTokenA) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientA, "/Admin/Users");
        var adminFormTokenA = adminDocA.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var inviteData = new Dictionary<string, string>
        {
            ["email"] = UserBEmail
        };
        if (!string.IsNullOrEmpty(adminFormTokenA))
        {
            inviteData["__RequestVerificationToken"] = adminFormTokenA;
        }

        using var inviteRespA = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            clientA,
            "/Admin/Users?handler=Invite",
            inviteData,
            adminHeaderA,
            adminTokenA);

        Assert.Equal(HttpStatusCode.Redirect, inviteRespA.StatusCode);

        var (_, adminAfterInviteDoc, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientA, "/Admin/Users");
        var inviteLinkInput = adminAfterInviteDoc.QuerySelector("#generated-invite-link");
        Assert.NotNull(inviteLinkInput);
        var inviteLink = inviteLinkInput.GetAttribute("value");
        Assert.False(string.IsNullOrWhiteSpace(inviteLink));

        var inviteUri = new Uri(inviteLink, UriKind.RelativeOrAbsolute);
        var inviteQuery = inviteUri.IsAbsoluteUri ? inviteUri.Query : inviteLink[inviteLink.IndexOf('?')..];
        var inviteToken = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(inviteQuery)["inviteToken"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(inviteToken));

        using var regPostRespB = await AccountTestHelper.RegisterAsync(clientB, UserBEmail, UserBPassword, inviteToken);
        Assert.True(regPostRespB.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

        var userAId = await FindUserIdAsync(factory, UserAEmail);
        var userBId = await FindUserIdAsync(factory, UserBEmail);

        var (_, adminDocForManualLink, adminHeaderA2, adminTokenA2) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientA, "/Admin/Users");
        var adminFormTokenA2 = adminDocForManualLink.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var manualLinkData = new Dictionary<string, string>
        {
            ["userId"] = userBId.ToString(),
            ["kind"] = "EmailConfirmation"
        };
        if (!string.IsNullOrEmpty(adminFormTokenA2))
        {
            manualLinkData["__RequestVerificationToken"] = adminFormTokenA2;
        }

        using var manualLinkResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            clientA,
            "/Admin/Users?handler=ManualLink",
            manualLinkData,
            adminHeaderA2,
            adminTokenA2);

        Assert.Equal(HttpStatusCode.Redirect, manualLinkResp.StatusCode);

        var (_, adminAfterManualDoc, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientA, "/Admin/Users");
        var manualLinkInput = adminAfterManualDoc.QuerySelector("#generated-manual-link");
        Assert.NotNull(manualLinkInput);
        var manualConfirmLink = manualLinkInput.GetAttribute("value");
        Assert.False(string.IsNullOrWhiteSpace(manualConfirmLink));

        var manualConfirmUri = new Uri(manualConfirmLink, UriKind.RelativeOrAbsolute);
        var manualConfirmPath = manualConfirmUri.IsAbsoluteUri ? manualConfirmUri.PathAndQuery : manualConfirmLink;

        using var confirmRespB = await clientB.GetAsync(manualConfirmPath, cancellationToken);
        Assert.True(confirmRespB.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

        using var loginPostRespB = await AccountTestHelper.SignInAsync(clientB, UserBEmail, UserBPassword);
        Assert.Equal(HttpStatusCode.Redirect, loginPostRespB.StatusCode);

        var (settingsRespB, settingsDocB, settingsHeaderB, settingsTokenB) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientB, "/Account/Settings");
        var settingsFormTokenB = settingsDocB.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var updateSettingsDataB = new Dictionary<string, string>
        {
            ["SettingsInput.TimeZoneId"] = "Europe/Lisbon",
            ["SettingsInput.PomodoroFocusMinutes"] = "30",
            ["SettingsInput.PomodoroBreakMinutes"] = "5"
        };
        if (!string.IsNullOrEmpty(settingsFormTokenB))
        {
            updateSettingsDataB["__RequestVerificationToken"] = settingsFormTokenB;
        }

        using var updateSettingsRespB = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            clientB,
            "/Account/Settings?handler=Settings",
            updateSettingsDataB,
            settingsHeaderB,
            settingsTokenB);

        Assert.True(updateSettingsRespB.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

        using (var scope = factory.Services.CreateScope())
        {
            await using var dbContext = CreateDbContext(scope, provider, UserId.From(userBId));

            var userASettings = await dbContext.UserSettings.FirstOrDefaultAsync(x => x.Id == UserId.From(userAId), cancellationToken);
            Assert.Null(userASettings);

            var allSettingsForUserB = await dbContext.UserSettings.ToListAsync(cancellationToken);
            Assert.Single(allSettingsForUserB);
            Assert.Equal(userBId, allSettingsForUserB[0].Id.Value);
            Assert.Equal("Europe/Lisbon", allSettingsForUserB[0].TimeZoneId);
            Assert.Equal(30, allSettingsForUserB[0].PomodoroFocusMinutes);
            Assert.Equal(5, allSettingsForUserB[0].PomodoroBreakMinutes);
        }

        var (getSettingsRespB, getSettingsDocB, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientB, "/Account/Settings");
        Assert.Equal(HttpStatusCode.OK, getSettingsRespB.StatusCode);

        var selectedTzOption = getSettingsDocB.QuerySelector("select[name='SettingsInput.TimeZoneId'] option[selected]");
        Assert.Equal("Europe/Lisbon", selectedTzOption?.GetAttribute("value"));

        var focusInputVal = getSettingsDocB.QuerySelector("input[name='SettingsInput.PomodoroFocusMinutes']")?.GetAttribute("value");
        Assert.Equal("30", focusInputVal);

        var breakInputVal = getSettingsDocB.QuerySelector("input[name='SettingsInput.PomodoroBreakMinutes']")?.GetAttribute("value");
        Assert.Equal("5", breakInputVal);

        var pageHtmlB = getSettingsDocB.DocumentElement.OuterHtml;
        Assert.DoesNotContain("America/Manaus", pageHtmlB);

        using var getAdminRespB = await clientB.GetAsync("/Admin/Users", cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, getAdminRespB.StatusCode);

        var toggleActiveDataB = new Dictionary<string, string>
        {
            ["userId"] = userAId.ToString(),
            ["isActive"] = "false"
        };
        if (!string.IsNullOrEmpty(settingsFormTokenB))
        {
            toggleActiveDataB["__RequestVerificationToken"] = settingsFormTokenB;
        }

        using var postToggleRespB = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            clientB,
            "/Admin/Users?handler=ToggleActive",
            toggleActiveDataB,
            settingsHeaderB,
            settingsTokenB);

        Assert.Equal(HttpStatusCode.Forbidden, postToggleRespB.StatusCode);

        var manualLinkDataB = new Dictionary<string, string>
        {
            ["userId"] = userAId.ToString(),
            ["kind"] = "PasswordReset"
        };
        if (!string.IsNullOrEmpty(settingsFormTokenB))
        {
            manualLinkDataB["__RequestVerificationToken"] = settingsFormTokenB;
        }

        using var postManualRespB = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            clientB,
            "/Admin/Users?handler=ManualLink",
            manualLinkDataB,
            settingsHeaderB,
            settingsTokenB);

        Assert.Equal(HttpStatusCode.Forbidden, postManualRespB.StatusCode);

        var (_, adminDocDeact, adminHeaderA3, adminTokenA3) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientA, "/Admin/Users");
        var adminFormTokenA3 = adminDocDeact.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var deactData = new Dictionary<string, string>
        {
            ["userId"] = userBId.ToString(),
            ["isActive"] = "false"
        };
        if (!string.IsNullOrEmpty(adminFormTokenA3))
        {
            deactData["__RequestVerificationToken"] = adminFormTokenA3;
        }

        using var deactResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            clientA,
            "/Admin/Users?handler=ToggleActive",
            deactData,
            adminHeaderA3,
            adminTokenA3);

        Assert.Equal(HttpStatusCode.Redirect, deactResp.StatusCode);

        using var postDeactReqB = await clientB.GetAsync("/Account/Settings", cancellationToken);
        Assert.True(postDeactReqB.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect);

        using var reLoginPostRespB = await AccountTestHelper.SignInAsync(clientB, UserBEmail, UserBPassword, rememberMe: false);
        Assert.Equal(HttpStatusCode.Forbidden, reLoginPostRespB.StatusCode);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task PlanningRoutes_WithAnotherUsersIdentifiers_ReturnNotFoundWithoutLeakingOrChangingData(DatabaseProvider provider)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var factory = TrailWebApplicationFactory.Create(provider, new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = "",
            ["Registration:Mode"] = "Open"
        });

        using var clientA = CreateClient(factory);
        using var clientB = CreateClient(factory);

        var userAId = await RegisterConfirmAndSignInAsync(factory, clientA, UserAEmail, UserAPassword, HttpStatusCode.Redirect);
        var userBId = await RegisterConfirmAndSignInAsync(factory, clientB, UserBEmail, UserBPassword, HttpStatusCode.OK);

        var victim = await SeedCompletePlanAsync(factory, provider, UserId.From(userAId), "Alpha", cancellationToken);
        var attacker = await SeedCompletePlanAsync(factory, provider, UserId.From(userBId), "Bravo", cancellationToken);

        var (plansResponseB, _, csrfHeaderB, csrfTokenB) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientB, "/planos");
        Assert.Equal(HttpStatusCode.OK, plansResponseB.StatusCode);

        var routeCases = PlanningIsolationSweep.Build(victim, attacker);
        foreach (var routeCase in routeCases)
        {
            using var response = await PlanningIsolationSweep.SendAsync(clientB, routeCase, csrfHeaderB, csrfTokenB);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            Assert.True(
                response.StatusCode == routeCase.ExpectedStatus,
                $"{routeCase.Name} ({routeCase.Method} {routeCase.Path}) answered {(int)response.StatusCode} instead of {(int)routeCase.ExpectedStatus}.");

            foreach (var sensitiveText in victim.SensitiveTexts)
            {
                Assert.False(
                    body.Contains(sensitiveText, StringComparison.Ordinal),
                    $"{routeCase.Name} ({routeCase.Method} {routeCase.Path}) exposed '{sensitiveText}'.");
            }
        }

        await AssertSeededPlanUnchangedAsync(factory, provider, UserId.From(userAId), victim, cancellationToken);
        await AssertSeededPlanUnchangedAsync(factory, provider, UserId.From(userBId), attacker, cancellationToken);
        await AssertNoRowsBeyondSeedAsync(factory, provider, cancellationToken);

        var (planAResponse, planADocument, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientA, $"/planos/{victim.PlanId}");
        Assert.Equal(HttpStatusCode.OK, planAResponse.StatusCode);
        Assert.Contains(victim.PlanName, planADocument.Body!.TextContent, StringComparison.Ordinal);
        Assert.Contains(victim.SubjectName, planADocument.Body.TextContent, StringComparison.Ordinal);

        var (noteAResponse, noteADocument, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(
            clientA,
            $"/planos/{victim.PlanId}/disciplinas/{victim.SubjectId}?handler=TopicNote&topicId={victim.TopicId}");
        Assert.Equal(HttpStatusCode.OK, noteAResponse.StatusCode);
        Assert.Contains(victim.NoteMarkdown, noteADocument.Body!.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanningIsolationSweep_CoversEveryPlansPageHandler()
    {
        var handlers = DiscoverPlansPageHandlers();
        Assert.NotEmpty(handlers);

        var sweepKeys = PlanningIsolationSweep
            .Build(SeededPlan.Placeholder("Victim"), SeededPlan.Placeholder("Attacker"))
            .Select(routeCase => HandlerKey(routeCase.Page, routeCase.Method.Method, routeCase.Handler))
            .ToHashSet(StringComparer.Ordinal);

        var exemptKeys = HandlersWithoutResourceIdentifiers
            .Select(handler => handlers.Single(h => h.Page == handler.Page && h.Method.Name == handler.MethodName))
            .Select(handler => handler.Key)
            .ToHashSet(StringComparer.Ordinal);

        var missing = handlers
            .Where(handler => !sweepKeys.Contains(handler.Key) && !exemptKeys.Contains(handler.Key))
            .Select(handler => $"{handler.Page.Name}.{handler.Method.Name}")
            .ToList();

        var unknown = sweepKeys
            .Where(key => handlers.All(handler => handler.Key != key))
            .ToList();

        Assert.True(missing.Count == 0, "Handlers missing from the isolation sweep: " + string.Join(", ", missing));
        Assert.True(unknown.Count == 0, "Sweep cases targeting handlers that do not exist: " + string.Join(", ", unknown));
    }

    [Fact]
    public void HandlersWithoutResourceIdentifiers_TakeNoIdentifiers()
    {
        foreach (var (page, methodName) in HandlersWithoutResourceIdentifiers)
        {
            var method = page.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.NotNull(method);

            Assert.DoesNotContain(method.GetParameters(), parameter => IsIdentifier(parameter.ParameterType));

            var boundProperties = page
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetCustomAttribute<BindPropertyAttribute>() is not null);

            foreach (var boundProperty in boundProperties)
            {
                Assert.False(IsIdentifier(boundProperty.PropertyType), $"{page.Name}.{boundProperty.Name} binds an identifier.");
                Assert.DoesNotContain(
                    boundProperty.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                    property => IsIdentifier(property.PropertyType));
            }
        }
    }

    private static List<(Type Page, MethodInfo Method, string Key)> DiscoverPlansPageHandlers()
    {
        var pageTypes = typeof(DetailsModel).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(DetailsModel).Namespace
                && type.IsClass
                && !type.IsAbstract
                && typeof(PageModel).IsAssignableFrom(type));

        var handlers = new List<(Type Page, MethodInfo Method, string Key)>();
        foreach (var pageType in pageTypes)
        {
            foreach (var method in pageType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var match = HandlerMethodPattern().Match(method.Name);
                if (match.Success)
                {
                    var verb = match.Groups["verb"].Value.ToUpperInvariant();
                    handlers.Add((pageType, method, HandlerKey(pageType, verb, match.Groups["handler"].Value)));
                }
            }
        }

        return handlers;
    }

    private static string HandlerKey(Type page, string verb, string handler) => $"{page.Name}:{verb}:{handler}";

    private static bool IsIdentifier(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(Guid)
            || underlying.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITypedId<>));
    }

    [GeneratedRegex("^On(?<verb>Get|Post)(?<handler>\\w*?)(Async)?$", RegexOptions.CultureInvariant)]
    private static partial Regex HandlerMethodPattern();

    private static HttpClient CreateClient(TrailWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<Guid> RegisterConfirmAndSignInAsync(
        TrailWebApplicationFactory factory,
        HttpClient client,
        string email,
        string password,
        HttpStatusCode expectedRegistrationStatus)
    {
        using var registerResponse = await AccountTestHelper.RegisterAsync(client, email, password);
        Assert.Equal(expectedRegistrationStatus, registerResponse.StatusCode);

        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);

            if (!user.EmailConfirmed)
            {
                var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmation = await userManager.ConfirmEmailAsync(user, confirmationToken);
                Assert.True(confirmation.Succeeded);
            }

            userId = user.Id;
        }

        using var signInResponse = await AccountTestHelper.SignInAsync(client, email, password);
        Assert.Equal(HttpStatusCode.Redirect, signInResponse.StatusCode);

        return userId;
    }

    private static async Task<Guid> FindUserIdAsync(TrailWebApplicationFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        return user.Id;
    }

    private static AppDbContext CreateDbContext(IServiceScope scope, DatabaseProvider provider, UserId? currentUserId)
    {
        var currentUser = new TestCurrentUser
        {
            IsAuthenticated = currentUserId.HasValue,
            Id = currentUserId ?? UserId.New(),
        };

        return provider == DatabaseProvider.Sqlite
            ? new SqliteAppDbContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<SqliteAppDbContext>>(), currentUser)
            : new PostgreSqlAppDbContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<PostgreSqlAppDbContext>>(), currentUser);
    }

    private static async Task<SeededPlan> SeedCompletePlanAsync(
        TrailWebApplicationFactory factory,
        DatabaseProvider provider,
        UserId ownerId,
        string label,
        CancellationToken cancellationToken)
    {
        var plan = StudyPlan.Create(
            ownerId,
            $"{label} plan",
            $"{label} description",
            ScoringRule.Cebraspe,
            PerformancePeriod.All(),
            PerformanceBands.CreateDefault(),
            new DateOnly(2026, 12, 1),
            120,
            false).Data;

        var section = plan.AddSection($"{label} section").Data;
        var spareSection = plan.AddSection($"{label} spare section").Data;
        var subject = Subject.Create(ownerId, plan.Id, $"{label} subject").Data;
        Assert.True(plan.PlaceSubject(subject.Id, section.Id).IsOk);

        var topic = subject.AddTopic($"{label} topic", TopicCode.Create("1").Data, null, TopicStatus.ToStudy).Data;
        var subtopic = subject.AddTopic($"{label} subtopic", TopicCode.Create("1.1").Data, topic.Id, TopicStatus.ToStudy).Data;
        var siblingTopic = subject.AddTopic($"{label} sibling topic", TopicCode.Create("2").Data, null, TopicStatus.ToStudy).Data;
        var note = TopicNote.Create(ownerId, topic.Id, $"{label} note").Data;

        using (var scope = factory.Services.CreateScope())
        {
            await using var dbContext = CreateDbContext(scope, provider, ownerId);
            await dbContext.StudyPlans.AddAsync(plan, cancellationToken);
            await dbContext.Subjects.AddAsync(subject, cancellationToken);
            await dbContext.TopicNotes.AddAsync(note, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        using (var scope = factory.Services.CreateScope())
        {
            await using var dbContext = CreateDbContext(scope, provider, null);
            var planVersion = await dbContext.StudyPlans.IgnoreQueryFilters().Where(p => p.Id == plan.Id).Select(p => p.Version).SingleAsync(cancellationToken);
            var subjectVersion = await dbContext.Subjects.IgnoreQueryFilters().Where(s => s.Id == subject.Id).Select(s => s.Version).SingleAsync(cancellationToken);
            var noteVersion = await dbContext.TopicNotes.IgnoreQueryFilters().Where(n => n.Id == note.Id).Select(n => n.Version).SingleAsync(cancellationToken);

            return new SeededPlan(
                plan.Id.Value,
                section.Id.Value,
                spareSection.Id.Value,
                subject.Id.Value,
                topic.Id.Value,
                subtopic.Id.Value,
                siblingTopic.Id.Value,
                planVersion,
                subjectVersion,
                noteVersion,
                plan.Name,
                section.Name,
                spareSection.Name,
                subject.Name,
                topic.Title,
                subtopic.Title,
                siblingTopic.Title,
                note.Markdown);
        }
    }

    private static async Task AssertSeededPlanUnchangedAsync(
        TrailWebApplicationFactory factory,
        DatabaseProvider provider,
        UserId ownerId,
        SeededPlan seeded,
        CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        await using var dbContext = CreateDbContext(scope, provider, null);

        var planId = StudyPlanId.From(seeded.PlanId);
        var subjectId = SubjectId.From(seeded.SubjectId);

        var plan = await dbContext.StudyPlans
            .IgnoreQueryFilters()
            .Include(p => p.Sections)
            .Include(p => p.Outline)
            .SingleAsync(p => p.Id == planId, cancellationToken);

        Assert.Equal(ownerId, plan.OwnerId);
        Assert.Equal(seeded.PlanName, plan.Name);
        Assert.Equal(seeded.PlanVersion, plan.Version);
        Assert.Equal(
            new[] { seeded.SectionName, seeded.SpareSectionName },
            plan.Sections.OrderBy(s => s.Order).Select(s => s.Name).ToArray());

        var placement = Assert.Single(plan.Outline);
        Assert.Equal(subjectId, placement.SubjectId);
        Assert.Equal(SectionId.From(seeded.SectionId), placement.SectionId);

        var sectionsOfPlan = await dbContext.Set<Section>().IgnoreQueryFilters().CountAsync(s => s.StudyPlanId == planId, cancellationToken);
        Assert.Equal(2, sectionsOfPlan);

        var placementsOfPlan = await dbContext.Set<SubjectPlacement>().IgnoreQueryFilters().CountAsync(p => p.StudyPlanId == planId, cancellationToken);
        Assert.Equal(1, placementsOfPlan);

        var subjectsOfPlan = await dbContext.Subjects
            .IgnoreQueryFilters()
            .Include(s => s.Topics)
            .Where(s => s.StudyPlanId == planId)
            .ToListAsync(cancellationToken);

        var subject = Assert.Single(subjectsOfPlan);
        Assert.Equal(subjectId, subject.Id);
        Assert.Equal(ownerId, subject.OwnerId);
        Assert.Equal(seeded.SubjectName, subject.Name);
        Assert.Equal(seeded.SubjectVersion, subject.Version);

        var expectedTopics = new[]
        {
            (Id: seeded.TopicId, Title: seeded.TopicTitle, ParentId: (Guid?)null),
            (Id: seeded.SubtopicId, Title: seeded.SubtopicTitle, ParentId: (Guid?)seeded.TopicId),
            (Id: seeded.SiblingTopicId, Title: seeded.SiblingTopicTitle, ParentId: (Guid?)null),
        };

        Assert.Equal(expectedTopics.Length, subject.Topics.Count);
        foreach (var expected in expectedTopics)
        {
            var topic = Assert.Single(subject.Topics, t => t.Id == TopicId.From(expected.Id));
            Assert.Equal(expected.Title, topic.Title);
            Assert.Equal(expected.ParentId, topic.ParentId?.Value);
            Assert.Equal(TopicStatus.ToStudy, topic.Status);

            var notesOfTopic = await dbContext.TopicNotes
                .IgnoreQueryFilters()
                .Where(n => n.TopicId == topic.Id)
                .ToListAsync(cancellationToken);

            if (expected.Id == seeded.TopicId)
            {
                var note = Assert.Single(notesOfTopic);
                Assert.Equal(ownerId, note.OwnerId);
                Assert.Equal(seeded.NoteMarkdown, note.Markdown);
                Assert.Equal(seeded.NoteVersion, note.Version);
            }
            else
            {
                Assert.Empty(notesOfTopic);
            }
        }

        var topicsOfSubject = await dbContext.Set<Topic>().IgnoreQueryFilters().CountAsync(t => t.SubjectId == subjectId, cancellationToken);
        Assert.Equal(expectedTopics.Length, topicsOfSubject);
    }

    private static async Task AssertNoRowsBeyondSeedAsync(
        TrailWebApplicationFactory factory,
        DatabaseProvider provider,
        CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        await using var dbContext = CreateDbContext(scope, provider, null);

        Assert.Equal(2, await dbContext.StudyPlans.IgnoreQueryFilters().CountAsync(cancellationToken));
        Assert.Equal(4, await dbContext.Set<Section>().IgnoreQueryFilters().CountAsync(cancellationToken));
        Assert.Equal(2, await dbContext.Set<SubjectPlacement>().IgnoreQueryFilters().CountAsync(cancellationToken));
        Assert.Equal(2, await dbContext.Subjects.IgnoreQueryFilters().CountAsync(cancellationToken));
        Assert.Equal(6, await dbContext.Set<Topic>().IgnoreQueryFilters().CountAsync(cancellationToken));
        Assert.Equal(2, await dbContext.TopicNotes.IgnoreQueryFilters().CountAsync(cancellationToken));
    }
}
