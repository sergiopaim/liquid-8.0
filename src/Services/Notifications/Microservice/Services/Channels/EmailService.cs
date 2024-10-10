﻿using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Liquid;
using Liquid.Base;
using Liquid.Domain;
using Liquid.Platform;
using Liquid.Runtime.Telemetry;
using Microservice.Configuration;
using Microservice.Models;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using NotificationType = Liquid.Platform.NotificationType;

namespace Microservice.Services
{
    internal partial class EmailService : LightService
    {
        internal async Task<DomainResponse> SendAsync(EmailMSG msg)
        {
            Config userConfig = null;

            if (!string.IsNullOrWhiteSpace(msg.UserId))
            {
                userConfig = await Service<ConfigService>().GetConfigByIdAsync(msg.UserId);
                if (userConfig is null)
                    throw new BusinessLightException($"userId {msg.UserId} has no notification config data");
            }
            else if (msg.Type != Liquid.Platform.NotificationType.Direct.Code)
                return BadRequest("userId must be informed for notification types other then direct");
            else if (string.IsNullOrWhiteSpace(msg.Email))
                return BadRequest("direct notifications must have email address");

            return await SendAsync(userConfig, msg);
        }

        internal async Task<DomainResponse> SendAsync(Config userConfig, EmailMSG msg)
        {
            Telemetry.TrackEvent("Send Email", userConfig?.Id ?? msg.Email);

            var toAddress = string.IsNullOrWhiteSpace(msg.Email)
                               ? userConfig?.EmailChannel?.Email
                               : msg.Email;

            if (string.IsNullOrWhiteSpace(toAddress))
                return BusinessWarning("EMAIL_ADDRESS_IS_NULL_WARN", userConfig?.Id ?? msg.UserId);

            if (NotificationType.IsToSendOnlyIfChannelIsValid(msg.Type) &&
                !(userConfig?.EmailChannel?.IsValid == true))
            {
                Telemetry.TrackTrace($"E-mail '{toAddress}' is not valid yet. Message was ignored!");
                return Response();
            }

            string oldlanguage = FormatterByProfile.SetCurrentLanguage(userConfig?.Language);

            var subject = ApplyMacros(msg.Subject, userConfig);
            var message = msg.Message;

            var source = LightLocalizer.Localize("NO-REPLY_EMAIL_ADDRESS");

            FormatterByProfile.SetCurrentLanguage(oldlanguage);

            OverrideIfTestUser(ref subject, ref toAddress);

            var credentials = new BasicAWSCredentials(NotificationConfig.awsAcessKeyId, NotificationConfig.awsSecretAccessKey);
            using var client = new AmazonSimpleEmailServiceClient(credentials, RegionEndpoint.USEast1);
            var sendRequest = new SendEmailRequest
            {
                ReturnPath = NotificationConfig.awsReturnPath,
                Source = source,
                Destination = new()
                {
                    ToAddresses = [toAddress]
                },

                Message = new()
                {
                    Subject = new(subject),
                    Body = new()
                    {
                        Html = new()
                        {
                            Charset = "UTF-8",
                            Data = GetHtmlEmailFrom(message, userConfig)
                        },
                        Text = new()
                        {
                            Charset = "UTF-8",
                            Data = GetTextEmailFrom(message, userConfig)
                        }
                    }
                }
            };
            SendEmailResponse response;
            try
            {
                Telemetry.TrackTrace($"toAddress: {toAddress}");
                response = await client.SendEmailAsync(sendRequest);
            }
            catch (Exception)
            {
                try
                {
                    Thread.Sleep(1000);
                    Telemetry.TrackTrace($"RETRY: {toAddress}");
                    response = await client.SendEmailAsync(sendRequest);
                }
                catch (Exception ex2)
                {
                    ((LightTelemetry)Telemetry).TrackException(ex2);
                    AddBusinessError("FAILED_SENDING_EMAIL_THRU_AWS_SES");
                    return Response();
                }
            }

            return Response(new
            {
                messageId = response.MessageId
            });
        }

