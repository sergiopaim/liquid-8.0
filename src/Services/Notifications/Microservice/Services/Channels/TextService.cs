using Liquid;
using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.API;
using Liquid.Platform;
using Microservice.Configuration;
using Microservice.Models;
using System;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microservice.Services
{
    internal class TextService : LightService
    {
        internal async Task<DomainResponse> SendAsync(ShortTextMSG msg)
        {
            Config userConfig = null;

            if (!string.IsNullOrWhiteSpace(msg.UserId))
            {
                userConfig = await Service<ConfigService>().GetConfigByIdAsync(msg.UserId);
                if (userConfig is null)
                    throw new BusinessLightException($"userId {msg.UserId} has no notification config data");
            }
            else if (msg.Type != NotificationType.Direct.Code)
                return BadRequest("userId must be informed for notification types other then direct");
            else if (string.IsNullOrWhiteSpace(msg.Phone))
                return BadRequest("direct notifications must have phone number");

            return await SendAsync(userConfig, msg);
        }

        internal async Task<DomainResponse> SendAsync(Config userConfig, ShortTextMSG msg)
        {
            Telemetry.TrackEvent("Send Text", userConfig?.Id ?? msg.Phone);

            var toNumber = string.IsNullOrWhiteSpace(msg.Phone)
                              ? userConfig?.PhoneChannel?.Phone
                              : msg.Phone;

            if (string.IsNullOrWhiteSpace(toNumber))
                return BusinessWarning("PHONE_NUMBER_IS_NULL_WARN", userConfig?.Id ?? msg.UserId);

            if (NotificationType.IsToSendOnlyIfChannelIsValid(msg.Type) &&
                !(userConfig?.PhoneChannel?.IsValid == true))
            {
                Telemetry.TrackTrace($"Phone number '{toNumber}' is not valid yet. Message was ignored!");
                return Response();
            }

            toNumber = toNumber.Replace("+", "")
                               .Replace("-", "")
                               .Replace("(", "")
                               .Replace(")", "")
                               .Replace(" ", "");

            var message = ApplyMacros(msg.Message, msg.ShowSender);

            if (toNumber.StartsWith("5500") &&
                (WorkBench.IsDevelopmentEnvironment ||
                 WorkBench.IsIntegrationEnvironment ||
                 WorkBench.IsQualityEnvironment ||
                 WorkBench.IsDemonstrationEnvironment))
            {
                if (NotificationConfig.textSendToTestUsers == true)
                {
                    toNumber = NotificationConfig.textPhoneForTestUsers;

                    if (userConfig is null)
                        await CaptureTextAsync(toNumber, message);
                    else
                        await CaptureTextAsync(userConfig, toNumber, message);
                }
                else
                {
                    await CaptureTextAsync(toNumber, message);
                }
                return Response();
            }

            string apiErrorMsg = null;
            JsonDocument response = default;
            ApiWrapper api = new("TEXT_GATEWAY");

            try
            {
                var result = api.Get<JsonDocument>($"send?key={NotificationConfig.textGatewayKey}&type=9&number={toNumber}&msg={message}&flash=1&refer={SessionContext.OperationId}");
                response = result.Content;

                if (result.StatusCode != HttpStatusCode.OK ||
                    response.Property("situacao").AsString() != "OK")
                {
                    apiErrorMsg = response.ToJsonString();
                }
            }
            catch (Exception)
            {
                Thread.Sleep(5000);
                //Performs ONE API call retry after 5 seconds
                try
                {
                    var result = api.Get<JsonDocument>($"send?key={NotificationConfig.textGatewayKey}&type=9&number={toNumber}&msg={message}");
                    response = result.Content;

                    if (result.StatusCode != HttpStatusCode.OK ||
                        response.Property("situacao").AsString() != "OK")
                    {
                        apiErrorMsg = response?.ToJsonString();
                    }
                }
                catch (Exception e)
                {
                    apiErrorMsg = e.ToString();
                }
            }

            if (apiErrorMsg is null)
            {
                Telemetry.TrackTrace($"toNumber: {toNumber}");
                return Response(data: new { messageId = response.Property("id").AsString() });
            }
            else
            {
                Telemetry.TrackTrace($"send?##KEY##&type=9&number={toNumber}&msg={message}");
                Telemetry.TrackTrace($"ErrorMessage:\n'{apiErrorMsg}'");
                throw new BusinessLightException("SMS_GATEWAY_UNAVAILABLE");
            }
        }

        private async Task CaptureTextAsync(string toPhone, string message)
        {
            var textAsEmailMSG = FactoryLightMessage<EmailMSG>(EmailCMD.Send);

            textAsEmailMSG.Type = NotificationType.Direct.Code;
            textAsEmailMSG.Email = "any@persons.com";
            textAsEmailMSG.Subject = LightLocalizer.Localize("TEXT_CAPTURED", toPhone);
            textAsEmailMSG.Message = message;

            WorkBench.ConsoleWriteLine($"{textAsEmailMSG.Subject}: {textAsEmailMSG.Message}");
            await Service<EmailService>().SendAsync(textAsEmailMSG);
        }

        private async Task CaptureTextAsync(Config userConfig, string toPhone, string message)
        {
            var textAsEmailMSG = FactoryLightMessage<EmailMSG>(EmailCMD.Send);

            var oldlanguage = FormatterByProfile.SetCurrentLanguage(userConfig?.Language);

            textAsEmailMSG.UserId = userConfig?.Id;
            textAsEmailMSG.Type = NotificationType.Account.Code;
            textAsEmailMSG.Subject = LightLocalizer.Localize("TEXT_CAPTURED", toPhone);
            textAsEmailMSG.Message = message;

            FormatterByProfile.SetCurrentLanguage(oldlanguage);

            WorkBench.ConsoleWriteLine($"{textAsEmailMSG.Subject}: {textAsEmailMSG.Message}");
            await Service<EmailService>().SendAsync(userConfig, textAsEmailMSG);
        }

        private static string ApplyMacros(string text, bool showSender)
        {
            text = PlatformServices.ExpandAppUrls(text);

            // Workaround for SMSDev gateway bug when shortening https URIs *********************************
            // When SMS bug gets fixed or SMSDev is replaced, remove it

            text = text.Replace("//", "/");
            text = text.Replace("http:/", "http://");
            text = text.Replace("https:/", "http:///");

            // Workaround end *******************************************************************************

            if (showSender)
                text = NotificationConfig.textSender + text;

            return text;
        }
    }
}