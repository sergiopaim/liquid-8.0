using Liquid.Base;
using Liquid.Domain;
using Liquid.Platform;
using Liquid.Domain.Test;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class RevertChannelByIdAsync(Fixture fixture) : LightUnitTestCase<RevertChannelByIdAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("c4e221c1-f445-41bf-b624-29a518a41c94", "ewogICJpZCI6ICJjNGUyMjFjMS1mNDQ1LTQxYmYtYjYyNC0yOWE1MThhNDFjOTQiLAogICJvdHAiOiAiOTg4MjEiCn0=", "jsantos@members.com", "+55 (00) 99998-8956")]
        public void Success(string accountId, string otpToken, string oldEmail, string oldPhone)
        {
            var wrapper = Fixture.Api
                                 .Anonymously()
                                 .Put<Response<ProfileVM>>($"{accountId}/channel/revert?otpToken={otpToken}");

            var response = wrapper.Content;
            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);

            var resultProfile = response.Payload;

            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.Equal(oldEmail, resultProfile.Email);
            Assert.Equal(oldPhone, resultProfile.Phone);
        }
    }
}