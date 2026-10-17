using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Trail.Domain.Common;
using Trail.Infrastructure.Identity;
using Trail.Infrastructure.Persistence;
using Trail.Infrastructure.Persistence.PostgreSql;
using Trail.Infrastructure.Persistence.Sqlite;
using Trail.Web.Tests.Persistence;

namespace Trail.Web.Tests.Accounts;

public sealed class UserIsolationTests
{
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

        using var clientA = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        using var clientB = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var (regRespA, regDocA, regHeaderA, regTokenA) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientA, "/Account/Register");
        var formTokenA = regDocA.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var regDataA = new Dictionary<string, string>
        {
            ["Input.Email"] = "admin@trail.local",
            ["Input.Password"] = "AdminPass123!",
            ["Input.ConfirmPassword"] = "AdminPass123!"
        };
        if (!string.IsNullOrEmpty(formTokenA))
        {
            regDataA["__RequestVerificationToken"] = formTokenA;
        }

        using var registerPostRespA = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            clientA,
            "/Account/Register",
            regDataA,
            regHeaderA,
            regTokenA);

        Assert.Equal(HttpStatusCode.Redirect, registerPostRespA.StatusCode);

        var (loginRespA, loginDocA, loginHeaderA, loginTokenA) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientA, "/Account/Login");
        var loginFormTokenA = loginDocA.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var loginDataA = new Dictionary<string, string>
        {
            ["Input.Email"] = "admin@trail.local",
            ["Input.Password"] = "AdminPass123!",
            ["Input.RememberMe"] = "true"
        };
        if (!string.IsNullOrEmpty(loginFormTokenA))
        {
            loginDataA["__RequestVerificationToken"] = loginFormTokenA;
        }

        using var loginPostRespA = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            clientA,
            "/Account/Login",
            loginDataA,
            loginHeaderA,
            loginTokenA);

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
            ["email"] = "userb@trail.local"
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

        var (regRespB, regDocB, regHeaderB, regTokenB) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientB, $"/Account/Register?inviteToken={Uri.EscapeDataString(inviteToken)}");
        var regFormTokenB = regDocB.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var regDataB = new Dictionary<string, string>
        {
            ["Input.Email"] = "userb@trail.local",
            ["Input.Password"] = "UserBPass123!",
            ["Input.ConfirmPassword"] = "UserBPass123!",
            ["Input.InviteToken"] = inviteToken
        };
        if (!string.IsNullOrEmpty(regFormTokenB))
        {
            regDataB["__RequestVerificationToken"] = regFormTokenB;
        }

        using var regPostRespB = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            clientB,
            "/Account/Register",
            regDataB,
            regHeaderB,
            regTokenB);

        Assert.True(regPostRespB.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

        Guid userAId;
        Guid userBId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var userA = await userManager.FindByEmailAsync("admin@trail.local");
            var userB = await userManager.FindByEmailAsync("userb@trail.local");

            Assert.NotNull(userA);
            Assert.NotNull(userB);

            userAId = userA.Id;
            userBId = userB.Id;
        }

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

        var (loginRespB, loginDocB, loginHeaderB, loginTokenB) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientB, "/Account/Login");
        var loginFormTokenB = loginDocB.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var loginDataB = new Dictionary<string, string>
        {
            ["Input.Email"] = "userb@trail.local",
            ["Input.Password"] = "UserBPass123!",
            ["Input.RememberMe"] = "true"
        };
        if (!string.IsNullOrEmpty(loginFormTokenB))
        {
            loginDataB["__RequestVerificationToken"] = loginFormTokenB;
        }

        using var loginPostRespB = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            clientB,
            "/Account/Login",
            loginDataB,
            loginHeaderB,
            loginTokenB);

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

        var currentUserB = new TestCurrentUser
        {
            IsAuthenticated = true,
            Id = UserId.From(userBId),
            IsAdmin = false
        };

        using (var scope = factory.Services.CreateScope())
        {
            AppDbContext dbContext;
            if (provider == DatabaseProvider.Sqlite)
            {
                var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SqliteAppDbContext>>();
                dbContext = new SqliteAppDbContext(options, currentUserB);
            }
            else
            {
                var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<PostgreSqlAppDbContext>>();
                dbContext = new PostgreSqlAppDbContext(options, currentUserB);
            }

            await using (dbContext)
            {
                var userASettings = await dbContext.UserSettings.FirstOrDefaultAsync(x => x.Id == UserId.From(userAId), cancellationToken);
                Assert.Null(userASettings);

                var allSettingsForUserB = await dbContext.UserSettings.ToListAsync(cancellationToken);
                Assert.Single(allSettingsForUserB);
                Assert.Equal(userBId, allSettingsForUserB[0].Id.Value);
                Assert.Equal("Europe/Lisbon", allSettingsForUserB[0].TimeZoneId);
                Assert.Equal(30, allSettingsForUserB[0].PomodoroFocusMinutes);
                Assert.Equal(5, allSettingsForUserB[0].PomodoroBreakMinutes);
            }
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

        var (reLoginRespB, reLoginDocB, reLoginHeaderB, reLoginTokenB) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientB, "/Account/Login");
        var reLoginFormTokenB = reLoginDocB.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var reLoginDataB = new Dictionary<string, string>
        {
            ["Input.Email"] = "userb@trail.local",
            ["Input.Password"] = "UserBPass123!"
        };
        if (!string.IsNullOrEmpty(reLoginFormTokenB))
        {
            reLoginDataB["__RequestVerificationToken"] = reLoginFormTokenB;
        }

        using var reLoginPostRespB = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            clientB,
            "/Account/Login",
            reLoginDataB,
            reLoginHeaderB,
            reLoginTokenB);

        Assert.Equal(HttpStatusCode.Forbidden, reLoginPostRespB.StatusCode);
    }
}
