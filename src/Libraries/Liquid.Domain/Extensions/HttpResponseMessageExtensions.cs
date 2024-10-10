using Liquid.Base;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Liquid.Domain
{
    public static class HttpResponseMessageExtensions
    {
        /// <summary>
        /// Convert to LightDomain after response server
        /// </summary>
        /// <param name="response">Http response message</param>
        /// <returns>LightDomain</returns>
        public static async Task<DomainResponse> ConvertToDomainAsync(this HttpResponseMessage response)
        {
            var value = await (response?.Content?.ReadAsStringAsync());
            return (DomainResponse)Convert.ChangeType(new Liquid.Base.DomainResponse() { Payload = JsonDocument.Parse(value) }, typeof(DomainResponse));
        }
    }
}