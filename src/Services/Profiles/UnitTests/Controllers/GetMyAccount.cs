using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class GetMyAccount(Fixture fixture) : LightUnitTestCase<GetMyAccount, Fixture>(fixture)
    {
        [Theory]
        [InlineData("profGetMyAccount01")]
        public void Success(string testId)
        {
            var testData = LoadTestData<DomainResponse>(testId);

            var input = testData.Input;
            var role = input.Property("role").AsString();

            var expectedId = testData.Output.Payload.Property("id").AsString();

            var wrapper = Fixture.Api
                                 .WithRole(role)
                                 .Get<DomainResponse>("me/account");
            var response = wrapper.Content;
            var resultId = response.Payload.Property("id").AsString();

            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.Equal(expectedId, resultId);
        }

        [Theory]
        [InlineData("Guest")]
        public void Unauthorized(string role)
        {
            var wrapper = Fixture.Api.WithRole(role).Get("me/account");

            Assert.Equal(HttpStatusCode.Unauthorized, wrapper.StatusCode);
        }
    }
}