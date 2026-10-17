using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.Sqlite;
using Trail.Infrastructure.Identity;
using Trail.Infrastructure.Persistence;

namespace Trail.Web.Tests.Accounts;

public sealed class EmailAndSessionPersistenceTests
{
    private const string MailpitImage = "axllent/mailpit:v1.24.0";

    [Fact]
    public async Task Mailpit_EndToEndEmailFlow_RegistersInvitesAndResetsPassword()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var mailpit = new ContainerBuilder(MailpitImage)
            .WithPortBinding(1025, assignRandomHostPort: true)
            .WithPortBinding(8025, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/api/v1/info")))
            .Build();

        await mailpit.StartAsync(cancellationToken);

        try
        {
            var smtpPort = mailpit.GetMappedPublicPort(1025);
            var httpPort = mailpit.GetMappedPublicPort(8025);
            var host = mailpit.Hostname;
            var mailpitBaseUrl = $"http://{host}:{httpPort}";

            using var mailpitHttpClient = new HttpClient();

            await using var factory = TrailWebApplicationFactory.Create(DatabaseProvider.Sqlite, new Dictionary<string, string?>
            {
                ["Email:Smtp:Host"] = host,
                ["Email:Smtp:Port"] = smtpPort.ToString(CultureInfo.InvariantCulture),
                ["Email:Smtp:FromAddress"] = "noreply@trail.local",
                ["Email:Smtp:FromName"] = "Trail",
                ["Email:Smtp:UseSsl"] = "false",
                ["Email:Smtp:UseStartTls"] = "false",
                ["Registration:Mode"] = "InviteOnly"
            });

            using var client1 = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            var (regResp1, regDoc1, regHeader1, regToken1) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client1, "/Account/Register");
            var formToken1 = regDoc1.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

            var regData1 = new Dictionary<string, string>
            {
                ["Input.Email"] = "admin@trail.local",
                ["Input.Password"] = "AdminPass123!",
                ["Input.ConfirmPassword"] = "AdminPass123!"
            };
            if (!string.IsNullOrEmpty(formToken1))
            {
                regData1["__RequestVerificationToken"] = formToken1;
            }

            using var regPostResp1 = await AccountTestHelper.PostFormWithAntiforgeryAsync(
                client1,
                "/Account/Register",
                regData1,
                regHeader1,
                regToken1);

            Assert.True(regPostResp1.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

            var confirmEmailMsg = await WaitForMailpitMessageAsync(mailpitHttpClient, mailpitBaseUrl, "admin@trail.local", cancellationToken);
            var confirmEmailBody = confirmEmailMsg.GetProperty("HTML").GetString() ?? confirmEmailMsg.GetProperty("Text").GetString()!;
            var confirmUrl = ExtractUrlPath(confirmEmailBody, "/Account/ConfirmEmail");

            using var confirmResp = await client1.GetAsync(confirmUrl, cancellationToken);
            Assert.True(confirmResp.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

            var (loginResp1, loginDoc1, loginHeader1, loginToken1) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client1, "/Account/Login");
            var loginFormToken1 = loginDoc1.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

            var loginData1 = new Dictionary<string, string>
            {
                ["Input.Email"] = "admin@trail.local",
                ["Input.Password"] = "AdminPass123!",
                ["Input.RememberMe"] = "true"
            };
            if (!string.IsNullOrEmpty(loginFormToken1))
            {
                loginData1["__RequestVerificationToken"] = loginFormToken1;
            }

            using var loginPostResp1 = await AccountTestHelper.PostFormWithAntiforgeryAsync(
                client1,
                "/Account/Login",
                loginData1,
                loginHeader1,
                loginToken1);

            Assert.Equal(HttpStatusCode.Redirect, loginPostResp1.StatusCode);

            await mailpitHttpClient.DeleteAsync($"{mailpitBaseUrl}/api/v1/messages", cancellationToken);

            var (adminResp, adminDoc, adminHeader, adminToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client1, "/Admin/Users");
            var adminFormToken = adminDoc.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

            var testEmailData = new Dictionary<string, string>
            {
                ["recipientEmail"] = "testrecipient@trail.local"
            };
            if (!string.IsNullOrEmpty(adminFormToken))
            {
                testEmailData["__RequestVerificationToken"] = adminFormToken;
            }

            using var testEmailResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
                client1,
                "/Admin/Users?handler=TestEmail",
                testEmailData,
                adminHeader,
                adminToken);

            Assert.Equal(HttpStatusCode.Redirect, testEmailResp.StatusCode);

            var testMsg = await WaitForMailpitMessageAsync(mailpitHttpClient, mailpitBaseUrl, "testrecipient@trail.local", cancellationToken);
            Assert.True(testMsg.ValueKind != JsonValueKind.Undefined);

            await mailpitHttpClient.DeleteAsync($"{mailpitBaseUrl}/api/v1/messages", cancellationToken);

            var (adminResp2, adminDoc2, adminHeader2, adminToken2) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client1, "/Admin/Users");
            var adminFormToken2 = adminDoc2.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

