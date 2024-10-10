using Liquid.Base;
using Liquid.Domain;
using Liquid.Platform;
using Liquid.Domain.Test;
using Microservice.Models;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class RequestMyChannelsValidationAsync(Fixture fixture) : LightUnitTestCase<RequestMyChannelsValidationAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("João de Deus", "email", "49437", "jdedeus3@members.com")]
        [InlineData("João de Deus", "phone", "54783", "+55 (00) 99697-8956")]
        public void Success(string role, string channelType, string validationOTP, string validatedData)
        {
            var wrapper = Fixture.Api
                                 .WithRole(role)
                                 .Put<Response<ProfileVM>>($"me/channel/{channelType}/validate?validationOTP={validationOTP}");

            var response = wrapper.Content;

            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);

            var resultProfile = response.Payload;

            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.Equal(validatedData, channelType == ChannelType.Email.Code ? resultProfile.Email : resultProfile.Phone);
        }
    }
}