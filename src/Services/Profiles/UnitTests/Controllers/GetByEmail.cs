using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class GetByEmail(Fixture fixture) : LightUnitTestCase<GetByEmail, Fixture>(fixture)
    {
        [Theory]
        [InlineData("profGetByEmail01")]
        public void Success(string testId)
        {
            var testData = LoadTestData<DomainResponse>(testId);

            var input = testData.Input;
            var email = input.Property("email").AsString();

            var expectedId = testData.Output.Payload.Property("id").AsString();

            var wrapper = Fixture.Api.Get<DomainResponse>($"byemail/{email}");

            var response = wrapper.Content;
            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);

            var resultId = response.Payload.Property("id").AsString();
            Assert.Equal(expectedId, resultId);
        }

        [Theory]
        [InlineData("profGetByEmailNoContent")]
        public void UserNoContent(string testId)
        {
            var email = LoadTestData(testId).Input.Property("email").AsString();
            var wrapper = Fixture.Api.Get($"byemail/{email}");

            Assert.Equal(HttpStatusCode.NoContent, wrapper.StatusCode);
        }

        [Theory]
        [InlineData("Guest")]
        public void Unauthorized(string role)
        {
            var wrapper = Fixture.Api.WithRole(role).Get("byemail/a");

            Assert.Equal(HttpStatusCode.Unauthorized, wrapper.StatusCode);
        }
    }
}