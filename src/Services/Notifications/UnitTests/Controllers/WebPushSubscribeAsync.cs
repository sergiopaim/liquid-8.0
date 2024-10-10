using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using Microservice.ViewModels;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class WebPushSubscribeAsync(Fixture fixture) : LightUnitTestCase<WebPushSubscribeAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("notiPostWebSubscription01")]
        [InlineData("notiPostWebSubscription02")]
        public void Success(string testId)
        {
            var testData = LoadTestData<Response<WebPushEndpointVM>>(testId);

            var input = testData.Input;
            var userId = input.Property("userId").AsString();
            var payload = input.Property("payload").ToJsonDocument();

            var expectedOutput = testData.Output.Payload;

            var wrapper = Fixture.Api
                                 .WithRole(userId)
                                 .Post<Response<WebPushEndpointVM>>($"mine/web/devices", payload);

            var response = wrapper.Content;
            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);

            var result = response.Payload;
            Assert.Equal(expectedOutput.DeviceId, result.DeviceId);
        }

        [Theory]
        [InlineData("notiPostWebSubscriptionChannelAlreadySubscribed")]
        public void ChannelAlreadySubscribed(string testId)
        {
            var testData = LoadTestData<DomainResponse>(testId);

            var input = testData.Input;
            var userId = input.Property("userId").AsString();
            var payload = input.Property("payload").ToJsonDocument();

            var wrapper = Fixture.Api
                                 .WithRole(userId)
                                 .Post($"mine/web/devices", payload);

            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);
        }
    }
}