        private static string ApplyMacros(string text, Config user)
        {

            text = PlatformServices.ExpandAppUrls(text);
            string name = user?.Name ?? string.Empty;
            string firstName = name.Split(" ")[0];
            string greeting = string.IsNullOrWhiteSpace(firstName) ? string.Empty : $", {firstName}";

            text = text.Replace("{userName}", name, StringComparison.InvariantCulture);
            text = text.Replace("{userFirstName}", firstName, StringComparison.InvariantCulture);
            text = text.Replace("{userGreeting}", greeting, StringComparison.InvariantCulture);

            return text;
        }

        private static string GetTextEmailFrom(string message, Config user)
        {
            var oldLanguage = FormatterByProfile.SetCurrentLanguage(user?.Language);

            message = message.Replace("&#xA;", Environment.NewLine, StringComparison.InvariantCulture);
            message = message.Replace("\n", Environment.NewLine, StringComparison.InvariantCulture);

            string email = File.ReadAllText($"{Directory.GetCurrentDirectory()}/Resources/email.{CultureInfo.CurrentUICulture.Name}.txt", System.Text.Encoding.UTF8);
            email = email.Replace("{message}", message, StringComparison.InvariantCulture);

            var body = ApplyMacros(email, user);

            FormatterByProfile.SetCurrentLanguage(oldLanguage);

            return body;
        }

        private static string GetHtmlEmailFrom(string message, Config user)
        {
            var oldLanguage = FormatterByProfile.SetCurrentLanguage(user?.Language);

            message = ApplyMacros(message, user);

            message = ExpandPlainUrlsAsClickToSeeLinks(message);

            message = message.Replace("&#xA;", "<br/>", StringComparison.InvariantCulture);
            message = message.Replace("\n", "<br/>", StringComparison.InvariantCulture);

            string email = File.ReadAllText($"{Directory.GetCurrentDirectory()}/Resources/email.{CultureInfo.CurrentUICulture.Name}.html", System.Text.Encoding.UTF8);
            email = email.Replace("{message}", message, StringComparison.InvariantCulture);

            var body = ApplyMacros(email, user);

            FormatterByProfile.SetCurrentLanguage(oldLanguage);

            return body;
        }

        private static string ExpandPlainUrlsAsClickToSeeLinks(string message)
        {
            //Replaces URLs as <a> HTML tags
            string clickToOpen = LightLocalizer.Localize("CLICK_TO_SEE_HTML_LINK");

            HtmlDocument doc = new();
            doc.LoadHtml(message);


            // Select all text nodes that are not descendants of <a> tags
            var textNodes = doc.DocumentNode.SelectNodes("//text()[not(ancestor::a)]");

            if (textNodes == null)
                return message;

            foreach (var node in textNodes)
                // For each text node, replace URLs not enclosed in <a> tags
                node.InnerHtml = FindUrlRegex().Replace(node.InnerText, m => $"<strong><a target=\"_system\" href=\"{m.Value}\">{clickToOpen}</a></strong>");

            // Extract the updated HTML content
            return doc.DocumentNode.OuterHtml;
        }

        private static void OverrideIfTestUser(ref string subject, ref string toAddress)
        {
            string emailPrefix =
                WorkBench.IsDevelopmentEnvironment ? "dev" :
                WorkBench.IsIntegrationEnvironment ? "int" :
                WorkBench.IsQualityEnvironment ? "qa" :
                WorkBench.IsDemonstrationEnvironment ? "demo" : null;

            if (toAddress.EndsWith("@members.com") ||
                toAddress.EndsWith("@persons.com") ||
                toAddress.EndsWith("@clients.com") ||
                toAddress.EndsWith("@your-dev-domain.onmicrosoft.com"))
            {
                if (emailPrefix is not null)
                {
                    subject = $"{toAddress}: {subject}";
                    toAddress = $"{emailPrefix}-users@your-domain.com";
                }
            }
        }

        [GeneratedRegex(@"(?:https?:\/\/)((([A-Za-z]{3,9}:(?:\/\/)?|localhost:)(?:[-;:&=\+\$,\w]+@)?[A-Za-z0-9.-]+|(?:|[-;:&=\+\$,\w]+@)[A-Za-z0-9.-]+)((?:\/[\+~%\/.\w-_]*)?\??(?:[-\+=&;%@.\w_]*)#?(?:[\w]*))?)")]
        private static partial Regex FindUrlRegex();
    }
}