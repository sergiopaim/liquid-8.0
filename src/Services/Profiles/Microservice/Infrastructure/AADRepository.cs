using Liquid;
using Liquid.Base;
using Liquid.Domain;
using Liquid.Platform;
using Liquid.Runtime;
using Microservice.Configuration;
using Microservice.Services;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microservice.Infrastructure
{
    internal class AADRepository : LightHelper
    {
        #region AAD / MS Graph Connection

        public static readonly AuthenticationConfig Config = LightConfigurator.LoadConfig<AuthenticationConfig>("Authentication");

        public static readonly ConfigurationManager<OpenIdConnectConfiguration> ConfigManager = new($"https://login.microsoftonline.com/{Config.AADTenantId}/.well-known/openid-configuration",
                                                                                                    new OpenIdConnectConfigurationRetriever());

        private static readonly IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
                                                                                                    .Create(Config.AADServicePrincipalId)
                                                                                                    .WithTenantId(Config.AADTenantId)
                                                                                                    .WithClientSecret(Config.AADServicePrincipalPassword)
                                                                                                    .Build();

        private static readonly GraphServiceClient msGraph = FactoryNewGraphServiceClient();

        private static readonly int GRAPH_SERVICE_TIMEOUT_IN_MS = 50000;

        private static async Task<string> GetAccessTokenAsync()
        {
            // Client credential flow requires permission scopes on the app registration in aad, then scope is just default
            string[] scopes = ["https://graph.microsoft.com/.default"];

            // Retrieve an access token for Microsoft Graph (gets a fresh token if needed).
            var authResult = await confidentialClientApplication.AcquireTokenForClient(scopes)
                                                                .ExecuteAsync();

            return authResult.AccessToken;
        }

        private static GraphServiceClient FactoryNewGraphServiceClient()
        {
            // Build the Microsoft Graph client. As the authentication provider, set an async lambda
            // which uses the MSAL client to obtain an app-only access token to Microsoft Graph,
            // and inserts this access token in the Authorization header of each API request. 

            var auth = new DelegateAuthenticationProvider(async (requestMessage) =>
                                                                {
                                                                    // Add the access token in the Authorization header of the API request.
                                                                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
                                                                                                                                         await GetAccessTokenAsync());
                                                                });

            var client = new GraphServiceClient(auth);

            return client;
        }

        #endregion

        #region Service methods

        internal static async Task<(DirectoryUserInvitationVM Invitation, string CriticCode)> InviteUserAsync(string name, string email, string redirectUrl)
        {
            Telemetry.TrackEvent("Add AAD Guest User", $"email: {email}");

            try
            {
                using var timeoutToken = new CancellationTokenSource();
                var timeoutTask = Task.Delay(GRAPH_SERVICE_TIMEOUT_IN_MS, timeoutToken.Token);

                var invitationTask = msGraph.Invitations
                                            .Request()
                                            .AddAsync(new Invitation()
                                            {
                                                InvitedUserDisplayName = name,
                                                InvitedUserEmailAddress = email,
                                                InviteRedirectUrl = PlatformServices.ExpandAppUrls(redirectUrl),
                                                SendInvitationMessage = false
                                            },
                                                      timeoutToken.Token);

                if (await Task.WhenAny(invitationTask, timeoutTask) == invitationTask)
                {
                    timeoutToken.Cancel();
                    var invitation = await invitationTask;

                    var toUpdate = new User
                    {
                        PreferredLanguage = "pt-BR"
                    };

                    await UpdateUser(invitation.InvitedUser.Id, toUpdate);

                    return (new()
                    {
                        Id = invitation.InvitedUser.Id,
                        Email = invitation.InvitedUserEmailAddress,
                        Name = invitation.InvitedUserDisplayName,
                        InviteRedeemUrl = invitation.InviteRedeemUrl
                    },
                            null);
                }
                else
                    throw new TimeoutException($"MS Graph invite operation took more than {(int)GRAPH_SERVICE_TIMEOUT_IN_MS / 1000}s to respond");
            }
            catch (ServiceException e)
            {
                if (e.StatusCode == HttpStatusCode.BadRequest &&
                    e.Error.Message.Contains("Group email address is not supported"))
                    return (null, "GROUP_EMAIL_ADDRESS_NOT_SUPPORTED");
                else
                {
                    Telemetry.TrackException(new LightException($"Error while inviting user {email}: status '{e.StatusCode}', error '{e.Error.Message}'", e));
                    return (null, "COULD_NOT_INVITE");
                }
            }
        }

        internal static async Task RemoveUserAsync(string id)
        {
            await DeleteUser(id);
        }

        internal static async Task<List<DirectoryUserSummaryVM>> GetNonGuestUsersByEmailFilterAsync(string tip, List<string> emailFilters)
        {
            tip = tip?.ToLower().Trim().ToEscapedString();
            emailFilters = emailFilters?.Where(e => !string.IsNullOrWhiteSpace(e))
                                        .Select(e => e.ToLower().Trim().ToEscapedString())
                                        .ToList();

            string filter = "userType ne 'guest'";

            if (!string.IsNullOrWhiteSpace(tip))
            {
                tip = tip.ToEscapedString();

                filter += $" and (startswith(displayName,'{tip}') or startswith(mail,'{tip}'))";
            }

            if (emailFilters?.Count > 0)
            {
                if (!string.IsNullOrWhiteSpace(filter))
                    filter += " and ";

                filter += "(endswith(mail,'" +
                          string.Join(@$"') or endswith(mail,'", emailFilters) +
                          "'))";
            }

            List<DirectoryUserSummaryVM> users = await Filter(filter);

            return users;
        }

        internal static async Task<(DirectoryUserSummaryVM User, List<string> Roles)> GetUserAndRolesByIdAsync(string id)
        {
            var users = await Filter($"id eq '{id.ToEscapedString()}'");
            var user = users.FirstOrDefault();

            if (user is not null)
                return (user, AADService.MapRolesFromGroups(await GetMemberGroupsByUserAsync(id)));

            return (null, null);
        }

        internal static async Task<List<DirectoryUserSummaryVM>> GetUsersByIdsAsync(List<string> ids)
        {
            string filter = string.Empty;
            List<string> scapedId = ids.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.ToEscapedString()).ToList();

            if (scapedId.Count == 0)
                return [];

            filter += "(id eq '" +
                      string.Join(@$"' or id eq '", scapedId) +
                      "')";

            var users = await Filter(filter);

            return users;
        }

        internal static async Task<DirectoryUserSummaryVM> GetUserByIdAsync(string id)
        {
            var users = await GetUsersByIdsAsync([id]);

            return users?.FirstOrDefault();
        }

        internal static async Task DeleteUserByIdAsync(string id)
        {
            try
            {
                using var timeoutToken = new CancellationTokenSource();
                var timeoutTask = Task.Delay(GRAPH_SERVICE_TIMEOUT_IN_MS, timeoutToken.Token);

                var deleteTask = msGraph.Users[id].Request().DeleteAsync();

                if (await Task.WhenAny(deleteTask, timeoutTask) == deleteTask)
                    timeoutToken.Cancel();
                else
                    throw new TimeoutException($"MS Graph get operation took more than {(int)GRAPH_SERVICE_TIMEOUT_IN_MS / 1000}s to respond");
            }
            catch (ServiceException e)
            {
                if (e.StatusCode != HttpStatusCode.NotFound)
                    Telemetry.TrackException(new LightException($"Error deleting AAD user '{id}'", e));
            }
        }

        internal static async Task<List<string>> GetMemberGroupsByUserAsync(string userId)
        {
            IDirectoryObjectGetMemberGroupsCollectionPage groups = default;
            try
            {
                using var timeoutToken = new CancellationTokenSource();
                var timeoutTask = Task.Delay(GRAPH_SERVICE_TIMEOUT_IN_MS, timeoutToken.Token);

                var getGroupTask = msGraph.Users[userId].GetMemberGroups(true).Request().PostAsync();

                if (await Task.WhenAny(getGroupTask, timeoutTask) == getGroupTask)
                {
                    timeoutToken.Cancel();
                    groups = await getGroupTask;
                }
                else
                    throw new TimeoutException($"MS Graph get group operation took more than {(int)GRAPH_SERVICE_TIMEOUT_IN_MS / 1000}s to respond");
            }
            catch (ServiceException e)
            {
                if (e.StatusCode != HttpStatusCode.NotFound)
                    Telemetry.TrackException(new LightException("Error acessing Microsoft Graph", e));
            }
            return groups?.Select(g => g).ToList() ?? [];
        }

        internal static async Task RemoveUserGroupsAsync(string userId, List<string> groups)
        {
            if (groups is null)
                return;

            foreach (var group in groups)
                try
                {
                    await msGraph.Groups[group].Members[userId].Reference.Request().DeleteAsync();
                }
                catch (ServiceException e)
                {
                    if (e.StatusCode != HttpStatusCode.NotFound)
                        Telemetry.TrackException(new LightException("Error acessing Microsoft Graph", e));
                }
        }

        internal static async Task AddUserGroupsAsync(string userId, List<string> groups)
        {
            if (groups is null)
                return;

            Telemetry.TrackEvent("Add AAD User To Groups", $"id: {userId} groups: {string.Join(',', groups)}");

            foreach (var group in groups)
                try
                {
                    await msGraph.Groups[group].Members.References.Request().AddAsync(new DirectoryObject() { Id = userId });
                }
                catch (ServiceException e)
                {
                    if (e.Message?.Contains("One or more added object references already exist") == true)
                        continue;

                    Telemetry.TrackException(new LightException($"Error while adding user '{userId}' to AAD group '{group}'", e));
                }
        }

        #endregion

        #region Private methods

        private static readonly int RETRY_DELAY_MS = 1000;
        private static readonly int MAX_RETRIES = 4;

        private static async Task UpdateUser(string userId, User toUpdate, int retry = 0)
        {
            try
            {
                using var timeoutToken = new CancellationTokenSource();
                var timeoutTask = Task.Delay(GRAPH_SERVICE_TIMEOUT_IN_MS, timeoutToken.Token);

                var updateTask = msGraph.Users[userId].Request().UpdateAsync(toUpdate, timeoutToken.Token);

                if (await Task.WhenAny(updateTask, timeoutTask) == updateTask)
                {
                    timeoutToken.Cancel();
                    await updateTask;
                }
                else
                    throw new TimeoutException($"MS Graph update operation took more than {(int)GRAPH_SERVICE_TIMEOUT_IN_MS / 1000}s to respond");
            }
            catch (ServiceException)
            {
                if (retry <= MAX_RETRIES)
                {
                    Thread.Sleep(RETRY_DELAY_MS * retry);
                    await UpdateUser(userId, toUpdate, ++retry);
                }
                else
                    throw;
            }
        }

        private static async Task DeleteUser(string userId, int retry = 0)
        {
            try
            {
                using var timeoutToken = new CancellationTokenSource();
                var timeoutTask = Task.Delay(GRAPH_SERVICE_TIMEOUT_IN_MS, timeoutToken.Token);

                var deleteTask = msGraph.Users[userId].Request().DeleteAsync(timeoutToken.Token);

                if (await Task.WhenAny(deleteTask, timeoutTask) == deleteTask)
                {
                    timeoutToken.Cancel();
                    await deleteTask;
                }
                else
                    throw new TimeoutException($"MS Graph delete operation took more than {(int)GRAPH_SERVICE_TIMEOUT_IN_MS / 1000}s to respond");
            }
            catch (ServiceException e)
            {
                if (e.StatusCode != HttpStatusCode.NotFound)
                    if (retry <= MAX_RETRIES)
                    {
                        Thread.Sleep(RETRY_DELAY_MS * retry);
                        await DeleteUser(userId, ++retry);
                    }
                    else
                        throw;
            }
        }

        private static async Task<List<DirectoryUserSummaryVM>> Filter(string filter)
        {
            //**The following code should work but the MS Graph SDK and/ or its documentation is buggy
            //* https://github.com/microsoftgraph/microsoft-graph-docs/issues/4331
            //*

            //var request = msGraph.Users.Request().Filter(filter);
            //request.Headers.Add(new HeaderOption("ConsistencyLevel", "Eventual"));

            //var users = await request.GetAsync();

            //return users.ToList();

            //*
            //*So we had to make a REST workaround as bellow

            List<DirectoryUserSummaryVM> users = [];

            var client = new HttpClient();

            foreach (var header in msGraph.Users.Request().Headers)
                client.DefaultRequestHeaders.Add(header.Name, header.Value);

            client.DefaultRequestHeaders.Add("ConsistencyLevel", "Eventual");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync());

            string uri = msGraph.Users.RequestUrl + $"?$count=true&$select=displayName,mail,otherMails,id,createdDateTime,externalUserState&$filter={filter}";
            var response = await client.GetAsync(uri);

            var value = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
                                    .Property("value");

            if (value.ValueKind == JsonValueKind.Array)
                foreach (var jObj in value.EnumerateArray())
                    try
                    {
                        users.Add(FactoryUserSummaryFrom(jObj));
                    }
                    catch (Exception e)
                    {
                        Telemetry.TrackTrace(jObj.ToJsonString(true));
                        Telemetry.TrackException(new LightException("Error while trying to read user from AAD user array", e));
                    }
            else if (value.ValueKind == JsonValueKind.Object)
                try
                {
                    users.Add(FactoryUserSummaryFrom(value));
                }
                catch (Exception e)
                {
                    Telemetry.TrackTrace(value.ToJsonString(true));
                    Telemetry.TrackException(new LightException("Error while trying to read user from AAD user object", e));
                }
            else if (value.ValueKind != JsonValueKind.String) // ignoring empty strings meaning not found
            {
                Telemetry.TrackTrace(value.ToJsonString(true));
                Telemetry.TrackException(new LightException("Could not read users from AAD user response"));
            }

            return users;
        }

        private static DirectoryUserSummaryVM FactoryUserSummaryFrom(JsonElement jObj)
        {
            DirectoryUserSummaryVM summaryUser = new()
            {
                Id = jObj.Property("id").AsString(),
                Name = jObj.Property("displayName").AsString(),
                Email = jObj.Property("mail").AsString()?.ToLower(),
                OtherMails = jObj.Property("otherMails").EnumerateArray().Select(o => o.AsString().ToLower()).ToList()
            };

            string externalUserState = jObj.Property("externalUserState").AsString();

            summaryUser.CreatedAt = jObj.Property("createdDateTime").AsDateTime();
            summaryUser.UpdatedAt = summaryUser.CreatedAt;

            if (externalUserState == "Accepted")
                summaryUser.InviteStatus = InviteStatus.Accepted;
            else if (externalUserState == "PendingAcceptance")
                summaryUser.InviteStatus = InviteStatus.Pending;

            return summaryUser;
        }

        #endregion
    }
}