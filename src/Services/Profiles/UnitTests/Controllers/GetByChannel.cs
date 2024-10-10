using Liquid.Base;
using Liquid.Domain;
using Liquid.Platform;
using Liquid.Domain.Test;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class GetByChannel(Fixture fixture) : LightUnitTestCase<GetByChannel, Fixture>(fixture)
    {
        [Theory]
        [InlineData("profGetIdByEmail01")]
        [InlineData("profGetIdByPhone01")]
        public void Success(string testId)
        {
            var testData = LoadTestData<DomainResponse>(testId);

            var input = testData.Input;
            var channel = input.Property("channel").AsString();

            var expectedId = testData.Output.Payload.Property("id").AsString();

            var wrapper = Fixture.Api.Get<Response<ProfileBasicVM>>($"byChannel/{channel}");
            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);

            var response = wrapper.Content;
            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);

            var profile = response.Payload;
            Assert.Equal(expectedId, profile.Id);
        }

        [Theory]
        [InlineData("profGetIdByEmailNoContent")]
        [InlineData("profGetIdByEmailAAD")]
        public void UserNoContent(string testId)
        {
            var channel = LoadTestData(testId).Input.Property("channel").AsString();
            var wrapper = Fixture.Api.Get($"byChannel/{channel}");

            Assert.Equal(HttpStatusCode.NoContent, wrapper.StatusCode);
        }
    }
}