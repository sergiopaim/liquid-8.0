using Liquid.Base;
using Liquid.Domain;
using Liquid.OnAzure;
using Microservice.Configuration;
using Microservice.Messages;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.IO;

namespace Microservice.Services
{
    internal class MSGraphService : LightService
    {
        static readonly MessageBus<ServiceBus> userBouncesBus = new("TRANSACTIONAL", "user/bounces");

        #region MS Graph Connection

        public static readonly ConfigurationManager<OpenIdConnectConfiguration> ConfigManager = new($"https://login.microsoftonline.com/{NotificationConfig.aadTenantId}/.well-known/openid-configuration",
                                                                                                    new OpenIdConnectConfigurationRetriever());

        private static readonly IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
                                                                                                    .Create(NotificationConfig.aadServicePrincipalId)
                                                                                                    .WithTenantId(NotificationConfig.aadTenantId)
                                                                                                    .WithClientSecret(NotificationConfig.aadServicePrincipalPassword)
                                                                                                    .Build();

        private static readonly GraphServiceClient msGraph = FactoryNewGraphServiceClient();

        #endregion

        #region Service Operations

        internal async Task<DomainResponse> RetrieveEmailBouncesAsync(DateTime from, DateTime to)
        {
            Telemetry.TrackEvent("Retrieve Email Bounces", $"from: {from} to: {to}");

            var bounces = await GetEmailBouncesAsync(from, to);

            if (bounces.Count != 0)
            {
                var msg = FactoryLightMessage<EmailBounceMSG>(EmailBounceCMD.Process);
                msg.From = from;
                msg.To = to;
                msg.Addresses = bounces;

                await userBouncesBus.SendToTopicAsync(msg);
            }

            return Response();
        }

        internal async Task<DomainResponse> TestGetEmailBouncesAsync(DateTime from, DateTime to)
        {
            return Response(await GetEmailBouncesAsync(from, to));
        }

        #endregion

        #region Private Methods

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
            }
                                                         );

            var client = new GraphServiceClient(auth);

            return client;
        }

        private static async Task<List<StatusByEmail>> GetEmailBouncesAsync(DateTime from, DateTime to)
        {
            const int PAGE_SIZE = 50;

            try
            {
                // Get the messages from the shared mailbox
                var request = msGraph.Users[NotificationConfig.awsReturnPath]
                                     .Messages
                                     .Request()
                                     .Filter($"receivedDateTime ge {from:yyyy-MM-ddTHH:mm:ssZ} and receivedDateTime lt {to:yyyy-MM-ddTHH:mm:ssZ}")
                                     .Top(PAGE_SIZE);

                List<StatusByEmail> addresses= [];

                while (request != null)
                {
                    var messages = await request.GetAsync();

                    foreach (var message in messages.CurrentPage.Where(m => m.ToRecipients is not null))
                    {
                        if (message.ToRecipients.Any(t => t.EmailAddress.Address == NotificationConfig.awsReturnPath))
                            await msGraph.Users[NotificationConfig.awsReturnPath]
                                         .Messages[message.Id]
                                         .Request()
                                         .DeleteAsync();
                        else
                        {
                            using var stream = await msGraph.Users[NotificationConfig.awsReturnPath]
                                                            .Messages[message.Id]
                                                            .Content
                                                            .Request()
                                                            .GetAsync();

                            var status = FindDeliveryStatusPart(stream);

                            addresses.AddRange(message.ToRecipients?.Select(r => new StatusByEmail()
                                                                                 {
                                                                                     Email = r?.EmailAddress?.Address,
                                                                                     Status = status
                                                                                 }) ?? []);
                        }
                    }

                    request = messages.NextPageRequest;
                }

                return [.. addresses.Where(a => !string.IsNullOrWhiteSpace(a.Email))
                                    .DistinctBy(a => a.Email)
                                    .OrderBy(a => a.Email)];
            }
            catch (Exception ex)
            {
                throw new LightException($"Failed to process email bounces: {ex.Message}", ex);
            }
        }

        #endregion

        #region Private Static Methods

        private static string FindDeliveryStatusPart(Stream mimeStream)
        {
            var message = MimeMessage.Load(mimeStream);
            MessageDeliveryStatus status = null;

            if (message.Body is Multipart multipart)
            {
                foreach (var part in multipart)
                {
                    if (part.ContentType.MediaType == "message" && part.ContentType.MediaSubtype == "delivery-status")
                    {
                        status = (MessageDeliveryStatus) part;
                    }

                    // Recursively search in nested multiparts
                    if (part is Multipart nestedMultipart)
                    {
                        var nestedPart = FindDeliveryStatusPart(nestedMultipart);
                        if (nestedPart != null)
                        {
                            status = nestedPart;
                        }
                    }
                }
            }

            if (status is null)
                return null;

            var reader = new StreamReader(status.Content.Open());
            return reader.ReadToEnd();
        }

        private static MessageDeliveryStatus FindDeliveryStatusPart(Multipart multipart)
        {
            foreach (var part in multipart)
            {
                if (part.ContentType.MediaType == "message" && part.ContentType.MediaSubtype == "delivery-status")
                {
                    return (MessageDeliveryStatus) part;
                }

                if (part is Multipart nestedMultipart)
                {
                    var nestedPart = FindDeliveryStatusPart(nestedMultipart);
                    if (nestedPart != null)
                    {
                        return nestedPart;
                    }
                }
            }

            return null;
        }

        #endregion
    }
}