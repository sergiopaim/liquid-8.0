using Liquid;
using Liquid.Base;
using Liquid.Domain;
using Liquid.Platform;
using Microservice.Infrastructure;
using Microservice.Models;
using Microservice.ViewModels;
using Microsoft.Graph;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Microservice.Services
{
    internal class AADService : LightService
    {
        #region Service Operations

        public async Task<ClaimsPrincipal> GetClaimsFromIdTokenAsync(string idToken)
        {
            if (await ValidateIdTokenAsync(idToken))
                try
                {
                    var userClaims = DecodeIdToken(idToken);

                    if (userClaims.FindFirstValue("hasgroups") == "true")
                        userClaims = ReplaceGroupsByRoles(userClaims, LoadGroupsFromMSGraphAsync(userClaims).Result);
                    else
                        userClaims = ReplaceGroupsByRoles(userClaims, userClaims.Claims
                                                                                .Where(c => c.Type == "groups")
                                                                                .Select(g => MapRoleFromGroup(g.Value)));

                    return userClaims;
                }
                //An exception thrown while decoding means an malformed token
                catch (LightException)
                {
                    BadRequest("malformed token");
                }

            return null;
        }

        public async Task<DomainResponse> FilterAADUsersByEmailFilterAsync(string tip, List<string> emailFilters, bool guestOnly)
        {
            List<DirectoryUserSummaryVM> users;

            if (guestOnly)
                //Guests are only invited from Profiles and managed by it, so it's mandatory to query locally
                users = await FilterUsersLocallyAsync(tip, emailFilters);
            else
                //Otherwise, the query should be on the AAD itself
                users = await AADRepository.GetNonGuestUsersByEmailFilterAsync(tip, emailFilters);

            return Response(users);
        }

        public async Task<DomainResponse> GetAADUserByIdAsync(string id)
        {
            var user = await GetUserByIdLocallyAsync(id);

            if (user is null)
                return NoContent();

            return Response(user);
        }

        public async Task<DomainResponse> SetDirectoryUserCreatedAtAsync(string id, DateTime date)
        {
            var user = await SetDirectoryLocalUserCreatedAtAsync(id, date);

            if (user is null)
                return NoContent();

            return Response();
        }

        public async Task<DomainResponse> GetAADUsersByIdsAsync(List<string> ids)
        {
            return Response(await GetUsersByIdsLocallyAsync(ids));
        }

        public async Task<DomainResponse> GetAADUserFromOriginByIdAsync(string id)
        {
            var user = await GetUserByIdFromOriginAsync(id);

            if (user is null)
                return NoContent();

            return Response(user);
        }

        public async Task<DomainResponse> GetAADUsersFromOriginByIdsAsync(List<string> ids)
        {
            return Response(await GetUsersByIdsFromOriginAsync(ids));
        }

        public async Task<DomainResponse> UpdateUserRolesAsync(string id, List<string> roles)
        {
            var newGroups = MapGroupsFromRoles(roles);
            var oldGroups = await AADRepository.GetMemberGroupsByUserAsync(id);

            var toRemove = oldGroups.Where(g => !newGroups.Contains(g)).ToList();
            var toAdd = newGroups.Where(g => !oldGroups.Contains(g)).ToList();

            await AADRepository.RemoveUserGroupsAsync(id, toRemove);
            await AADRepository.AddUserGroupsAsync(id, toAdd);

            var profile = await Service<ProfileService>().UpdateRolesForAADUserAsync(id, roles);

            return Response(ProfileVM.FactoryFrom(profile));
        }

        public async Task<DomainResponse> InactivateUserAsync(string id)
        {
            Telemetry.TrackEvent("Inactivate AAD user", id);

            Profile profile = await Service<ProfileService>().InactivateUserAsync(id);

            if (profile is null)
                return NoContent();

            await AADRepository.DeleteUserByIdAsync(id);

            return Response(FactoryDirectoryUserSummaryVM(profile));
        }

        public async Task<DomainResponse> InviteUserAsync(string name, string email, string role, string redirectUrl)
        {
            Telemetry.TrackEvent("Invite AAD User", $"email: {email} role:{role}");

            name = name.Trim().Replace("  ", " ").Replace("  ", " ").Replace("  ", " ");
            email = email.Trim().ToLower();

            var existing = Repository.Get<Profile>(filter: p => p.Channels.Email == email ||
                                                                p.Channels.EmailToChange == email,
                                                   orderBy: p => p.CreatedAt,
                                                   descending: true).FirstOrDefault();

            if (existing is not null)
            {
                if (!existing.IsFromAAD)
                    return BusinessError("CANNOT_INVITE_MEMBERS_AS_ADD", email);

                else if (existing.Status == ProfileStatus.Active.Code ||
                         existing.Status == ProfileStatus.Invited.Code)
                    return BusinessError("USER_OF_SAME_EMAIL", email);

                else if (existing.Banned == true)
                    return BusinessError("USER_EMAIL_BANNED", email);
            }

            var (invitation, criticCode) = await AADRepository.InviteUserAsync(name, email, redirectUrl);

            if (invitation is null)
                return BusinessError(criticCode);

            if (!string.IsNullOrWhiteSpace(criticCode))
                AddBusinessWarning(criticCode);

            await AADRepository.AddUserGroupsAsync(invitation.Id, [MapGroupFromRole(role)]);

            await Service<ProfileService>().UpsertProfileForAADUserAsync(DirectoryUserSummaryVM.FactoryFrom(invitation), 
                                                                         [role], 
                                                                         fromInvitation: true);

            return Response(invitation);
        }

        internal async Task<DomainResponse> UnbanUserAsync(string userId)
        {
            Telemetry.TrackEvent("Unban Profile", userId);

            var profile = await Repository.GetByIdAsync<Profile>(userId);

            if (profile is null || !profile.IsAADGuest)
                return NoContent();

            if (profile.Status != ProfileStatus.Inactive.Code)
                return BusinessError("INVALID_STATUS");

            profile.Unban();

            await Repository.UpdateAsync(profile);

            return Response(FactoryDirectoryUserSummaryVM(profile));
        }

        internal static async Task<(DirectoryUserSummaryVM User, List<string> Roles)> GetUserAndRolesFromAADByIdAsync(string id)
        {
            return await AADRepository.GetUserAndRolesByIdAsync(id);
        }

        internal static async Task<DirectoryUserSummaryVM> GetUserByIdLocallyAsync(string id)
        {
            return await GetUserLocallyAsync(id);
        }

        internal static async Task<Profile> SetDirectoryLocalUserCreatedAtAsync(string id, DateTime date)
        {
            var profile = await Repository.GetByIdAsync<Profile>(id);

            if (profile is not null)
            { 
                profile.CreatedAt = date;
                await Repository.UpdateAsync(profile);
            }

            return profile;
        }

        internal static async Task<DirectoryUserSummaryVM> GetUserByIdFromOriginAsync(string id)
        {
            return await GetUserFromOriginAsync(id);
        }

        internal static async Task<List<DirectoryUserSummaryVM>> GetUsersByIdsLocallyAsync(List<string> ids)
        {
            return await GetUsersLocallyAsync(ids);
        }

        internal static async Task<List<DirectoryUserSummaryVM>> GetUsersByIdsFromOriginAsync(List<string> ids)
        {
            return await GetUsersFromOriginAsync(ids);
        }

        internal static async Task RemoveAADUserAsync(string id)
        {
            await AADRepository.RemoveUserAsync(id);
        }

        #endregion

        #region AAD Users

        private static async Task<DirectoryUserSummaryVM> GetUserLocallyAsync(string id)
        {
            var users = await GetUsersLocallyAsync([id]);
            return users?.FirstOrDefault();
        }

        private static async Task<List<DirectoryUserSummaryVM>> GetUsersLocallyAsync(List<string> ids)
        {
            ids?.RemoveAll(string.IsNullOrWhiteSpace);

            if (!(ids?.Count > 0))
                return [];

            string sql = $@"SELECT {GetLocalUserColumns()}
                            FROM <{nameof(Profile)}> AS c
                            JOIN a IN c.accounts
                            WHERE c.id IN ('{string.Join("','", ids)}')
                              AND a.source = 'aAD'";

            var profiles = await Repository.QueryAsync<Profile>(sql);

            return profiles?.Select(FactoryDirectoryUserSummaryVM)
                            .ToList();
        }

        private static string GetLocalUserColumns()
        {
            return "c.id, c.name, c.channels, c.status, c.banned, c.banMotive, c.createdAt, c.lastSignedinAt, c.inactivatedAt";
        }

        private static async Task<DirectoryUserSummaryVM> GetUserFromOriginAsync(string id)
        {
            var originUser = await AADRepository.GetUserByIdAsync(id);
            if (originUser is null)
                return null;

            var localUser = await GetUserByIdLocallyAsync(id);
            if (localUser is null)
                return null;

            localUser.OtherMails = originUser.OtherMails;

            return localUser;
        }

        private static async Task<List<DirectoryUserSummaryVM>> GetUsersFromOriginAsync(List<string> ids)
        {
            ids?.RemoveAll(string.IsNullOrWhiteSpace);

            var originUsers = await AADRepository.GetUsersByIdsAsync(ids);
            var localUsers = await GetUsersByIdsLocallyAsync(ids);

            localUsers.ForEach(l => l.OtherMails = originUsers.FirstOrDefault(o => o.Id == l.Id)?.OtherMails);

            return localUsers;
        }

        private static DirectoryUserSummaryVM FactoryDirectoryUserSummaryVM(Profile profile)
        {
            DirectoryUserSummaryVM user = new()
            {
                Id = profile.Id,
                Name = profile.Name,
                Email = profile.Channels.Email,
                CreatedAt = profile.CreatedAt,
                BanMotive = profile.BanMotive,
            };

            if (profile.Status == ProfileStatus.Inactive.Code)
            {
                user.UpdatedAt = profile.InactivatedAt ?? profile.CreatedAt;
                if (profile.Banned == true)
                    user.InviteStatus = InviteStatus.Bounced;
                else
                    user.InviteStatus = InviteStatus.Reinvite;
            }
            else if (profile.Status == ProfileStatus.Invited.Code)
            {
                user.UpdatedAt = profile.CreatedAt;
                user.InviteStatus = InviteStatus.Pending;
            }
            else if (profile.Status == ProfileStatus.Active.Code)
            {
                user.UpdatedAt = profile.LastSignedinAt ?? profile.CreatedAt;
                user.InviteStatus = InviteStatus.Accepted;
            }

            return user;
        }

        private async Task<List<DirectoryUserSummaryVM>> FilterUsersLocallyAsync(string tip, List<string> emailFilters)
        {
            tip = tip?.ToLower().Trim().ToEscapedString();
            emailFilters = emailFilters?.Where(e => !string.IsNullOrWhiteSpace(e))
                                        .Select(e => e.ToLower().Trim().ToEscapedString())
                                        .ToList();

            string sql = $@"SELECT {GetLocalUserColumns()}
                            FROM <{nameof(Profile)}> AS c
                            JOIN a IN c.accounts
                            WHERE (NOT IS_DEFINED(c.banned) OR NOT c.banned)
                              AND a.source = 'aAD'";

            if (!string.IsNullOrWhiteSpace(tip))
                sql += $@"
                            AND (LOWER(c.name) LIKE '%{tip}%' OR LOWER(c.channels.email) LIKE '%{tip}%')";

            if (emailFilters?.Count > 0)
                sql += $@"
                            AND (c.channels.email LIKE '%{string.Join("' OR c.channels.email LIKE '%", emailFilters)}')";

            var profiles = await Repository.QueryAsync<Profile>(sql);

            return profiles?.Select(FactoryDirectoryUserSummaryVM).ToList();
        }

        #endregion

        #region Token handling

        private static ClaimsPrincipal DecodeIdToken(string jwt)
        {
            ClaimsIdentity claims = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(jwt))
                {
                    claims = new ClaimsIdentity(new JwtSecurityToken(jwtEncodedString: jwt).Claims, "Custom");

                    var oid = claims.FindFirst("oid");

                    claims.RemoveClaim(claims.FindFirst(Liquid.Runtime.JwtClaimTypes.UserId));
                    claims.AddClaim(new Claim(Liquid.Runtime.JwtClaimTypes.UserId, oid.Value));

                    claims.RemoveClaim(oid);
                }
            }
            catch (Exception e)
            {
                throw new LightException("Error when trying to decode JWT into User.Claims", e);
            }

            if (claims is null)
            {
                return null;
            }
            else
            {
                //Validation PASSED
                return new(claims);
            }

        }

        private async Task<bool> ValidateIdTokenAsync(string jwt)
        {
            bool forceValidate = false;

            if (!forceValidate && (WorkBench.IsDevelopmentEnvironment || WorkBench.IsIntegrationEnvironment || WorkBench.IsQualityEnvironment || WorkBench.IsDemonstrationEnvironment))
                return true;

            // GetAsync Config from AAD according to the configManager's refresh parameter
            var AADConfig = await AADRepository.ConfigManager.GetConfigurationAsync();

            var validationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidAudience = AADRepository.Config.AADServicePrincipalId,
                ValidIssuer = $"https://login.microsoftonline.com/{AADRepository.Config.AADTenantId}/v2.0",
                IssuerSigningKeys = AADConfig.SigningKeys
            };

            try
            {
                //Throws an Exception as the token is invalid (expired, invalid-formatted, etc.)
                new JwtSecurityTokenHandler().ValidateToken(jwt, validationParameters, out var validatedToken);
                return validatedToken is not null;
            }
            catch (SecurityTokenException e)
            {
                string invalidationError = $"'{e.Message.Split(".")[0]}'";
                BadRequest($"token is invalid by the motive {invalidationError}");

                Telemetry.TrackException(new LightException($"POSSIBLE THREAT! Attempt to authenticate with invalid token_id: error {invalidationError} from token '{jwt}'"));
                return false;
            }
            catch (ArgumentException e)
            {
                BadRequest($"token is invalid by the motive {e.Message}");

                return false;
            }
        }

        #endregion

        #region Group>Role mapping

        private static async Task<IEnumerable<Claim>> LoadGroupsFromMSGraphAsync(ClaimsPrincipal userClaims)
        {
            var userId = userClaims.FindFirstValue(Liquid.Runtime.JwtClaimTypes.UserId);

            try
            {
                var groups = await AADRepository.GetMemberGroupsByUserAsync(userId);
                return groups.Select(MapRoleFromGroup).ToList();
            }
            catch (ServiceException e)
            {
                throw GraphApiExceptionFor(userId, e);
            }
        }

        private static ClaimsPrincipal ReplaceGroupsByRoles(ClaimsPrincipal userClaims, IEnumerable<Claim> roles)
        {
            var identity = new ClaimsIdentity(userClaims.Claims);

            var claimsToRemove = new List<Claim>();
            foreach (var hasGroups in identity.Claims.Where(c => c.Type == "hasgroups"))
                claimsToRemove.Add(hasGroups);

            foreach (var group in identity.Claims.Where(c => c.Type == "groups"))
                claimsToRemove.Add(group);

            foreach (var claim in claimsToRemove)
                identity.RemoveClaim(claim);

            foreach (var role in roles)
                if (role is not null)
                    identity.AddClaim(role);

            return new(identity);
        }

        internal static Claim MapRoleFromGroup(string groupId)
        {
            if (AADRepository.Config?.AADGroupsAsRoles is null)
                return null;

            var groupAsRole = AADRepository.Config.AADGroupsAsRoles.Find(x => x.GroupObjectId == groupId);

            if (groupAsRole is not null)
                return new(ClaimsIdentity.DefaultRoleClaimType, groupAsRole.RoleName);
            else
                return null;
        }

        internal static List<string> MapRolesFromGroups(List<string> groups)
        {
            var roles = new List<string>();
            foreach (var group in groups)
            {
                var role = MapRoleFromGroup(group);
                if (role is not null)
                    roles.Add(role.Value);
            }

            return roles;
        }

        private static string MapGroupFromRole(string role)
        {
            if (AADRepository.Config?.AADGroupsAsRoles is null)
                return null;

            var groupAsRole = AADRepository.Config.AADGroupsAsRoles.Find(x => x.RoleName == role);

            if (groupAsRole is not null)
                return groupAsRole.GroupObjectId;
            else
                return null;
        }

        private static List<string> MapGroupsFromRoles(List<string> roles)
        {
            var groups = new List<string>();
            foreach (var role in roles)
            {
                var group = MapGroupFromRole(role);
                if (group is not null)
                    groups.Add(group);
            }

            return groups;
        }

        #endregion

        #region Exceptions

        private static LightException GraphApiExceptionFor(string userId, ServiceException e)
        {
            return new LightException(LightLocalizer.Localize("CANNOT_ACCESS_USER_IN_GRAPH_API", userId, AADRepository.Config.AADTenantId), e);
        }

        internal static List<string> GetRolesToUpdate(Profile profile)
        {
            if (!profile.IsFromAAD)
                return null;

            var account = profile.Accounts.FirstOrDefault();
            var currentRoles = MapRolesFromGroups(AADRepository.GetMemberGroupsByUserAsync(profile.Id).Result);

            if (account.Roles.Except(currentRoles).Any() || currentRoles.Except(account.Roles).Any())
                return currentRoles;

            return null;
        }

        #endregion
    }
}