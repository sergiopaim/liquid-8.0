using Liquid;
using Liquid.Base;
using Liquid.Domain;
using Liquid.OnAzure;
using Liquid.Platform;
using Liquid.Repository;
using Microservice.Infrastructure;
using Microservice.Messages;
using Microservice.Models;
using Microservice.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microservice.Services
{
    internal class ProfileService : LightService
    {
        private const string DEFAULT_TIMEZONE = "America/Sao_Paulo";
        private const string DEV_DOMAIN = "@your-dev-domain.onmicrosoft.com";
        private const string PRD_DOMAIN = "@your-domain.com";

        static readonly MessageBus<ServiceBus> userProfileBus = new("TRANSACTIONAL", "user/profiles");

        public async Task<DomainResponse> GetOfCurrentAccountAsync()
        {
            Telemetry.TrackEvent("Get Current Profile", CurrentUserId);

            var profile = await Repository.GetByIdAsync<Profile>(CurrentUserId);

            if (profile is null)
                return NoContent();

            return Response(profile.FactoryVM());
        }

        public async Task<DomainResponse> GetPendingChangesOfCurrentAccountAsync()
        {
            Telemetry.TrackEvent("Get Current Profile With Pending Changes", CurrentUserId);

            var profile = await Repository.GetByIdAsync<Profile>(CurrentUserId);

            if (profile is null)
                return NoContent();

            return Response(profile.FactoryWithPendingChangesVM());
        }

        public async Task<DomainResponse> UpdateOfCurrentAccountAsync(EditProfileVM edit, int? tryNum = 1)
        {
            Telemetry.TrackEvent("Update Current Profile", $"id: {CurrentUserId} tryNum:{tryNum}");

            var toUpdate = await Repository.GetByIdAsync<Profile>(CurrentUserId);

            if (toUpdate is null)
            {
                Account currentAccount = Account.FactoryFromAADClaims(SessionContext.User);
                if (currentAccount.Source == AccountSource.AAD.Code)
                    toUpdate = await CreateProfileForAADClaimsAsync(SessionContext.User);
                else
                    return NoContent();
            }

            if (!CheckEmailDomain(toUpdate, edit))
                return Response();

            if (toUpdate.Accounts.First().Source == AccountSource.AAD.Code &&
                toUpdate.Channels.WillChangeFrom(edit))
                return BadRequest("Its not allowed to update channels (phone and email) of AAD users");

            if (toUpdate.Channels.WillRemoveAnyFrom(edit))
                return BadRequest("Its not allowed to remove an already defined channel (phone or email)");

            CheckAlternateKeys(toUpdate.Id,
                               toUpdate.Channels.Email,
                               toUpdate.Channels.Phone,
                               edit.Email,
                               edit.Phone);

            if (HasBusinessErrors)
                return Response();
            else
            {
                var before = ComparableProfileVM.FactoryFrom(toUpdate);
                toUpdate.MapFromEditVM(edit);

                if (HasBusinessErrors)
                    return Response();
                else
                {
                    Service<ChannelService>().ControlChannelUpdates(before, toUpdate);

                    Profile updated;
                    try
                    {
                        updated = await Repository.UpdateAsync(toUpdate);

                        Service<ChannelService>().RequestValidationOfUpdatedChannels(before, updated);

                        await NotifySubscribersOfChangesBetween(before, updated);
                    }
                    catch (OptimisticConcurrencyLightException)
                    {
                        if (tryNum <= 3)
                            return await UpdateOfCurrentAccountAsync(edit, ++tryNum);
                        else
                            throw;
                    }

                    return Response(updated?.FactoryVM());
                }
            }
        }

        private bool CheckEmailDomain(Profile current, EditProfileVM edit)
        {
            if (current.Channels.Email != edit.Email?.ToLower() && !EmailAddress.CheckDomain(edit.Email))
            {
                AddBusinessError("EMAIL_DOMAIN_INVALID", edit.Email.Split('@').LastOrDefault());
                return false;
            }

            return true;
        }

        public async Task<DomainResponse> RevertChannelByIdAsync(JsonDocument token, int? tryNum = 1)
        {
            var toUpdate = await Repository.GetByIdAsync<Profile>(token.Property("id").AsString());
            if (toUpdate is null)
            {
                Telemetry.TrackEvent("Revert Profile Channel Update", $"id:null tryNum:{tryNum}");
                return NoContent();
            }

            Telemetry.TrackEvent("Revert Profile Channel Update", $"id:{toUpdate.Id} tryNum:{tryNum}");

            var before = ComparableProfileVM.FactoryFrom(toUpdate);

            Service<ChannelService>().RevertUpdate(toUpdate, token.Property("otp").AsString());

            CheckAlternateKeys(toUpdate.Id,
                               before.Channels.Email,
                               before.Channels.Phone,
                               toUpdate.Channels.Email,
                               toUpdate.Channels.Phone);

            if (HasBusinessErrors)
                return BusinessWarning("UNABLE_TO_REVERT_UPDATE");
            else
            {
                try
                {
                    var updated = await Repository.UpdateAsync(toUpdate);

                    await NotifySubscribersOfChangesBetween(before, updated);

                    return Response(updated.FactoryVM());
                }
                catch (OptimisticConcurrencyLightException)
                {
                    if (tryNum <= 3)
                        return await RevertChannelByIdAsync(token, ++tryNum);
                    else
                        throw;
                }
            }
        }

        public async Task<DomainResponse> ValidateMyChannelAsync(string channelType, string validationOTP, int? tryNum = 1)
        {
            Telemetry.TrackEvent($"Validate Current Profile Channel {channelType}", $"id: {CurrentUserId} tryNum:{tryNum}");

            var toUpdate = await Repository.GetByIdAsync<Profile>(CurrentUserId);
            if (toUpdate is null)
                return NoContent();

            var before = ComparableProfileVM.FactoryFrom(toUpdate);

            Service<ChannelService>().ValidateRequest(toUpdate, channelType, validationOTP);

            if (HasBusinessErrors)
                return Response();
            else
            {
                Profile updated;
                try
                {
                    updated = await Repository.UpdateAsync(toUpdate);

                    await NotifySubscribersOfChangesBetween(before, updated);
                }
                catch (OptimisticConcurrencyLightException)
                {
                    if (tryNum <= 3)
                        return await ValidateMyChannelAsync(channelType, validationOTP, ++tryNum);
                    else
                        throw;
                }
                return Response(updated.FactoryVM());
            }
        }

        public async Task<DomainResponse> UpdateByIdAsync(string id, EditProfileVM edit, int? tryNum = 1)
        {
            Telemetry.TrackEvent("Update Profile By Id", $"id: {id} tryNum:{tryNum}");

            var toUpdate = await Repository.GetByIdAsync<Profile>(id);

            if (toUpdate is null)
                return NoContent();

            if (!CheckEmailDomain(toUpdate, edit))
                return Response();

            if (toUpdate.Accounts.First().Source == AccountSource.AAD.Code &&
                toUpdate.Channels.WillChangeFrom(edit))
                return BadRequest("Its not allowed to update channels (phone and email) of AAD users");

            if (toUpdate.Channels.WillRemoveAnyFrom(edit))
                return BadRequest("Its not allowed to remove an already defined channel (phone or email)");

            CheckAlternateKeys(toUpdate.Id,
                               toUpdate.Channels.Email,
                               toUpdate.Channels.Phone,
                               edit.Email,
                               edit.Phone);

            if (HasBusinessErrors)
                return Response();
            else
            {
                Profile updated;
                try
                {
                    var before = ComparableProfileVM.FactoryFrom(toUpdate);
                    toUpdate.MapFromEditVM(edit);

                    updated = await Repository.UpdateAsync(toUpdate);

                    await NotifySubscribersOfChangesBetween(before, updated);

                    return Response(updated.FactoryVM());
                }
                catch (OptimisticConcurrencyLightException)
                {
                    if (tryNum <= 3)
                        return await UpdateByIdAsync(id, edit, ++tryNum);
                    else
                        throw;
                }
            }
        }

        public async Task<DomainResponse> GetPendingChangesByIdAsync(string id)
        {
            Telemetry.TrackEvent("Get Profile With Pending Changes By Id", id);

            var profile = await GetAsync(id);

            if (profile is null)
                return NoContent();

            return Response(profile.FactoryWithPendingChangesVM());
        }

        public DomainResponse GetByEmail(string email)
        {
            Telemetry.TrackEvent("Get User by email", email);

            email ??= "";

#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
            var profile = Repository.Get<Profile>(filter: p => p.Channels.Email == email.ToLower(),
                                                  orderBy: p => p.CreatedAt,
                                                  descending: true)
                                    .FirstOrDefault();
#pragma warning restore CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons

            if (profile is null)
                return NoContent();

            return Response(profile.FactoryVM());
        }

        internal DomainResponse GetIdByChannel(string channel)
        {
            Telemetry.TrackEvent("Get Profile Id By Channel", channel);

            channel ??= "";

#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
            var profile = Repository.Get<Profile>(p => p.Channels.Email == channel.ToLower() ||
                                                       p.Channels.Phone == channel)
                                    .FirstOrDefault();
#pragma warning restore CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons

            if (profile is null ||
                profile.Accounts.First().Source == AccountSource.AAD.Code)
                return NoContent();

            return Response(profile.FactoryBasicVM());
        }

        public async Task<DomainResponse> GetByIdAsync(string id, bool onlyIM)
        {
            Telemetry.TrackEvent("Get Profile By Id", id);

            var profile = await GetAsync(id, onlyIM);

            if (profile is null)
                return NoContent();

            return Response(profile.FactoryBasicVM());
        }

        public async Task<DomainResponse> GetByIdsAsync(List<string> ids)
        {
            Telemetry.TrackEvent("Get Profile By Ids", $"ids: {string.Join(",", ids)}");
            List<ProfileBasicVM> vms = [];

            ids?.RemoveAll(string.IsNullOrWhiteSpace);

            if (ids is null || ids.Count == 0 || ids.Any(string.IsNullOrWhiteSpace))
                return Response(vms);

            var profiles = Repository.Get<Profile>(p => ids.Contains(p.Id), orderBy: p => p.Name);

            foreach (var profile in profiles)
                vms.Add(profile.FactoryBasicVM());

            //Checks if not found users haven't been created (invited) on AAD directly and, if so, creates them
            foreach (var id in ids.Where(i => !vms.Any(p => p.Id == i)))
            {
                var (user, roles) = await AADService.GetUserAndRolesFromAADByIdAsync(id);

                if (user is not null)
                {
                    var profile = await UpsertProfileForAADUserAsync(user, roles);

                    if (profile is not null)
                        vms.Add(profile.FactoryBasicVM());
                }
            }

            return Response(vms);
        }

        public DomainResponse GetByRole(string role, bool all)
        {
            Telemetry.TrackEvent("Get Profile by role", role);

            var profiles = Repository.Get<Profile>(p => p.Accounts.Any(a => a.Roles.Any(r => r == role)) &&
                                                        (all || p.Status == ProfileStatus.Active.Code),
                                                   p => p.Name);

            List<ProfileVM> vms = [];
            foreach (var profile in profiles)
                vms.Add(profile.FactoryVM());

            return Response(vms);
        }

        public DomainResponse GetByRoles(List<string> roles, bool all)
        {
            Telemetry.TrackEvent("Get users by roles", string.Join(", ", roles));

            return Response(GetUsersByRoles(roles, all));
        }

        private static List<ProfileVM> GetUsersByRoles(List<string> roles, bool all = false)
        {
            var profiles = Repository.Get<Profile>(p => p.Accounts.Any(a => a.Roles.Any(r => roles.Contains(r))) &&
                                                        (all || p.Status == ProfileStatus.Active.Code),
                                                   p => p.Name);

            List<ProfileVM> vms = [];
            foreach (var profile in profiles)
                vms.Add(profile.FactoryVM());

            return vms;
        }

        public async Task<DomainResponse> GetCurrentAccountAsync()
        {
            Telemetry.TrackEvent("Get Current Account", CurrentUserId);

            var profile = await Repository.GetByIdAsync<Profile>(CurrentUserId);

            if (profile is null)
            {
                Account current = Account.FactoryFromAADClaims(SessionContext.User);
                if (current?.Source == AccountSource.AAD.Code)
                {
                    profile = await CreateProfileForAADClaimsAsync(SessionContext.User);

                    if (profile is null)
                        return NoContent();
                }
                else
                    return NoContent();
            }

            return Response(AccountVM.FactoryFrom(profile.Accounts.First()));
        }

        public async Task<DomainResponse> DeleteMeAsync(string feedback)
        {
            Telemetry.TrackEvent("Delete Profile of Current Account", CurrentUserId);

            var profile = await Repository.GetByIdAsync<Profile>(CurrentUserId);
            if (profile is null)
                return NoContent();

            await Repository.DeleteAsync<Profile>(profile.Id);

            SendLogoutDomainEvent(profile);

            await NotifySubscribersAsync(profile, ProfileCMD.Delete);
            NotifyBackOfficeManagersOfOptOut(profile, feedback);

            return Response(profile.FactoryVM());
        }

        public async Task<DomainResponse> CreateOrUpdateWithOTPAsync(ProfileVM toCreate, int? tryNum = 1)
        {
            bool updating = false;

            Telemetry.TrackEvent("Create User With OTP", $"email: {toCreate.Email} tryNum:{tryNum}");

            string eTag = null;

            var existing = await Repository.GetByIdAsync<Profile>(toCreate.Id);
            if (existing is not null)
            {
                //Updates the newProfile so to get existing data so the method could continue as if it was a newProfile
                var toMerge = existing.FactoryVM();
                eTag = existing.ETag;
                toMerge.MapFrom(toCreate);
                toCreate = toMerge;

                updating = true;
            }
            else
            {
                //The new user language is currently based on the inviter's
                toCreate.Language = CultureInfo.CurrentUICulture.Name;

                //If not informed, the timeZone is the default one
                if (string.IsNullOrWhiteSpace(toCreate.TimeZone))
                    toCreate.TimeZone = DEFAULT_TIMEZONE;
            }

            CheckAlternateKeys(existing?.Id,
                               existing?.Channels.Email,
                               existing?.Channels.Phone,
                               toCreate.Email,
                               toCreate.Phone);

            if (HasBusinessErrors)
                return Response();
            else
            {
                var toSave = Profile.FactoryFrom(toCreate);

                toSave.AddAccount(AccountSource.IM.Code, AccountIMRole.Member.Code);
                toSave.GenerateNewOTP();

                Profile saved;
                if (updating)
                {
                    try
                    {
                        toSave.ETag = eTag;
                        saved = await Repository.UpdateAsync(toSave);

                        AddBusinessInfo("USER_PROFILE_UPDATED");

                        await NotifySubscribersOfChangesBetween(ComparableProfileVM.FactoryFrom(existing), saved);
                    }
                    catch (OptimisticConcurrencyLightException)
                    {
                        if (tryNum <= 3)
                            return await CreateOrUpdateWithOTPAsync(toCreate, ++tryNum);
                        else
                            throw;
                    }
                }
                else
                {
                    saved = await Repository.AddAsync(toSave);

                    AddBusinessInfo("USER_PROFILE_CREATED");

                    await NotifySubscribersAsync(saved, ProfileCMD.Create);
                }

                return Response(saved.FactoryWithOTPVM());
            }
        }

        public async Task<DomainResponse> CreateSAAsync(string id, string name, string email)
        {
            Telemetry.TrackEvent("Create Service Account User", id);

            var existing = await Repository.GetByIdAsync<Profile>(id);
            if (existing is not null)
                return Conflict("SERVICE_ACCOUNT_ALREADY_EXISTS");

            string unencriptedSecret = Credentials.RandomizeSecret();

            var toSave = new Profile()
            {
                Id = id,
                Name = name.Replace(" (Service Account)", "")
                           .Replace("(", "")
                           .Replace(")", "") + " (Service Account)",
                Channels = new()
                {
                    Email = email?.ToLower(),
                    EmailIsValid = true
                },
                Accounts =
                [
                    new()
                    {
                        Id = id,
                        Source = AccountSource.IM.Code,
                        Roles = [AccountIMRole.ServiceAccount.Code],
                        Credentials = new() { Secret = Credentials.OneWayEncript(unencriptedSecret) }
                    }
                ]
            };

            Profile saved = await Repository.AddAsync(toSave);

            AddBusinessInfo("SERVICE_ACCOUNT_CREATED");

            await NotifySubscribersAsync(saved, ProfileCMD.Create);

            return Response(new ServiceAccountVM() { Id = id, Name = saved.Name, Email = saved.Channels.Email, Secret = unencriptedSecret });
        }

        public async Task<DomainResponse> UpdateSAAsync(string id, string name, string email, int? tryNum = 1)
        {
            Telemetry.TrackEvent("Update Service Account User", $"id: {id} tryNum:{tryNum}");

            var toSave = await Repository.GetByIdAsync<Profile>(id);
            //Ignores not found to make it easier for other MSs to keep SP name and email updated,
            //so they do not need to control if the SP is created or not - just triggering SP update if the original data is updated
            if (toSave is null)
                return Response();

            var before = ComparableProfileVM.FactoryFrom(toSave);
            toSave.Name = name.Replace(" (Service Account)", "")
                              .Replace("(", "")
                              .Replace(")", "") + " (Service Account)";

            if (!string.IsNullOrWhiteSpace(email))
                toSave.Channels.Email = email;

            Profile saved;
            try
            {
                saved = await Repository.UpdateAsync(toSave);

                AddBusinessInfo("SERVICE_ACCOUNT_UPDATED");

                await NotifySubscribersOfChangesBetween(before, saved);
            }
            catch (OptimisticConcurrencyLightException)
            {
                if (tryNum <= 3)
                    return await UpdateSAAsync(id, name, email, ++tryNum);
                else
                    throw;
            }

            return Response(new ServiceAccountVM() { Id = id, Name = saved.Name, Email = saved.Channels.Email });
        }

        public async Task<DomainResponse> DeleteSAAsync(string id)
        {
            Telemetry.TrackEvent("Delete Service Account User", id);

            var toDelete = await Repository.GetByIdAsync<Profile>(id);

            if (toDelete is null)
                return Response();

            var deleted = await Repository.DeleteAsync<Profile>(id);

            return Response(new ServiceAccountVM() { Id = id, Name = deleted?.Name, Email = deleted?.Channels?.Email });
        }

        public async Task<DomainResponse> GenerateSASecretAsync(string id, int? tryNum = 1)
        {
            Telemetry.TrackEvent("Generate Service Account Secret", $"id: {id} tryNum:{tryNum}");

            var toSave = await Repository.GetByIdAsync<Profile>(id);

            if (toSave is null)
                return NoContent();
            else
            {
                string unencriptedSecret = Credentials.RandomizeSecret();

                toSave.Accounts.First().Credentials.Secret = Credentials.OneWayEncript(unencriptedSecret);

                Profile saved;
                try
                {
                    saved = await Repository.UpdateAsync(toSave);

                    AddBusinessInfo("SERVICE_ACCOUNT_SECRET_CREATED");
                }
                catch (OptimisticConcurrencyLightException)
                {
                    if (tryNum <= 3)
                        return await GenerateSASecretAsync(id, ++tryNum);
                    else
                        throw;
                }


                return Response(new ServiceAccountVM() { Id = id, Name = saved.Name, Email = saved.Channels.Email, Secret = unencriptedSecret });
            }
        }

        internal async Task<DomainResponse> SyncFromAADUsersAsync()
        {
            Telemetry.TrackEvent("Sync Profiles from AAD Users");

            string sql = $@"SELECT *
                            FROM <{nameof(Profile)}> c
                            WHERE c.accounts[0].source = '{AccountSource.AAD.Code}'
                              AND (   c.channels.email LIKE '%{PRD_DOMAIN}'
                                   OR c.channels.email LIKE '%{DEV_DOMAIN}')";

            int count = 0;
            foreach (var profile in Repository.Query<Profile>(sql))
                if (await SyncFromAADAsync(profile))
                    count++;

            return Response(count);
        }

        private async Task<bool> SyncFromAADAsync(Profile profile)
        {
            var (user, roles) = await AADService.GetUserAndRolesFromAADByIdAsync(profile.Accounts.First().Id);

            if (user is null)
                return await InactivateFromAADAsync(profile);

            var before = ComparableProfileVM.FactoryFrom(profile);
            bool synced = false;

            var account = profile.Accounts.First();
            if (!account.Roles.OrderBy(x => x).SequenceEqual(roles.OrderBy(x => x)))
            {
                account.Roles = roles;
                synced = true;

                if (account.Roles.Count == 0)
                    if (profile.Status == ProfileStatus.Inactive.Code)
                        return true;
                    else
                        return await InactivateFromAADAsync(profile);
            }

            if (profile.Status == ProfileStatus.Inactive.Code)
            {
                profile.ActivateFromEmail();
                synced = true;
            }

            if (profile.Channels.Email != user.Email &&
                !string.IsNullOrWhiteSpace(user.Email))
            {
                profile.Channels.Email = user.Email;
                synced = true;
            }

            if (profile.Name != user.Name &&
                !string.IsNullOrWhiteSpace(user.Name))
            {
                profile.Name = user.Name;
                synced = true;
            }

            if (synced)
            {
                var updated = await Repository.UpdateAsync(profile);
                await NotifySubscribersOfChangesBetween(before, updated);
            }

            return synced;
        }

        internal async Task<Profile> GetAsync(string id, bool onlyIM = false)
        {
            Telemetry.TrackEvent("Get Profile", id);

            var profile = await Repository.GetByIdAsync<Profile>(id);

            //Checks if not found user hasn't been created on AAD directly and, if so, creates it
            if (profile is null && !onlyIM)
            {
                var (user, roles) = await AADService.GetUserAndRolesFromAADByIdAsync(id);

                if (user is not null)
                    profile = await UpsertProfileForAADUserAsync(user, roles);
            }

            return profile;
        }

        internal async Task<Profile> PutAsync(Profile profile)
        {
            Telemetry.TrackEvent("Update Profile", profile.Id);

            return await Repository.UpdateAsync(profile);
        }

        internal async Task<Profile> CreateProfileForAADClaimsAsync(ClaimsPrincipal user)
        {
            try
            {
                var toSave = Profile.FactoryFromAADClaims(user);
                toSave.RegisterSignin();

                var saved = await Repository.AddAsync(toSave);

                await NotifySubscribersAsync(saved, ProfileCMD.Create);

                return saved;
            }
            catch
            {
                //Ignore errors caused by DB is reset in non-production environments
                if (!WorkBench.IsProductionEnvironment)
                    return null;
                else
                    throw;
            }
        }

        internal async Task<Profile> UpsertProfileForAADUserAsync(DirectoryUserSummaryVM user, List<string> roles, bool fromInvitation = false, int? tryNum = 1)
        {
            Telemetry.TrackEvent("Upsert Profile for AAD User", $"id: {user?.Id} tryNum:{tryNum}");

            var fromAAD = Profile.FactoryFromAADUser(user, roles, fromInvitation);

            if (fromAAD is null)
                return null;

            Profile existing = await Repository.GetByIdAsync<Profile>(user.Id);

            Profile saved;
            if (existing is null)
            {
                saved = await Repository.AddAsync(fromAAD);
                await NotifySubscribersAsync(saved, ProfileCMD.Create);
            }
            else
            {
                try
                {
                    var before = ComparableProfileVM.FactoryFrom(existing);

                    //Maps only key AAD data
                    existing.Name = fromAAD.Name;
                    existing.Accounts.First().Roles = fromAAD.Accounts.First().Roles;
                    if (!string.IsNullOrWhiteSpace(fromAAD.Channels.Email))
                        existing.Channels.Email = fromAAD.Channels.Email?.ToLower();

                    saved = await Repository.UpdateAsync(existing);

                    await NotifySubscribersOfChangesBetween(before, saved);
                }
                catch (OptimisticConcurrencyLightException)
                {
                    if (tryNum <= 3)
                        return await UpsertProfileForAADUserAsync(user, roles, fromInvitation, ++tryNum);
                    else
                        throw;
                }
            }

            return saved;
        }

        internal async Task<Profile> UpdateRolesForAADUserAsync(Profile toSave, List<string> roles)
        {
            Telemetry.TrackEvent("Update Roles for AAD User", $"id: {toSave.Id} tryNum:1");

            if (roles is null)
                return toSave;

            var before = ComparableProfileVM.FactoryFrom(toSave);
            toSave.Accounts.First().Roles = roles;

            if (roles.Count == 0)
                toSave.Status = ProfileStatus.Inactive.Code;

            Profile saved;
            try
            {
                saved = await Repository.UpdateAsync(toSave);
                await NotifySubscribersOfChangesBetween(before, saved);
            }
            catch (OptimisticConcurrencyLightException)
            {
                return await UpdateRolesForAADUserAsync(toSave.Id, roles, 2);
            }

            return saved;
        }

        internal async Task<Profile> UpdateRolesForAADUserAsync(string id, List<string> roles, int? tryNum = 1)
        {
            Telemetry.TrackEvent("Update Roles for AAD User", $"id: {id} tryNum:{tryNum}");

            var toSave = await GetAsync(id);
            var before = ComparableProfileVM.FactoryFrom(toSave);
            toSave.Accounts.First().Roles = roles;

            if (roles.Count == 0)
                toSave.Status = ProfileStatus.Inactive.Code;

            Profile saved;
            try
            {
                saved = await Repository.UpdateAsync(toSave);
                await NotifySubscribersOfChangesBetween(before, saved);
            }
            catch (OptimisticConcurrencyLightException)
            {
                if (tryNum <= 3)
                    return await UpdateRolesForAADUserAsync(id, roles, ++tryNum);
                else
                    throw;
            }

            return saved;
        }

        internal async Task<Profile> InactivateUserAsync(string id)
        {
            var profile = await Repository.GetByIdAsync<Profile>(id);

            if (profile is null || !profile.IsFromAAD)
                return null;

            profile.Status = ProfileStatus.Inactive.Code;
            profile.InactivatedAt = WorkBench.UtcNow;

            var inactivated = await Repository.UpdateAsync(profile);

            await NotifySubscribersAsync(inactivated, ProfileCMD.Delete);

            return inactivated;
        }

        internal async Task<DomainResponse> ProcessEmailBouncesAsync(DateTime from, DateTime to, List<StatusByEmail> addresses)
        {
            Telemetry.TrackEvent("Process Email Bounces", $"from: {from}, to: {to}, qty:{addresses.Count}");

            List<(Profile profile, string email)> toNotify = [];

            foreach (var address in addresses)
            {
                var profiles = Repository.Get<Profile>(p => p.Channels.Email == address.Email || p.Channels.EmailToChange == address.Email);

                foreach (var profile in profiles)
                    if (Service<ChannelService>().InvalidateEmailAddress(profile, address))
                    {
                        var updated = await Repository.UpdateAsync(profile);

                        toNotify.Add((profile, address.Email));

                        if (updated.Status == ProfileStatus.Inactive.Code)
                        {
                            //Only AAD users can be inactive, IM users are deleted instead
                            if (updated.IsFromAAD)
                                try
                                {
                                    await AADService.RemoveAADUserAsync(updated.Id);
                                }
                                catch (Exception e)
                                {
                                    Telemetry.TrackException(new LightException($"Error while trying to remove AAD User '{updated.Id}'", e));
                                }

                            await NotifySubscribersAsync(updated, ProfileCMD.Ban);
                        }
                        else
                            await NotifySubscribersAsync(updated, ProfileCMD.Update);
                    }
            }

            Thread.Sleep(5000);

            foreach (var (profile, email) in toNotify)
                ChannelService.NotifyUserOfInvalidEmail(profile, email);

            return Response();
        }

        internal async Task NotifySubscribersAsync(Profile profile, ProfileCMD command)
        {
            var msg = FactoryLightMessage<ProfileMSG>(command);
            profile.MapToMSG(msg);

            await userProfileBus.SendToTopicAsync(msg);
        }

        private void SendLogoutDomainEvent(Profile profile)
        {
            DomainEventMSG logout = FactoryLightMessage<DomainEventMSG>(DomainEventCMD.Notify);
            logout.Name = "logout";
            logout.ShortMessage = LightLocalizer.Localize("YOU_HAVE_BEEN_DISCONNECTED");
            logout.UserIds.Add(profile.Id);

            PlatformServices.SendDomainEvent(logout);
        }

        private void NotifyBackOfficeManagersOfOptOut(Profile profile, string feedback)
        {
            var msg = FactoryLightMessage<EmailMSG>(EmailCMD.Send);
            msg.Type = NotificationType.Tasks.Code;
            msg.Subject = LightLocalizer.Localize("MEMBER_OPTED_OUT_SHORT_SUBJECT", profile.Name);
            msg.Message = LightLocalizer.Localize("MEMBER_OPTED_OUT_SHORT_MESSAGE", profile.Name, feedback, "{EmployeeAppURL}" + $"/members/{profile.Id}");

            foreach (var manager in GetUsersByRoles(["generalAdmin", "fieldManager"]))
            {
                msg.UserId = manager.Id;
                PlatformServices.SendEmail(msg);
            }
        }

        internal async Task NotifySubscribersOfChangesBetween(ComparableProfileVM toCompare, Profile updated)
        {
            var before = FactoryLightMessage<ProfileMSG>(ProfileCMD.Update);
            toCompare.MapToMSG(before);

            var toSend = FactoryLightMessage<ProfileMSG>(ProfileCMD.Update);
            updated.MapToMSG(toSend);

            before.At = toSend.At;

            if (toCompare.Status == ProfileStatus.Inactive.Code &&
                (updated.Status == ProfileStatus.Active.Code ||
                 updated.Status == ProfileStatus.Invited.Code))
                toSend.CommandType = ProfileCMD.Create.Code;

            //Notify subscribing microservices about subscribable changes in user's profile
            if (before.ToJsonString() != toSend.ToJsonString())
                await userProfileBus.SendToTopicAsync(toCompare.MapChangingRoles(toSend));
        }

        private void CheckAlternateKeys(string profileId, string oldEmail, string oldPhone, string toSaveEmail, string toSavePhone)
        {
            toSaveEmail ??= "";

            if (oldEmail != toSaveEmail || (oldPhone != toSavePhone && toSavePhone is not null))
            {

#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
                var existingEmailOrPhone = Repository.Get<Profile>(p => p.Id != profileId &&
                                                                        (p.Channels.Email == toSaveEmail.ToLower() ||
                                                                         p.Channels.Phone == toSavePhone ||
                                                                         p.Channels.EmailToChange == toSaveEmail.ToLower() ||
                                                                         p.Channels.PhoneToChange == toSavePhone));
#pragma warning restore CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons

                if (existingEmailOrPhone.Any())
                    BusinessError("PHONE_OR_EMAIL_ALREADY_REGISTERED");
            }
        }

        private async Task<bool> InactivateFromAADAsync(Profile profile)
        {
            profile.Status = ProfileStatus.Inactive.Code;
            profile.InactivatedAt = DateTime.UtcNow;

            await Repository.UpdateAsync(profile);
            await NotifySubscribersAsync(profile, ProfileCMD.Delete);

            return true;
        }
    }
}