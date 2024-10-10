using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class GetMe(Fixture fixture) : LightUnitTestCase<GetMe, Fixture>(fixture)
    {
        [Theory]
        [InlineData("profGetMeMember")]
        [InlineData("profGetMeBOAdmin")]
        public void Success(string testId)
        {
            var testData = LoadTestData<DomainResponse>(testId);

            var input = testData.Input;
            var role = input.Property("role").AsString();

            var expectedId = testData.Output.Payload.Property("id").AsString();

            var wrapper = Fixture.Api
                                 .WithRole(role)
                                 .Get<DomainResponse>("me");
            var response = wrapper.Content;
            var resultId = response.Payload.Property("id").AsString();

            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.Equal(expectedId, resultId);
        }

        [Theory]
        [InlineData("UnknownUser")]
        public void UserNoContent(string role)
        {
            var wrapper = Fixture.Api.WithRole(role).Get("me");

            Assert.Equal(HttpStatusCode.NoContent, wrapper.StatusCode);
        }

        [Theory]
        [InlineData("Guest")]
        public void Unauthorized(string role)
        {
            var wrapper = Fixture.Api.WithRole(role).Get("me");

            Assert.Equal(HttpStatusCode.Unauthorized, wrapper.StatusCode);
        }

    }
}