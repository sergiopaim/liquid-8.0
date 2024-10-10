using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class GetByIdAsync(Fixture fixture) : LightUnitTestCase<GetByIdAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("profGetById01")]
        public void Success(string testId)
        {
            var testData = LoadTestData<DomainResponse>(testId);

            var input = testData.Input;
            var id = input.Property("id").AsString();

            var expectedId = testData.Output.Payload.Property("id").AsString();

            var wrapper = Fixture.Api.Get<DomainResponse>($"{id}");

            var response = wrapper.Content;
            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);

            var resultId = response.Payload.Property("id").AsString();
            Assert.Equal(expectedId, resultId);
        }

        [Theory]
        [InlineData("profGetByIdNoContent")]
        public void UserNoContent(string testId)
        {
            var id = LoadTestData(testId).Input.Property("id").AsString();
            var wrapper = Fixture.Api.Get($"{id}");

            Assert.Equal(HttpStatusCode.NoContent, wrapper.StatusCode);
        }
    }
}