            var inviteData = new Dictionary<string, string>
            {
                ["email"] = "user2@trail.local"
            };
            if (!string.IsNullOrEmpty(adminFormToken2))
            {
                inviteData["__RequestVerificationToken"] = adminFormToken2;
            }

            using var inviteResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
                client1,
                "/Admin/Users?handler=Invite",
                inviteData,
                adminHeader2,
                adminToken2);

            Assert.Equal(HttpStatusCode.Redirect, inviteResp.StatusCode);

            var inviteMsg = await WaitForMailpitMessageAsync(mailpitHttpClient, mailpitBaseUrl, "user2@trail.local", cancellationToken);
            var inviteBody = inviteMsg.GetProperty("HTML").GetString() ?? inviteMsg.GetProperty("Text").GetString()!;
            var registerInviteUrl = ExtractUrlPath(inviteBody, "/Account/Register");

            using var client2 = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            var (regResp2, regDoc2, regHeader2, regToken2) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client2, registerInviteUrl);
            var regFormToken2 = regDoc2.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

            var inviteQuery = registerInviteUrl.Contains('?') ? registerInviteUrl[registerInviteUrl.IndexOf('?')..] : string.Empty;
            var parsedInviteToken = QueryHelpers.ParseQuery(inviteQuery)["inviteToken"].ToString();

            var regData2 = new Dictionary<string, string>
            {
                ["Input.Email"] = "user2@trail.local",
                ["Input.Password"] = "User2Pass123!",
                ["Input.ConfirmPassword"] = "User2Pass123!",
                ["Input.InviteToken"] = parsedInviteToken
            };
            if (!string.IsNullOrEmpty(regFormToken2))
            {
                regData2["__RequestVerificationToken"] = regFormToken2;
            }

            using var regPostResp2 = await AccountTestHelper.PostFormWithAntiforgeryAsync(
                client2,
                "/Account/Register",
                regData2,
                regHeader2,
                regToken2);

            Assert.True(regPostResp2.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

            var confirmEmailMsg2 = await WaitForMailpitMessageAsync(mailpitHttpClient, mailpitBaseUrl, "user2@trail.local", cancellationToken);
            var confirmBody2 = confirmEmailMsg2.GetProperty("HTML").GetString() ?? confirmEmailMsg2.GetProperty("Text").GetString()!;
            var confirmUrl2 = ExtractUrlPath(confirmBody2, "/Account/ConfirmEmail");

            using var confirmResp2 = await client2.GetAsync(confirmUrl2, cancellationToken);
            Assert.True(confirmResp2.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

            await mailpitHttpClient.DeleteAsync($"{mailpitBaseUrl}/api/v1/messages", cancellationToken);

            var (forgotResp, forgotDoc, forgotHeader, forgotToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client2, "/Account/ForgotPassword");
            var forgotFormToken = forgotDoc.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

            var forgotData = new Dictionary<string, string>
            {
                ["Email"] = "user2@trail.local"
            };
            if (!string.IsNullOrEmpty(forgotFormToken))
            {
                forgotData["__RequestVerificationToken"] = forgotFormToken;
            }

            using var forgotPostResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
                client2,
                "/Account/ForgotPassword",
                forgotData,
                forgotHeader,
                forgotToken);

            Assert.True(forgotPostResp.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

            var resetMsg = await WaitForMailpitMessageAsync(mailpitHttpClient, mailpitBaseUrl, "user2@trail.local", cancellationToken);
            var resetBody = resetMsg.GetProperty("HTML").GetString() ?? resetMsg.GetProperty("Text").GetString()!;
            var resetUrl = ExtractUrlPath(resetBody, "/Account/ResetPassword");

            var (resetPageResp, resetDoc, resetHeader, resetFormCsrf) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client2, resetUrl);
            var resetFormToken = resetDoc.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

            var resetQuery = resetUrl.Contains('?') ? resetUrl[resetUrl.IndexOf('?')..] : string.Empty;
            var resetQueryParams = QueryHelpers.ParseQuery(resetQuery);
            var resetUserId = resetQueryParams["userId"].ToString();
            var resetToken = resetQueryParams["token"].ToString();

            var resetPostData = new Dictionary<string, string>
            {
                ["Input.UserId"] = resetUserId,
                ["Input.Token"] = resetToken,
                ["Input.NewPassword"] = "NewPassword456!",
                ["Input.ConfirmNewPassword"] = "NewPassword456!"
            };
            if (!string.IsNullOrEmpty(resetFormToken))
            {
                resetPostData["__RequestVerificationToken"] = resetFormToken;
            }

            using var resetPostResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
                client2,
                "/Account/ResetPassword",
                resetPostData,
                resetHeader,
                resetFormCsrf);

            Assert.True(resetPostResp.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

            var (loginResp2, loginDoc2, loginHeader2, loginToken2) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client2, "/Account/Login");
            var loginFormToken2 = loginDoc2.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

            var loginData2 = new Dictionary<string, string>
            {
                ["Input.Email"] = "user2@trail.local",
                ["Input.Password"] = "NewPassword456!",
                ["Input.RememberMe"] = "true"
            };
            if (!string.IsNullOrEmpty(loginFormToken2))
            {
                loginData2["__RequestVerificationToken"] = loginFormToken2;
            }

            using var loginPostResp2 = await AccountTestHelper.PostFormWithAntiforgeryAsync(
                client2,
                "/Account/Login",
                loginData2,
                loginHeader2,
                loginToken2);

            Assert.Equal(HttpStatusCode.Redirect, loginPostResp2.StatusCode);
        }
        finally
        {
            await mailpit.DisposeAsync();
        }
    }

    [Fact]
    public async Task NoSmtp_EndToEndManualLinkFlow_AutoConfirmsAdminAndUsesManualLinks()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var factory = TrailWebApplicationFactory.Create(DatabaseProvider.Sqlite, new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = "",
            ["Registration:Mode"] = "InviteOnly"
        });

        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        using var userClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var (regResp, regDoc, regHeader, regToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(adminClient, "/Account/Register");
        var regFormToken = regDoc.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var regData = new Dictionary<string, string>
        {
            ["Input.Email"] = "admin@trail.local",
            ["Input.Password"] = "AdminPass123!",
            ["Input.ConfirmPassword"] = "AdminPass123!"
        };
        if (!string.IsNullOrEmpty(regFormToken))
        {
            regData["__RequestVerificationToken"] = regFormToken;
        }

        using var regPostResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            adminClient,
            "/Account/Register",
            regData,
            regHeader,
            regToken);

        Assert.Equal(HttpStatusCode.Redirect, regPostResp.StatusCode);

        var (loginResp, loginDoc, loginHeader, loginToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(adminClient, "/Account/Login");
        var loginFormToken = loginDoc.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var loginData = new Dictionary<string, string>
        {
            ["Input.Email"] = "admin@trail.local",
            ["Input.Password"] = "AdminPass123!",
            ["Input.RememberMe"] = "true"
        };
        if (!string.IsNullOrEmpty(loginFormToken))
        {
            loginData["__RequestVerificationToken"] = loginFormToken;
        }

        using var loginPostResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            adminClient,
            "/Account/Login",
            loginData,
            loginHeader,
            loginToken);

        Assert.Equal(HttpStatusCode.Redirect, loginPostResp.StatusCode);

        var (adminResp, adminDoc, adminHeader, adminToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(adminClient, "/Admin/Users");
        var adminFormToken = adminDoc.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var inviteData = new Dictionary<string, string>
        {
            ["email"] = "invited@trail.local"
        };
        if (!string.IsNullOrEmpty(adminFormToken))
        {
            inviteData["__RequestVerificationToken"] = adminFormToken;
        }

        using var inviteResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            adminClient,
            "/Admin/Users?handler=Invite",
            inviteData,
            adminHeader,
            adminToken);

        Assert.Equal(HttpStatusCode.Redirect, inviteResp.StatusCode);

        var (_, adminAfterInviteDoc, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(adminClient, "/Admin/Users");
        var inviteLinkInput = adminAfterInviteDoc.QuerySelector("#generated-invite-link");
        Assert.NotNull(inviteLinkInput);
        var inviteLink = inviteLinkInput.GetAttribute("value");
        Assert.False(string.IsNullOrWhiteSpace(inviteLink));

        var inviteUrlPath = ExtractUrlPath(inviteLink, "/Account/Register");
        var inviteQuery = inviteUrlPath.Contains('?') ? inviteUrlPath[inviteUrlPath.IndexOf('?')..] : string.Empty;
        var inviteToken = QueryHelpers.ParseQuery(inviteQuery)["inviteToken"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(inviteToken));

        var (userRegResp, userRegDoc, userRegHeader, userRegToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(userClient, inviteUrlPath);
        var userRegFormToken = userRegDoc.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var userRegData = new Dictionary<string, string>
        {
            ["Input.Email"] = "invited@trail.local",
            ["Input.Password"] = "UserPass123!",
            ["Input.ConfirmPassword"] = "UserPass123!",
            ["Input.InviteToken"] = inviteToken
        };
        if (!string.IsNullOrEmpty(userRegFormToken))
        {
            userRegData["__RequestVerificationToken"] = userRegFormToken;
        }

        using var userRegPostResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            userClient,
            "/Account/Register",
            userRegData,
            userRegHeader,
            userRegToken);

        Assert.True(userRegPostResp.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

        Guid invitedUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync("invited@trail.local");
            Assert.NotNull(user);
            invitedUserId = user.Id;
        }

        var (_, adminDoc2, adminHeader2, adminToken2) = await AccountTestHelper.GetDocWithAntiforgeryAsync(adminClient, "/Admin/Users");
        var adminFormToken2 = adminDoc2.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var confirmLinkData = new Dictionary<string, string>
        {
            ["userId"] = invitedUserId.ToString(),
            ["kind"] = "EmailConfirmation"
        };
        if (!string.IsNullOrEmpty(adminFormToken2))
        {
            confirmLinkData["__RequestVerificationToken"] = adminFormToken2;
        }

        using var confirmManualResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            adminClient,
            "/Admin/Users?handler=ManualLink",
            confirmLinkData,
            adminHeader2,
            adminToken2);

        Assert.Equal(HttpStatusCode.Redirect, confirmManualResp.StatusCode);

        var (_, adminAfterConfirmDoc, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(adminClient, "/Admin/Users");
        var manualConfirmLinkInput = adminAfterConfirmDoc.QuerySelector("#generated-manual-link");
        Assert.NotNull(manualConfirmLinkInput);
        var manualConfirmLink = manualConfirmLinkInput.GetAttribute("value");
        Assert.False(string.IsNullOrWhiteSpace(manualConfirmLink));

        var manualConfirmPath = ExtractUrlPath(manualConfirmLink, "/Account/ConfirmEmail");
        using var confirmResp = await userClient.GetAsync(manualConfirmPath, cancellationToken);
        Assert.True(confirmResp.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

        var (_, adminDoc3, adminHeader3, adminToken3) = await AccountTestHelper.GetDocWithAntiforgeryAsync(adminClient, "/Admin/Users");
        var adminFormToken3 = adminDoc3.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var resetLinkData = new Dictionary<string, string>
        {
            ["userId"] = invitedUserId.ToString(),
            ["kind"] = "PasswordReset"
        };
        if (!string.IsNullOrEmpty(adminFormToken3))
        {
            resetLinkData["__RequestVerificationToken"] = adminFormToken3;
        }

        using var resetManualResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            adminClient,
            "/Admin/Users?handler=ManualLink",
            resetLinkData,
            adminHeader3,
            adminToken3);

        Assert.Equal(HttpStatusCode.Redirect, resetManualResp.StatusCode);

        var (_, adminAfterResetDoc, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(adminClient, "/Admin/Users");
        var manualResetLinkInput = adminAfterResetDoc.QuerySelector("#generated-manual-link");
        Assert.NotNull(manualResetLinkInput);
        var manualResetLink = manualResetLinkInput.GetAttribute("value");
        Assert.False(string.IsNullOrWhiteSpace(manualResetLink));

        var manualResetPath = ExtractUrlPath(manualResetLink, "/Account/ResetPassword");
        var (resetPageResp, resetDoc, resetHeader, resetFormCsrf) = await AccountTestHelper.GetDocWithAntiforgeryAsync(userClient, manualResetPath);
        var resetFormToken = resetDoc.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var resetQuery = manualResetPath.Contains('?') ? manualResetPath[manualResetPath.IndexOf('?')..] : string.Empty;
        var resetQueryParams = QueryHelpers.ParseQuery(resetQuery);
        var resetToken = resetQueryParams["token"].ToString();

        var resetPostData = new Dictionary<string, string>
        {
            ["Input.UserId"] = invitedUserId.ToString(),
            ["Input.Token"] = resetToken,
            ["Input.NewPassword"] = "NewManualPass456!",
            ["Input.ConfirmNewPassword"] = "NewManualPass456!"
        };
        if (!string.IsNullOrEmpty(resetFormToken))
        {
            resetPostData["__RequestVerificationToken"] = resetFormToken;
        }

        using var resetPostResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            userClient,
            "/Account/ResetPassword",
            resetPostData,
            resetHeader,
            resetFormCsrf);

        Assert.True(resetPostResp.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

        var (userLoginResp, userLoginDoc, userLoginHeader, userLoginToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(userClient, "/Account/Login");
        var userLoginFormToken = userLoginDoc.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

        var userLoginData = new Dictionary<string, string>
        {
            ["Input.Email"] = "invited@trail.local",
            ["Input.Password"] = "NewManualPass456!",
            ["Input.RememberMe"] = "true"
        };
        if (!string.IsNullOrEmpty(userLoginFormToken))
        {
            userLoginData["__RequestVerificationToken"] = userLoginFormToken;
        }

        using var userLoginPostResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            userClient,
            "/Account/Login",
            userLoginData,
            userLoginHeader,
            userLoginToken);

        Assert.Equal(HttpStatusCode.Redirect, userLoginPostResp.StatusCode);
    }

    [Fact]
    public async Task SessionPersistence_AcrossHostRestart_RetainsAuthenticatedSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var keysDir = Path.Combine(Path.GetTempPath(), "TrailKeys", Guid.NewGuid().ToString("N"));
        var dbDir = Path.Combine(Path.GetTempPath(), "TrailDb", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keysDir);
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "trail.db");

        var hostConfig = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:Default"] = $"Data Source={dbPath}",
            ["DataProtection:KeysPath"] = keysDir,
            ["Email:Smtp:Host"] = "",
            ["Registration:Mode"] = "Open"
        };

        string? capturedAuthCookie = null;

        try
        {
            var host1 = TrailWebApplicationFactory.Create(DatabaseProvider.Sqlite, hostConfig);
            using (var client1 = host1.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            }))
            {
                var (regResp, regDoc, regHeader, regToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client1, "/Account/Register");
                var regFormToken = regDoc.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

                var regData = new Dictionary<string, string>
                {
                    ["Input.Email"] = "persistent@trail.local",
                    ["Input.Password"] = "PersistentPass123!",
                    ["Input.ConfirmPassword"] = "PersistentPass123!"
                };
                if (!string.IsNullOrEmpty(regFormToken))
                {
                    regData["__RequestVerificationToken"] = regFormToken;
                }

                using var regPostResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
                    client1,
                    "/Account/Register",
                    regData,
                    regHeader,
                    regToken);

                Assert.Equal(HttpStatusCode.Redirect, regPostResp.StatusCode);

                var (loginResp, loginDoc, loginHeader, loginToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client1, "/Account/Login");
                var loginFormToken = loginDoc.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");

                var loginData = new Dictionary<string, string>
                {
                    ["Input.Email"] = "persistent@trail.local",
                    ["Input.Password"] = "PersistentPass123!",
                    ["Input.RememberMe"] = "true"
                };
                if (!string.IsNullOrEmpty(loginFormToken))
                {
                    loginData["__RequestVerificationToken"] = loginFormToken;
                }

                using var loginPostResp = await AccountTestHelper.PostFormWithAntiforgeryAsync(
                    client1,
                    "/Account/Login",
                    loginData,
                    loginHeader,
                    loginToken);

                Assert.Equal(HttpStatusCode.Redirect, loginPostResp.StatusCode);

                if (loginPostResp.Headers.TryGetValues("Set-Cookie", out var cookies))
                {
                    foreach (var cookie in cookies)
                    {
                        var parts = cookie.Split(';');
                        foreach (var part in parts)
                        {
                            var trimmed = part.Trim();
                            if (trimmed.StartsWith(".AspNetCore.Identity.Application=", StringComparison.OrdinalIgnoreCase))
                            {
                                capturedAuthCookie = trimmed;
                                break;
                            }
                        }

                        if (capturedAuthCookie is not null)
                        {
                            break;
                        }
                    }
                }

                Assert.NotNull(capturedAuthCookie);

                using var settingsResp1 = await client1.GetAsync("/Account/Settings", cancellationToken);
                Assert.Equal(HttpStatusCode.OK, settingsResp1.StatusCode);
            }

            await host1.DisposeAsync();
            SqliteConnection.ClearAllPools();

            await using var host2 = TrailWebApplicationFactory.Create(DatabaseProvider.Sqlite, hostConfig);
            using var client2 = host2.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            client2.DefaultRequestHeaders.Add("Cookie", capturedAuthCookie);

            using var settingsResp2 = await client2.GetAsync("/Account/Settings", cancellationToken);
            Assert.Equal(HttpStatusCode.OK, settingsResp2.StatusCode);

            using var adminResp2 = await client2.GetAsync("/Admin/Users", cancellationToken);
            Assert.Equal(HttpStatusCode.OK, adminResp2.StatusCode);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (Directory.Exists(keysDir))
                {
                    Directory.Delete(keysDir, recursive: true);
                }
            }
            catch
            {
            }

            try
            {
                if (Directory.Exists(dbDir))
                {
                    Directory.Delete(dbDir, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private static async Task<JsonElement> WaitForMailpitMessageAsync(
        HttpClient httpClient,
        string mailpitBaseUrl,
        string recipientEmail,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await httpClient.GetAsync($"{mailpitBaseUrl}/api/v1/messages", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0)
                {
                    foreach (var msg in messages.EnumerateArray())
                    {
                        if (msg.TryGetProperty("To", out var toArr))
                        {
                            foreach (var to in toArr.EnumerateArray())
                            {
                                if (to.TryGetProperty("Address", out var addr) &&
                                    string.Equals(addr.GetString(), recipientEmail, StringComparison.OrdinalIgnoreCase))
                                {
                                    var id = msg.GetProperty("ID").GetString()!;
                                    var fullMsgResp = await httpClient.GetAsync($"{mailpitBaseUrl}/api/v1/message/{id}", cancellationToken);
                                    fullMsgResp.EnsureSuccessStatusCode();
                                    var fullJson = await fullMsgResp.Content.ReadAsStringAsync(cancellationToken);
                                    return JsonDocument.Parse(fullJson).RootElement.Clone();
                                }
                            }
                        }
                    }
                }
            }

            await Task.Delay(150, cancellationToken);
        }

        throw new TimeoutException($"No message received in Mailpit for {recipientEmail}.");
    }

    private static string ExtractUrlPath(string content, string pathSegment)
    {
        var decoded = HttpUtility.HtmlDecode(content);
        var pattern = @"https?://[^\s""'<>]*" + Regex.Escape(pathSegment) + @"[^\s""'<>]*|" + Regex.Escape(pathSegment) + @"[^\s""'<>]*";
        var match = Regex.Match(decoded, pattern);

        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find URL containing {pathSegment} in content.");
        }

        var matched = match.Value;
        if (Uri.TryCreate(matched, UriKind.Absolute, out var uri))
        {
            return uri.PathAndQuery;
        }

        return matched;
    }
}
