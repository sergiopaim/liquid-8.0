using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using Microservice.ViewModels;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class WebPushUnsubscribeAsync(Fixture fixture) : LightUnitTestCase<WebPushUnsubscribeAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("notiDeleteWebSubscription01")]
        [InlineData("notiDeleteWebSubscription02")]
        public void Success(string testId)
        {
            var testData = LoadTestData<Response<WebPushEndpointVM>>(testId);

            var input = testData.Input;
            var userId = input.Property("userId").AsString();
            var deviceId = input.Property("deviceId").AsString();

            var expectedOutput = testData.Output.Payload;

            var wrapper = Fixture.Api
                                 .WithRole(userId)
                                 .Delete<Response<WebPushEndpointVM>>($"mine/web/devices/{deviceId}");

            var response = wrapper.Content;
            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);

            var result = response.Payload;
            Assert.Equal(expectedOutput.DeviceId, result.DeviceId);
        }

        [Theory]
        [InlineData("notiDeleteWebSubscriptionChannelNoContent")]
        public void ChannelNoContent(string testId)
        {
            var input = LoadTestData(testId).Input;
            var userId = input.Property("userId").AsString();
            var deviceId = input.Property("deviceId").AsString();

            var wrapper = Fixture.Api
                                 .WithRole(userId).Delete($"mine/web/devices/{deviceId}");

            Assert.Equal(HttpStatusCode.NoContent, wrapper.StatusCode);
        }
    }
}