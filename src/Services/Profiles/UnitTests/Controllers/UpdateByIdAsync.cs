using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class UpdateByIdAsync(Fixture fixture) : LightUnitTestCase<UpdateByIdAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("userPutById")]
        public void Success(string testId)
        {
            var testData = LoadTestData<DomainResponse>(testId);

            var input = testData.Input;
            var id = input.Property("id").AsString();
            var payload = input.Property("payload").ToJsonDocument();

            var output = testData.Output.Payload;

            var expectedId = output.Property("id").AsString();
            var expectedName = output.Property("name").AsString();

            var wrapper = Fixture.Api.Put<DomainResponse>(id, payload);
            var response = wrapper.Content;
            var resultId = response.Payload.Property("id").AsString();
            var name = response.Payload.Property("name").AsString();

            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);
            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.Equal(expectedId, resultId);
            Assert.Equal(expectedName, name);
        }

        [Theory]
        [InlineData("profPutByIdNoContent")]
        public void UserNoContent(string testId)
        {
            var input = LoadTestData<DomainResponse>(testId).Input;
            var id = input.Property("id").AsString();
            var payload = input.Property("payload").ToJsonDocument();

            var wrapper = Fixture.Api.Put(id, payload);

            Assert.Equal(HttpStatusCode.NoContent, wrapper.StatusCode);
        }

        [Theory]
        [InlineData("user1")]
        public void Unauthorized(string role)
        {
            var wrapper = Fixture.Api.WithRole(role).Put("anyId");

            Assert.Equal(HttpStatusCode.Forbidden, wrapper.StatusCode);
        }
    }
}