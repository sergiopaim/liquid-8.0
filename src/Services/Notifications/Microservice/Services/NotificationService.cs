using Liquid;
using Liquid.Base;
using Liquid.Domain;
using Liquid.Platform;
using Microservice.Models;
using Microservice.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microservice.Services
{
    /// <summary>
    /// Sends contextual notifications thought user´s available channels
    /// </summary>
    internal class NotificationService : LightService
    {
        #region Migration

        internal async Task<DomainResponse> MigrateAsync()
        {
            Telemetry.TrackEvent("Migrate Notifications");

            WorkBench.ConsoleWriteLine();
            WorkBench.ConsoleWriteLine("******** MIGRATING NOTIFICATIONS *******");

            List<string> ret = [];

            int updated = 0;

            foreach (var notif in Repository.GetAll<Notification>())
            {
                if (!notif.Cleared)
                {
                    await Repository.UpdateAsync(notif);

                    string sent = $"{++updated} Not cleared -> ({notif.Id})";
                    ret.Add(sent);

                    WorkBench.ConsoleWriteLine(sent);
                }
            }

            return Response(ret);
        }

        #endregion

        internal async Task<DomainResponse> GetMineByIdAsync(string id)
        {
            Telemetry.TrackEvent("Get Notification", $"userId: {CurrentUserId} notificationId: {id}");

            var notification = await Repository.GetByIdAsync<Notification>(id, CurrentUserId);

            if (notification is null)
            {
                notification = Repository.Get<Notification>(n => n.Id == id).FirstOrDefault();

                if (notification is not null)
                {
                    var config = await Repository.GetByIdAsync<Config>(notification.UserId);
                    var login = config?.EmailChannel?.Email ?? config?.PhoneChannel?.Phone;
                    AddBusinessError("NOTIFICATION_OF_OTHER_USER", config?.Name);
                    AddBusinessInfo("OTHER_USER_LOGIN", login);

                    return Response();
                }

                return NoContent();
            }

            if (notification.Cleared)
                return NoContent();

            return Response(NotificationVM.FactoryFrom(notification));
        }

        internal DomainResponse GetAllMine()
        {
            Telemetry.TrackEvent("Get Notifications of Current User", CurrentUserId);

            var notifications = Repository.Get<Notification>(filter: a => a.UserId == CurrentUserId &&
                                                                          !a.Cleared,
                                                             orderBy: a => a.SentAt,
                                                             descending: true);

            return Response(notifications.Select(NotificationVM.FactoryFrom));
        }

        internal DomainResponse GetAllByUser(string userId)
        {
            Telemetry.TrackEvent("Get Notifications of User", userId);

            var notifications = Repository.Get<Notification>(filter: a => a.UserId == userId,
                                                             orderBy: a => a.SentAt,
                                                             descending: true);

            return Response(notifications.Select(HistoryVM.FactoryFrom));
        }

        internal async Task<DomainResponse> GetUserOfNotificationAsync(string id)
        {
            Telemetry.TrackEvent("Get User of Notification", id);

            //Making a generic query (not by partitionkey/id) because the partitionKey (userId) is indeed what is we are going to get
            var notif = Repository.Get<Notification>(a => a.Id == id && !a.Cleared)
                                  .FirstOrDefault();

            if (notif is null)
                return NoContent();

            var config = await Repository.GetByIdAsync<Config>(notif.UserId);

            return Response(BasicUserInfoVM.FactoryFrom(config));
        }

        internal async Task<DomainResponse> MarkAllMineAsViewedAsync()
        {
            Telemetry.TrackEvent("Mark All Notification of Current User As Viewed", CurrentUserId);

            var viewedAt = WorkBench.UtcNow;
            var notViewedOnes = Repository.Get<Notification>(a => a.UserId == CurrentUserId &&
                                                                  a.ViewedAt == DateTime.MinValue &&
                                                                  !a.Cleared);

            List<NotificationVM> viewedOnes = [];
            foreach (var notViewed in notViewedOnes)
            {
                notViewed.ViewedAt = viewedAt;
                await Repository.UpdateAsync(notViewed);
                viewedOnes.Add(NotificationVM.FactoryFrom(notViewed));
            }

            return Response(viewedOnes);
        }

        internal async Task<DomainResponse> MarkMineAsViewedByIdAsync(string notificationId)
        {
            Telemetry.TrackEvent("Mark Notification as Viewed", $"userId: {CurrentUserId} notificationId: {notificationId}");

            var toMarkAsViewed = await Repository.GetByIdAsync<Notification>(notificationId, CurrentUserId);

            if (toMarkAsViewed is null ||
                toMarkAsViewed.UserId != CurrentUserId ||
                toMarkAsViewed.Cleared)
                return NoContent();
            else
            {
                toMarkAsViewed.ViewedAt = WorkBench.UtcNow;
                var viewed = await Repository.UpdateAsync(toMarkAsViewed);

                return Response(NotificationVM.FactoryFrom(viewed));
            }
        }

        internal async Task<DomainResponse> ClearAllMineViewedAsync()
        {
            Telemetry.TrackEvent("Clear All Viewed Notifications of Current User", CurrentUserId);

            var onesToClear = Repository.Get<Notification>(a => a.UserId == CurrentUserId &&
                                                                a.ViewedAt != DateTime.MinValue &&
                                                                !a.Cleared);

            List<NotificationVM> clearedOnes = [];
            foreach (var toClear in onesToClear)
            {
                toClear.Cleared = true;
                await Repository.UpdateAsync(toClear);
                clearedOnes.Add(NotificationVM.FactoryFrom(toClear));
            }

            return Response(clearedOnes);
        }

        internal async Task<DomainResponse> SendNotificationAsync(NotificationVM notifVM)
        {
            Telemetry.TrackEvent("Send Notification", notifVM.Id);

            var userConfig = await Service<ConfigService>().GetConfigByIdAsync(notifVM.UserId);

            if (userConfig is null)
            {
                ResetNoContentError();
                return Response();
            }

            //Controls the fallback from WebPush channel to Email channel
            var immediateCount = await Service<WebPushService>().SendToAllEndPointsAsync(userConfig, notifVM, notifVM.Type);

            var notifSavedVM = await SaveNotificationAsync(notifVM, userConfig, immediateCount == 0);
            if (HasBusinessErrors)
                return Response();

            return Response(notifSavedVM);
        }

        internal async Task<DomainResponse> SendPushAsync(PushVM pushVM)
        {
            Telemetry.TrackEvent("Send Push", $"userId: {pushVM.UserId} contextUri:{pushVM.ContextUri}");

            var userConfig = await Service<ConfigService>().GetConfigByIdAsync(pushVM.UserId);

            await Service<WebPushService>().SendToAllEndPointsAsync(userConfig, pushVM);

            return Response();
        }

        internal async Task<DomainResponse> RegisterNotificationAsync(NotificationVM notif)
        {
            Telemetry.TrackEvent("Register Notification", notif.Id);

            Config userConfig = null;

            if (notif.Urgent)
            {
                userConfig = await Service<ConfigService>().GetConfigByIdAsync(notif.UserId);

                if (userConfig is null)
                {
                    ResetNoContentError();
                    return Response();
                }
            }

            return Response(await SaveNotificationAsync(notif, userConfig));
        }

        internal async Task DeleteAllByUser(string userId)
        {
            Telemetry.TrackEvent("Delete All Notifications of User", userId);

            var onesToDelete = Repository.Get<Notification>(a => a.UserId == userId);

            foreach (var toDelete in onesToDelete)
            {
                await Repository.DeleteAsync<Notification>(toDelete.Id, userId);
            }
        }

        internal async Task ReinforceByEmailAsync(DateTime from, DateTime to)
        {
            Telemetry.TrackEvent("Reinforce By Email", $"from: {from} to: {to}");

            var onesToReinforce = Repository.Get<Notification>(filter: a => !a.EmailSent &&
                                                                       a.ViewedAt == DateTime.MinValue &&
                                                                       (a.SentAt >= from && a.SentAt < to),
                                                               orderBy: n => n.UserId);

            Config userConfig = null;
            foreach (var toReinforce in onesToReinforce)
            {
                var notifToReinforeVM = NotificationVM.FactoryFrom(toReinforce);

                if (userConfig?.Id != toReinforce.UserId)
                    userConfig = await Service<ConfigService>().GetConfigByIdAsync(toReinforce.UserId);

                if (userConfig?.EmailChannel?.IsValid == true)
                {
                    var formatter = new FormatterByProfile(userConfig.Language, userConfig.TimeZone);

                    formatter.ApplyUserLanguage();

                    notifToReinforeVM.LongMessage = LightLocalizer.Localize("DID_YOU_SEE_THE_NOTIFICATION") + "> " + notifToReinforeVM.LongMessage;
                    notifToReinforeVM.Target = toReinforce.Target;
                    await SendEmailAsync(userConfig, notifToReinforeVM);

                    toReinforce.EmailSent = true;
                    await Repository.UpdateAsync(toReinforce);

                    formatter.RemoveUserLanguage();
                }
            }

            return;
        }

        internal DomainResponse GetTypes()
        {
            Telemetry.TrackEvent("Get Status Types");

            return Response(NotificationType.GetAll().OrderBy(s => s.Label));
        }

        private async Task SendEmailAsync(Config userConfig, NotificationVM notifVM)
        {
            var email = EmailMSG.FactoryFrom(notifVM);
            email.Subject = notifVM.ShortMessage;
            email.Message = GetContextMessage(notifVM.Id, notifVM.ShortMessage, notifVM.LongMessage, notifVM.Target);

            await Service<EmailService>().SendAsync(userConfig, email);
        }

        private async Task SendUrgentTextAsync(Config userConfig, NotificationVM notifVM)
        {
            var text = FactoryLightMessage<ShortTextMSG>(ShortTextCMD.Send);
            text.Message = GetUrgentContextMessage(notifVM.Id, notifVM.ShortMessage, notifVM.Target);
            text.UserId = userConfig.Id;
            text.Type = NotificationType.Tasks.Code;

            await Service<TextService>().SendAsync(userConfig, text);
        }

        private static string GetUrgentContextMessage(string id, string shortMessage, string target)
        {
            return LightLocalizer.Localize("URGENT_NOTIF_TEXT_MESSAGE", shortMessage, GetContextLink(id, target));
        }

        private static string GetContextMessage(string id, string shortMessage, string longMessage, string target)
        {
            if (string.IsNullOrWhiteSpace(longMessage))
            {
                longMessage = LightLocalizer.Localize("WE_HAVE_GOT_A_NEW_MESSAGE") + "> " + shortMessage + " ";
            }
            else
            {
                longMessage += ": ";
            }
            return longMessage + GetContextLink(id, target);
        }

        private static string GetContextLink(string id, string target)
        {
            return GetAppUrlFrom(target) + $"notifs/{id}/read";
        }

        private async Task<NotificationVM> SaveNotificationAsync(NotificationVM notif, Config userConfig, bool reinforceByEmail = false)
        {
            var notification = Notification.FactoryFrom(notif);
            notification.EmailSent = reinforceByEmail || notif.Urgent;
            var added = await Repository.AddAsync(notification);

            if (added.EmailSent)
            {
                var formatter = new FormatterByProfile(userConfig.Language, userConfig.TimeZone);

                formatter.ApplyUserLanguage();

                await SendEmailAsync(userConfig, notif);

                if (notif.Urgent)
                    await SendUrgentTextAsync(userConfig, notif);

                formatter.RemoveUserLanguage();
            }

            return NotificationVM.FactoryFrom(added);
        }

        private static Uri GetAppUrlFrom(string target)
        {
            if (target == NotificationTargetType.Prospect.Code)
                return PlatformServices.AppURLs["HomeAppURL"];
            else if (target == NotificationTargetType.Client.Code)
                return PlatformServices.AppURLs["ClientAppURL"];
            else if (target == NotificationTargetType.Staff.Code)
                return PlatformServices.AppURLs["EmployeeAppURL"];
            else
                return PlatformServices.AppURLs["MemberAppURL"];
        }
    }
}