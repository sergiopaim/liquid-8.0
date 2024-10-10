using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Liquid.Domain
{
    /// <summary>
    /// Helper class to deal with email addresses
    /// </summary>
    public static partial class EmailAddress
    {
        /// <summary>
        /// Check if a string is a valid email address
        /// </summary>
        /// <param name="emailAddress">The string to check</param>
        /// <returns>True if the emailAddress is a valid one</returns>
        public static bool IsValid(string emailAddress)
        {
            return EmailRegex().Match(emailAddress).Success;            
        }

        /// <summary>
        /// Check if a string is either null or a valid email address
        /// </summary>
        /// <param name="emailAddress">The string to check</param>
        /// <returns>True if the emailAddress is null or a valid email addressr</returns>
        public static bool IsNullOrValid(string emailAddress)
        {
            return emailAddress is null || IsValid(emailAddress);
        }

        /// <summary>
        /// Check if a string is either null, string.empty or a valid email address
        /// </summary>
        /// <param name="emailAddress">The string to check</param>
        /// <returns>True if the emailAddress is null, string.empty or a valid email addressr</returns>
        public static bool IsNullOrEmptyOrValid(string emailAddress)
        {
            return string.IsNullOrEmpty(emailAddress) || IsValid(emailAddress);
        }

        /// <summary>
        /// Checks if a domain has an email service defined
        /// </summary>
        /// <param name="address">The email or domain address to check</param>
        /// <returns>True if it is a valid email domain</returns>
        public static bool CheckDomain(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return false;

            List<string> invalidDomains =
            [
                "gmail.com.br",
                "live.com.br",
                "icloud.com.br"
            ];

            List<string> validDomains =
            [
                "gmail.com",
                "outlook.com",
                "hotmail.com",
                "live.com",
                "icloud.com",
                "yahoo.com",
                "outlook.com.br",
                "hotmail.com.br",
                "yahoo.com.br",
                "uol.com.br",
                "terra.com.br"
            ];

            string domain = address.Split('@').LastOrDefault().Trim();

            if (invalidDomains.Contains(domain))
                return false;

            if (validDomains.Contains(domain))
                return true;

            if (!IsValid($"some.name@{domain}"))
                return false;

            try
            {
                var dnsClient = new DnsClient.LookupClient();
                var mxRecords = dnsClient.Query(domain, DnsClient.QueryType.MX);

                return mxRecords?.Answers?.MxRecords()?.Any() == true;
            }
            catch
            {
                return false;
            }
        }

        [GeneratedRegex(@"\A(?:[a-z0-9_-]+(?:\.[a-z0-9_-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z")]
        private static partial Regex EmailRegex();
    }
}