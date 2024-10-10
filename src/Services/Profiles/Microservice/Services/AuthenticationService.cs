using Liquid;
using Liquid.Base;
using Liquid.Domain;
using Liquid.Platform;
using Liquid.Runtime;
using Microservice.Models;
using Microservice.ViewModels;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Microservice.Services
{
    internal class AuthenticationService : LightService
    {
        private static readonly int MAX_SECRET_ATTEMPTS = 5;

        internal DomainResponse RequestLogin(string id, string connectionId)
        {
            Telemetry.TrackEvent("Request Login", $"id: {id} connectionId: {connectionId}");

            var profile = Repository.Get<Profile>(p => p.Accounts[0].Id == id)
                                    .FirstOrDefault();

            if (profile is null || profile.Accounts.First().Source == AccountSource.AAD.Code)
                return NoContent();

            DomainEventMSG login = FactoryLightMessage<DomainEventMSG>(DomainEventCMD.Notify);
            login.Name = "requestLogin";
            login.ShortMessage = LightLocalizer.Localize("REQUEST_LOGIN_SHORT_MESSAGE");
            login.UserIds.Add(id);
            login.PushIfOffLine = true;

            JsonNode json = login.Payload.ToJsonNode();
            json.AsObject().Add("connectionId", JsonNode.Parse($"\"{connectionId}\""));
            login.Payload = json.ToJsonDocument();

            PlatformServices.SendDomainEvent(login);

            return Response();
        }

        internal async Task<DomainResponse> AllowLoginAsync(string id, string connectionId, int? tryNum = 1)
        {
            Telemetry.TrackEvent("Allow Login", $"id: {id} connectionId: {connectionId} tryNum:{tryNum}");

            var profile = Repository.Get<Profile>(p => p.Accounts[0].Id == id)
                                    .FirstOrDefault();

            if (profile is null || profile.Accounts.First().Source == AccountSource.AAD.Code)
                return NoContent();

            try
            {
                profile.GenerateNewOTP();
                profile.RegisterSignin();

                await Repository.UpdateAsync(profile);

                DomainEventMSG allowLogin = FactoryLightMessage<DomainEventMSG>(DomainEventCMD.Notify);
                allowLogin.Name = "allowLogin";
                allowLogin.ShortMessage = "Login allowed (message not to be shown as push)";
                allowLogin.AnonConns.Add(connectionId);

                JsonNode editablePayload = allowLogin.Payload.ToJsonNode();
                editablePayload.AsObject().Add("otp", JsonNode.Parse($"\"{profile.Accounts.First().Credentials.OTP}\""));
                allowLogin.Payload = editablePayload.ToJsonDocument();

                PlatformServices.SendDomainEvent(allowLogin);
            }
            catch (OptimisticConcurrencyLightException)
            {
                if (tryNum <= 3)
                    return await AllowLoginAsync(id, connectionId, ++tryNum);
                else
                    throw;
            }

            return Response();
        }

        internal async Task<DomainResponse> SendAuthLinkAsync(string accountId, string channelType, int? tryNum = 1)
        {
            Telemetry.TrackEvent("Resend Authentication Link", $"accountId: {accountId} channelType: {channelType} tryNum:{tryNum}");

            var toUpdate = await Repository.GetByIdAsync<Profile>(accountId);

            if (toUpdate is null)
                return NoContent();

            var account = toUpdate.Accounts.First(a => a.Id == accountId);

            if (account is null)
                return BusinessError("USER_HAS_NO_ACCOUNT");

            toUpdate.GenerateNewOTP();

            try
            {
                var updated = await Repository.UpdateAsync(toUpdate);

                if (channelType == ChannelType.Email.Code)
                    SendAuthLinkByEmail(account);
                else
                    SendAuthLinkByText(account);

                AddBusinessInfo("AUTHENTICATION_LINK_SENT_SUCCESSFULLY");
            }
            catch (OptimisticConcurrencyLightException)
            {
                if (tryNum <= 3)
                    return await SendAuthLinkAsync(accountId, channelType, ++tryNum);
                else
                    throw;
            }
            return Response();
        }

        public async Task<DomainResponse> RefreshJWTAsync(string oldToken)
        {
            if (!TokenVM.ValidatePrescribedJWT(oldToken, out SecurityToken validatedToken))
                return BusinessError("TOKEN_INVALID");

            var accountId = ((JwtSecurityToken)validatedToken).Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub).Value;

            Telemetry.TrackEvent("Refresh Token", accountId);

            var profile = Repository.Get<Profile>(p => p.Accounts[0].Id == accountId).FirstOrDefault();

            if (profile is null || profile.Status != ProfileStatus.Active.Code)
                return NoContent();

            profile = await Service<ProfileService>().UpdateRolesForAADUserAsync(profile,
                                                                                 AADService.GetRolesToUpdate(profile));

            if (profile.Status == ProfileStatus.Inactive.Code)
                return NoContent();

            return Response(TokenVM.FactoryFor(profile));
        }

        private async Task ValidateOTP(string accountId, Profile profile, string otp, string channelType, int? tryNum = 1)
        {
            Telemetry.TrackEvent("Validate OTP", $"accountId: {accountId} channelType: {channelType} tryNum:{tryNum}");

            var before = ComparableProfileVM.FactoryFrom(profile);

            var account = profile.Accounts.First(a => a.Id == accountId);
            if (account is null)
            {
                AddBusinessError("USER_HAS_NO_ACCOUNT");
                return;
            }

            if (account.Credentials?.OTP != otp)
            {
                AddBusinessError("OTP_INVALID");
                return;
            }

            if (WorkBench.UtcNow > account.Credentials.OTPExpiresAt)
            {
                AddBusinessError("OTP_EXPIRED");
                return;
            }

            profile.Channels.MarkAsValid(channelType);

            account.Credentials.OTPExpiresAt = WorkBench.UtcNow;

            try
            {
                profile.RegisterSignin();
                var updated = await Repository.UpdateAsync(profile);
                await Service<ProfileService>().NotifySubscribersOfChangesBetween(before, updated);
            }
            catch (OptimisticConcurrencyLightException)
            {
                if (tryNum <= 3)
                {
                    await ValidateOTP(accountId, profile, otp, channelType, ++tryNum);
                    return;
                }
                else
                    throw;
            }

            return;
        }

        public async Task<DomainResponse> RequestNewOTPAsync(string accountId, int? tryNum = 1)
        {
            Telemetry.TrackEvent("Request New OTP", $"accountId: {accountId} tryNum:{tryNum}");

            var toUpdate = Repository.Get<Profile>(p => p.Accounts[0].Id == accountId)
                                     .FirstOrDefault();

            if (toUpdate is null)
                return NoContent();

            var account = toUpdate.Accounts.First(a => a.Id == accountId);

            if (account is null)
                return BusinessError("USER_HAS_NO_ACCOUNT");

            if (!account.Roles.Contains(AccountIMRole.Member.Code))
                return Forbidden();

            toUpdate.GenerateNewOTP();

            Profile updated;
            try
            {
                updated = await Repository.UpdateAsync(toUpdate);
            }
            catch (OptimisticConcurrencyLightException)
            {
                if (tryNum <= 3)
                    return await RequestNewOTPAsync(accountId, ++tryNum);
                else
                    throw;
            }

            return Response(updated.FactoryWithOTPVM());
        }

        public async Task<DomainResponse> AuthenticateByOTPAsync(string accountId, string otp, string channelType)
        {
            Telemetry.TrackEvent("Authenticate by OTP", accountId);

            var profile = Repository.Get<Profile>(p => p.Accounts.Where(a => a.Id == accountId).ToList()[0] != null)
                                    .FirstOrDefault();

            if (profile is null)
                return NoContent();

            await ValidateOTP(accountId, profile, otp, channelType);

            if (HasBusinessErrors)
                return Response();

            return Response(TokenVM.FactoryFor(profile));
        }

        public async Task<DomainResponse> AuthenticateBySecretAsync(string accountId, string secret, int? tryNum = 1)
        {
            Telemetry.TrackEvent("Authenticate by Secret", $"accountId: {accountId} tryNum:{tryNum}");

            var profile = Repository.Get<Profile>(p => p.Accounts[0].Id == accountId).FirstOrDefault();

            if (profile is null)
                return Unauthorized("USER_NOT_FOUND");

            var account = profile.Accounts.FirstOrDefault(a => a.Id == accountId);

            try
            {
                if (!account.Roles.Contains(AccountIMRole.ServiceAccount.Code))
                    return Forbidden();

                if (account.Credentials.SecretTries >= MAX_SECRET_ATTEMPTS)
                    return BusinessError("ACCOUNT_IS_DISABLED_BY_INVALID_ATTEMPTS");

                if (Credentials.OneWayEncript(secret) != account.Credentials.Secret)
                {
                    account.Credentials.SecretTries++;
                    await Repository.UpdateAsync(profile);

                    if (account.Credentials.SecretTries < MAX_SECRET_ATTEMPTS)
                        BusinessError("INVALID_SECRET", account.Credentials.SecretTries, MAX_SECRET_ATTEMPTS);
                    else
                        BusinessError("ACCOUNT_WAS_DISABLED_BY_INVALID_ATTEMPTS", MAX_SECRET_ATTEMPTS);

                    return Response();
                }

                if (account.Credentials.SecretTries > 0)
                {
                    profile.RegisterSignin();
                    account.Credentials.SecretTries = 0;
                    await Repository.UpdateAsync(profile);
                }

                return Response(TokenVM.FactoryFor(profile));
            }
            catch (OptimisticConcurrencyLightException)
            {
                if (tryNum <= 3)
                    return await AuthenticateBySecretAsync(accountId, secret, ++tryNum);
                else
                    throw;
            }
        }

        public async Task<DomainResponse> AuthenticateByIdTokenAsync(string idToken, int? tryNum = 1)
        {
            ClaimsPrincipal userClaims = await Service<AADService>().GetClaimsFromIdTokenAsync(idToken);
            if (userClaims is null)
            {
                Telemetry.TrackEvent("Authenticate by Id_Token", $"accountId: null tryNum:{tryNum}");
                return Response();
            }

            var accountId = userClaims.FindFirstValue(JwtClaimTypes.UserId);
            Telemetry.TrackEvent("Authenticate by Id_Token", $"accountId: {accountId} tryNum:{tryNum}");

            var updating = Repository.Get<Profile>(p => p.Accounts[0].Id == accountId).FirstOrDefault();

            if (updating is null)
                updating = await Service<ProfileService>().CreateProfileForAADClaimsAsync(userClaims);
            else
            {
                var before = ComparableProfileVM.FactoryFrom(updating);
                updating.UpdateFromAADSignIn(userClaims);
                updating.RegisterSignin();
                try
                {
                    updating = await Repository.UpdateAsync(updating);
                    await Service<ProfileService>().NotifySubscribersOfChangesBetween(before, updating);
                }
                catch (OptimisticConcurrencyLightException)
                {
                    if (tryNum <= 3)
                        return await AuthenticateByIdTokenAsync(idToken, ++tryNum);
                    else
                        throw;
                }
            }

            if (HasBusinessErrors)
                return Response();

            return Response(TokenVM.FactoryFor(updating));
        }

        public DomainResponse GetChannelTypes()
        {
            Telemetry.TrackEvent("Get Channel Types");

            return Response(ChannelType.GetAll());
        }


        private void SendAuthLinkByEmail(Account account)
        {
            Telemetry.TrackEvent("Send AuthLink By Email", account.Id);

            var activationPayload = System.Text.Encoding.UTF8.GetBytes(new
            {
                channel = ChannelType.Email.Code,
                otp = account.Credentials.OTP,
                accountId = account.Id
            }.ToJsonString());

            var activationLink = "{MemberAppURL}" + $"/login/allow?otpToken={Convert.ToBase64String(activationPayload)}";

            var emailMSG = FactoryLightMessage<EmailMSG>(EmailCMD.Send);
            emailMSG.UserId = account.Id;
            emailMSG.Type = NotificationType.Direct.Code;
            emailMSG.Subject = LightLocalizer.Localize("AUTHENTICATION_LINK_EMAIL_SUBJECT");
            emailMSG.Message = LightLocalizer.Localize("AUTHENTICATION_LINK_EMAIL_MESSAGE", activationLink);

            PlatformServices.SendEmail(emailMSG);
        }

        private void SendAuthLinkByText(Account account)
        {
            Telemetry.TrackEvent("Send AuthLink By Text", account.Id);

            var activationPayload = System.Text.Encoding.UTF8.GetBytes(new
            {
                channel = ChannelType.Phone.Code,
                otp = account.Credentials.OTP,
                accountId = account.Id
            }.ToJsonString());

            var activationLink = "{MemberAppURL}" + $"/login/allow?otpToken={Convert.ToBase64String(activationPayload)}";

            var shortTextMSG = FactoryLightMessage<ShortTextMSG>(ShortTextCMD.Send);
            shortTextMSG.UserId = account.Id;
            shortTextMSG.Type = NotificationType.Direct.Code;
            shortTextMSG.Message = LightLocalizer.Localize("AUTHENTICATION_LINK_TEXT_MESSAGE", activationLink);

            PlatformServices.SendText(shortTextMSG);
        }
    }
}