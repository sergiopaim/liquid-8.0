using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class TestWebPushSendAsync(Fixture fixture) : LightUnitTestCase<TestWebPushSendAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("notiWebPushSend01")]
        public void Success(string testId)
        {
            var testData = LoadTestData<DomainResponse>(testId);

            var input = testData.Input;
            var payload = input.Property("payload").ToJsonDocument();

            var wrapper = Fixture.Api.Post<Response<int>>("test/webpush/send", payload);
            var response = wrapper.Content;
            var result = response.Payload;

            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.Equal(0, result);
        }

        [Theory]
        [InlineData("notiWebPushSendUserNoContent")]
        public void UserNoContent(string testId)
        {
            var input = LoadTestData(testId).Input;
            var payload = input.Property("payload").ToJsonDocument();

            var wrapper = Fixture.Api.Post("test/webpush/send", payload);

            Assert.Equal(HttpStatusCode.NoContent, wrapper.StatusCode);
        }
    }
}