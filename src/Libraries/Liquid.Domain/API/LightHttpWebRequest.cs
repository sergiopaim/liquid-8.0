using System.Net;

namespace Liquid.Domain.API
{
    /// <summary>
    /// Helper for the <c>HttpWebRequest</c> class.
    /// Properly retrieves http responses instead of throwing exceptions.
    /// </summary>
    public static class LightHttpWebRequest
    {
        public static WebResponse GetResponse(WebRequest request)
        {
            try
            {
                return request?.GetResponse();
            }
            catch (WebException wex)
            {
                if (wex.Response is not null)
                {
                    return wex.Response;
                }
                throw new WebException("`WebException` caught but `Response` was null.");
            }
        }
    }
}