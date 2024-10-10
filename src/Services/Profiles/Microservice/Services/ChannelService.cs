using Liquid;
using Liquid.Base;
using Liquid.Domain;
using Liquid.Platform;
using Microservice.Messages;
using Microservice.Models;
using Microservice.ViewModels;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Microservice.Services
{
    internal class ChannelService : LightService
    {
        private readonly int REVERSABLE_CHANGE_DEADLINE_IN_DAYS = 7;

        private static string GenerateOTP() => new Random().Next(10000, 99999).ToString();

        internal void ControlChannelUpdates(ComparableProfileVM before, Profile toChange)
        {
            if (before.Channels.Email != toChange.Channels.Email)
                CreateEmailChangeRequest(before, toChange);

            if (before.Channels.Phone != toChange.Channels.Phone)
                CreatePhoneChangeRequest(before, toChange);
        }

        internal void RequestValidationOfUpdatedChannels(ComparableProfileVM before, Profile updated)
        {
            if (!string.IsNullOrWhiteSpace(updated.Channels.EmailOTP) &&
                before.Channels.EmailOTP != updated.Channels.EmailOTP)
            {
                SendValidationEmail(updated.Id, updated.Channels.EmailToChange, updated.Channels.EmailOTP);
            }

            if (!string.IsNullOrWhiteSpace(updated.Channels.PhoneOTP) &&
                before.Channels.PhoneOTP != updated.Channels.PhoneOTP)
            {
                SendValidationText(updated.Id, updated.Channels.PhoneToChange, updated.Channels.PhoneOTP);
            }

            if (!string.IsNullOrWhiteSpace(updated.Channels.RevertOTP) &&
                before.Channels.RevertOTP != updated.Channels.RevertOTP)
            {
                string oldData = "";
                string newData = "";

                if (!string.IsNullOrWhiteSpace(updated.Channels.LastValidEmail))
                {
                    oldData = updated.Channels.LastValidEmail;
                    newData = updated.Channels.EmailToChange;
                }

                if (!string.IsNullOrWhiteSpace(updated.Channels.LastValidPhone))
                {
                    oldData += string.IsNullOrWhiteSpace(oldData) ? updated.Channels.LastValidPhone : (" / " + updated.Channels.LastValidPhone);
                    newData += string.IsNullOrWhiteSpace(newData) ? updated.Channels.PhoneToChange : (" / " + updated.Channels.PhoneToChange);
                }

                SendAlertAndRevertEmail(updated.Id, updated.Channels.RevertOTP, oldData, newData);
            }
        }

        internal void ValidateRequest(Profile profile, string channelType, string validationOTP)
        {
            if (channelType == ChannelType.Email.Code)
                ValidateEmailChange(profile, validationOTP);
            else
                ValidatePhoneChange(profile, validationOTP);
        }

        internal void RevertUpdate(Profile profile, string revertOTP)
        {
            if (profile.Channels.RevertOTP == revertOTP)
            {
                if (profile.Channels.ReversableUntil >= WorkBench.UtcNow)
                {
                    RevertEmailChange(profile);
                    RevertPhoneChange(profile);
                    profile.Channels.RevertOTP = string.Empty;
                    profile.Channels.ReversableUntil = DateTime.MinValue;
                }
                else
                    AddBusinessError("REVERT_LINK_PRESCRIBED");
            }
            else
                AddBusinessError("REVERT_LINK_INVALID");
        }

        private void CreatePhoneChangeRequest(ComparableProfileVM before, Profile toChange)
        {
            if (string.IsNullOrWhiteSpace(toChange.Channels.Phone))
                AddBusinessError("PHONE_IS_INVALID");
            else
            {
                toChange.Channels.PhoneOTP = GenerateOTP();
                toChange.Channels.PhoneToChange = toChange.Channels.Phone; //Stores the new (current) phone in a temp PhoneToChange property
                toChange.Channels.Phone = before.Channels.Phone; //Keeps the old phone as current until the validation

                if (before.Channels.PhoneIsValid && before.Channels.ReversableUntil < WorkBench.UtcNow)
                {
                    toChange.Channels.LastValidPhone = before.Channels.Phone; //Stores the old phone as a way to revert the change

                    //The same reversal OTP for both channel changes (replacing any older OTP and forcing a Revert message to user)
                    toChange.Channels.RevertOTP = GenerateOTP();
                    toChange.Channels.ReversableUntil = WorkBench.UtcNow.AddDays(REVERSABLE_CHANGE_DEADLINE_IN_DAYS);
                }
            }
        }

        private void ValidatePhoneChange(Profile profile, string validationOTP)
        {
            Telemetry.TrackEvent("Validate Phone Change", profile.Id);

            if (profile.Channels.PhoneOTP == validationOTP)
            {
                profile.Channels.PhoneIsValid = true;

                if (!string.IsNullOrWhiteSpace(profile.Channels.PhoneToChange))
                    profile.Channels.Phone = profile.Channels.PhoneToChange;

                profile.Channels.PhoneToChange = string.Empty;
                profile.Channels.PhoneOTP = string.Empty;
            }
            else
                AddBusinessError("INVALID_CHANNEL_OTP");
        }

        private void RevertPhoneChange(Profile profile)
        {
            Telemetry.TrackEvent("Revert Phone Change", profile.Id);

            if (!string.IsNullOrWhiteSpace(profile.Channels.LastValidPhone))
            {
                profile.Channels.Phone = profile.Channels.LastValidPhone;
                profile.Channels.LastValidPhone = string.Empty;
                profile.Channels.PhoneToChange = string.Empty;
                profile.Channels.PhoneOTP = string.Empty;

                AddBusinessInfo("PHONE_CHANGE_REVERTED", profile.Channels.Phone);
            }
            else
                AddBusinessWarning("NO_FORMER_PHONE_TO_REVERTED_TO");
        }

        private void CreateEmailChangeRequest(ComparableProfileVM before, Profile toChange)
        {
            if (string.IsNullOrWhiteSpace(toChange.Channels.Email))
                AddBusinessError("EMAIL_IS_INVALID");
            else
            {
                toChange.Channels.EmailOTP = GenerateOTP();
                toChange.Channels.EmailToChange = toChange.Channels.Email?.ToLower(); //Stores the new (current) email in a temp EmailToChange property
                toChange.Channels.Email = before.Channels.Email?.ToLower(); //Keeps the old email as current until the validation

                if (before.Channels.EmailIsValid && before.Channels.ReversableUntil < WorkBench.UtcNow)
                {
                    toChange.Channels.LastValidEmail = before.Channels.Email?.ToLower(); //Stores the old email as a way to revert the change

                    //The same reversal OTP for both channel changes (replacing any older OTP forcers a Revert message to user)
                    toChange.Channels.RevertOTP = GenerateOTP();
                    toChange.Channels.ReversableUntil = WorkBench.UtcNow.AddDays(REVERSABLE_CHANGE_DEADLINE_IN_DAYS);
                }
            }
        }

        private void ValidateEmailChange(Profile profile, string validationOTP)
        {
            Telemetry.TrackEvent("Validate Email Change", profile.Id);

            if (profile.Channels.EmailOTP == validationOTP)
            {
                profile.Channels.EmailIsValid = true;

                if (!string.IsNullOrWhiteSpace(profile.Channels.EmailToChange))
                    profile.Channels.Email = profile.Channels.EmailToChange?.ToLower();

                profile.Channels.EmailToChange = string.Empty;
                profile.Channels.EmailOTP = string.Empty;
            }
            else
                AddBusinessError("INVALID_CHANNEL_OTP");
        }

        internal bool InvalidateEmailAddress(Profile profile, StatusByEmail address)
        {
            bool invalidated = profile.Channels.EmailToChange == address.Email
                                  ? InvalidateEmailToChange(profile)
                                  : profile.InvalidateEmail(address);

            return invalidated;
        }

        private bool InvalidateEmailToChange(Profile profile)
        {
            if (!RevertEmailChange(profile))
                profile.Channels.EmailIsValid = false;

            return true;
        }

        private bool RevertEmailChange(Profile profile)
        {
            Telemetry.TrackEvent("Revert Email Change", profile.Id);

            if (!string.IsNullOrWhiteSpace(profile.Channels.LastValidEmail))
            {
                profile.Channels.Email = profile.Channels.LastValidEmail?.ToLower();
                profile.Channels.EmailIsValid = true;
                profile.Channels.LastValidEmail = string.Empty;
                profile.Channels.EmailToChange = string.Empty;
                profile.Channels.EmailOTP = string.Empty;

                AddBusinessInfo("EMAIL_CHANGE_REVERTED", profile.Channels.Email);

                return true;
            }
            else
            {
                AddBusinessWarning("NO_FORMER_EMAIL_TO_REVERTED_TO");
                return false;
            }
        }

        internal async Task<DomainResponse> RequestMyChannelsValidationAsync(int? tryNum = 1)
        {
            Telemetry.TrackEvent("Request Validation of User's Channels", $"id: {CurrentUserId} tryNum:{tryNum}");

            bool sendEmail = false;
            bool sendText = false;

            var profile = await Service<ProfileService>().GetAsync(CurrentUserId);
            if (profile is null)
                return NoContent();

            if (!string.IsNullOrWhiteSpace(profile.Channels.Email) && !profile.Channels.EmailIsValid)
            {
                profile.Channels.EmailOTP = GenerateOTP();
                sendEmail = true;
            }

            if (!string.IsNullOrWhiteSpace(profile.Channels.Phone) && !profile.Channels.PhoneIsValid)
            {
                profile.Channels.PhoneOTP = GenerateOTP();
                sendText = true;
            }

            try
            {
                await Service<ProfileService>().PutAsync(profile);

                if (sendEmail)
                    SendValidationEmail(profile.Id, profile.Channels.Email, profile.Channels.EmailOTP);

                if (sendText)
                    SendValidationText(profile.Id, profile.Channels.Phone, profile.Channels.PhoneOTP);
            }
            catch (OptimisticConcurrencyLightException)
            {
                if (tryNum <= 3)
                    return await RequestMyChannelsValidationAsync(++tryNum);
                else
                    throw;
            }

            return Response();
        }

        internal async Task<DomainResponse> ResendMyValidationLinkAsync(string channelType, int? tryNum = 1)
        {
            Telemetry.TrackEvent($"Resend {channelType.FirstToUpper()} Validation Link", $"id: {CurrentUserId} tryNum:{tryNum}");

            bool sendEmail = false;
            bool sendText = false;

            string phone = string.Empty;
            string email = string.Empty;

            var profile = await Service<ProfileService>().GetAsync(CurrentUserId);
            if (profile is null)
                return NoContent();

            if (channelType == ChannelType.Email.Code)
            {
                email = string.IsNullOrWhiteSpace(profile.Channels.EmailToChange) ? profile.Channels.Email : profile.Channels.EmailToChange;
                profile.Channels.EmailOTP = GenerateOTP();
                sendEmail = true;
            }
            else if (channelType == ChannelType.Phone.Code)
            {
                phone = string.IsNullOrWhiteSpace(profile.Channels.PhoneToChange) ? profile.Channels.Phone : profile.Channels.PhoneToChange;
                profile.Channels.PhoneOTP = GenerateOTP();
                sendText = true;
            }

            try
            {
                await Service<ProfileService>().PutAsync(profile);

                if (sendEmail)
                    SendValidationEmail(profile.Id, email, profile.Channels.EmailOTP);

                if (sendText)
                    SendValidationText(profile.Id, phone, profile.Channels.PhoneOTP);
            }
            catch (OptimisticConcurrencyLightException)
            {
                if (tryNum <= 3)
                    return await ResendMyValidationLinkAsync(channelType, ++tryNum);
                else
                    throw;
            }
            return Response();
        }

        private void SendAlertAndRevertEmail(string profileId, string revertOTP, string oldData, string newData)
        {
            Telemetry.TrackEvent("Send Alert And Revert Email", profileId);

            var activationPayload = Encoding.UTF8.GetBytes(new
            {
                id = profileId,
                otp = revertOTP
            }.ToJsonString());

            var activationLink = "{MemberAppURL}" + $"/account/profile/revert?otpToken={Convert.ToBase64String(activationPayload)}";

            var emailMSG = FactoryLightMessage<EmailMSG>(EmailCMD.Send);
            emailMSG.UserId = profileId;
            emailMSG.Type = NotificationType.Direct.Code;
            emailMSG.Subject = LightLocalizer.Localize("UPDATE_REVERT_LINK_EMAIL_SUBJECT");
            emailMSG.Message = LightLocalizer.Localize("UPDATE_REVERT_LINK_EMAIL_MESSAGE", activationLink, oldData, newData);

            PlatformServices.SendEmail(emailMSG);
        }

        private void SendValidationEmail(string profileId, string email, string emailOTP)
        {
            Telemetry.TrackEvent("Send Validation Email", profileId);

            var emailMSG = FactoryLightMessage<EmailMSG>(EmailCMD.Send);
            emailMSG.UserId = profileId;
            emailMSG.Type = NotificationType.Direct.Code;
            emailMSG.Email = email?.ToLower();
            emailMSG.Subject = LightLocalizer.Localize("EMAIL_VALIDATION_LINK_EMAIL_SUBJECT");
            emailMSG.Message = LightLocalizer.Localize("EMAIL_VALIDATION_LINK_EMAIL_MESSAGE", emailOTP);

            PlatformServices.SendEmail(emailMSG);
        }

        private void SendValidationText(string profileId, string phone, string phoneOTP)
        {
            Telemetry.TrackEvent("Send Validation Text", profileId);

            var shortTextMSG = FactoryLightMessage<ShortTextMSG>(ShortTextCMD.Send);
            shortTextMSG.UserId = profileId;
            shortTextMSG.Type = NotificationType.Direct.Code;
            shortTextMSG.Phone = phone;
            shortTextMSG.Message = LightLocalizer.Localize("PHONE_VALIDATION_LINK_TEXT_MESSAGE", phoneOTP);

            PlatformServices.SendText(shortTextMSG);
        }

        internal static void NotifyUserOfInvalidEmail(Profile profile, string address)
        {
            //Only notifies IM users
            if (profile.IsFromAAD)
                return;

            NotificationVM notifVM = new()
            {
                UserId = profile.Id,
                Type = NotificationType.Account.Code,
                Target = NotificationTargetType.Member.Code,
                ShortMessage = LightLocalizer.Localize("INVALID_EMAIL_SHORT_MESSAGE"),
                LongMessage = LightLocalizer.Localize("INVALID_EMAIL_LONG_MESSAGE", address),
                ContextUri = "account/profile",
                Urgent = true
            };

            PlatformServices.SendNotification(notifVM);
        }
    }
}