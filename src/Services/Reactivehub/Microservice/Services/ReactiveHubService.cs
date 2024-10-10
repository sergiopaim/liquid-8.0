using Liquid;
using Liquid.Activation;
using Liquid.Base;
using Liquid.Domain;
using Liquid.Interfaces;
using Liquid.OnAzure;
using Liquid.Platform;
using Microservice.Events;
using Microservice.Models;
using Microservice.ReactiveHubs;
using System;
using System.Threading.Tasks;
using System.Web;

namespace Microservice.Services
{
    internal class ReactiveHubService : LightService
    {
        private const int MINUTES_TO_LIVE = 60;

        #region Service operations
        static readonly MessageBus<ServiceBus> userNotifsBus = new("TRANSACTIONAL", "user/notifs");
        static readonly MessageBus<ServiceBus> userPushesBus = new("TRANSACTIONAL", "user/pushes");

        internal async Task<DomainResponse> NotifyAsync(NotificationVM notifVM)
        {
            NotificationMSG notifMSG;

            notifVM.SentAt = WorkBench.UtcNow;
            notifVM.Id = Guid.NewGuid().ToString();

            try
            {
                if (await SendToAllUserConnectionsAsync(notifVM))
                {
                    notifMSG = FactoryLightMessage<NotificationMSG>(NotificationCMD.Register);
                    notifMSG.MapFrom(notifVM);

                    await userNotifsBus.SendToQueueAsync(notifMSG);
                }
                else
                {
                    notifMSG = FactoryLightMessage<NotificationMSG>(NotificationCMD.Send);
                    notifMSG.MapFrom(notifVM);

                    await userNotifsBus.SendToQueueAsync(notifMSG, minutesToLive: MINUTES_TO_LIVE);
                }
            }
            catch (Exception ex) 
            {
                Telemetry.TrackException(new LightException("Error while sending reactive notifications", ex));
            }

            return Response();
        }

        internal async Task<DomainResponse> NotifyDomainEventAsync(DomainEventMSG domainEventMSG)
        {
            DomainEV domainEV = new()
            {
                Name = domainEventMSG.Name,
                ShortMessage = domainEventMSG.ShortMessage,
                Payload = domainEventMSG.Payload
            };

            foreach (var connId in domainEventMSG.AnonConns)
                await SentToConnectionAsync(nameof(GeneralHub.DomainEvent), connId, null, domainEV);

            foreach (var userId in domainEventMSG.UserIds)
            {
                bool sentAtLeastOnce = false;
                var connsQueried = Repository.Get<Connection>(c => c.UserId == userId);
                foreach (var conn in connsQueried)
                {
                    if (await SentToConnectionAsync(nameof(GeneralHub.DomainEvent), conn.Id, userId, domainEV))
                    {
                        sentAtLeastOnce = true;
                    }
                }
                if (domainEventMSG.PushIfOffLine && !sentAtLeastOnce)
                    await NotifyDomainEventViaPushAsync(userId, domainEV);
            }

            return Response();
        }

        private async Task NotifyDomainEventViaPushAsync(string userId, DomainEV domainEV)
        {
            var notifMSG = FactoryLightMessage<PushMSG>(PushCMD.Send);

            notifMSG.ShortMessage = domainEV.ShortMessage;
            notifMSG.UserId = userId;
            notifMSG.ContextUri = $"/event/domain?name={domainEV.Name}" +
                                  $"&payload={HttpUtility.UrlEncode(domainEV.Payload.ToJsonString())}";

            await userPushesBus.SendToQueueAsync(notifMSG, minutesToLive: MINUTES_TO_LIVE);
        }

        #endregion

        #region Send event operations

        private async Task<bool> SendToAllUserConnectionsAsync(NotificationVM notifVM)
        {
            bool sentAtLeastOnce = false;
            var notifEV = NotificationEV.FactoryFrom(notifVM);

            var connsQueried = Repository.Get<Connection>(c => c.UserId == notifVM.UserId);
            foreach (var conn in connsQueried)
            {
                if (await SentToConnectionAsync(nameof(GeneralHub.NotificationSent), conn.Id, notifVM.UserId, notifEV))
                {
                    sentAtLeastOnce = true;
                }
            }
            return sentAtLeastOnce;
        }

        internal async Task SendToUserSessions(string connectionId, UserSessionEV notifHandle)
        {
            var connsQueried = Repository.Get<Connection>(c => c.UserId == CurrentUserId && c.Id != connectionId);
            foreach (var conn in connsQueried)
            {
                await SentToConnectionAsync(nameof(GeneralHub.UserSessionEvent), conn.Id, CurrentUserId, notifHandle);
            }
        }

        #endregion

        #region Connection Management
        internal async Task<DomainResponse> RegisterConnectionAsync(string connectionId)
        {
            if (CurrentUserId is null)
                return Response();

            var conn = await Repository.GetByIdAsync<Connection>(connectionId, CurrentUserId);

            if (conn is not null)
                return BusinessWarning("CONNECTION_ID_ALREADY_REGISTERED");

            var newConn = new Connection
            {
                Id = connectionId,
                UserId = CurrentUserId
            };
            await Repository.AddAsync(newConn);

            return Response();
        }

        internal async Task<DomainResponse> RemoveConnectionAsync(string connectionId, string userId = null)
        {
            userId ??= CurrentUserId;

            if (userId is null)
                return Response();

            var conn = await Repository.GetByIdAsync<Connection>(connectionId, userId);

            if (conn is null)
                return BusinessWarning("CONNECTION_NOT_FOUND");

            await Repository.DeleteAsync<Connection>(conn.Id, CurrentUserId);

            return Response();
        }

        private async Task<bool> SentToConnectionAsync(string methodName, string connectionId, string userId, ILightReactiveEvent reactiveEvent)
        {
            if (!await LightHubConnection.InvokeAsync(methodName, connectionId, reactiveEvent))
            {
                //Only removes from DB authenticated connections
                if (userId is not null)
                    await RemoveConnectionAsync(connectionId, userId);
                return false;
            }
            return true;
        }

        #endregion
    }
}