using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using Microservice.ViewModels;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class GetUserOfNotificationAsync(Fixture fixture) : LightUnitTestCase<GetUserOfNotificationAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("3aa9455a-ec1c-4178-8bfa-97d8891f1856", "c1d17649-4a01-41e9-b12f-2e56e403e8a7")]
        public void Success(string userId, string id)
        {
            var wrapper = Fixture.Api.Get<Response<BasicUserInfoVM>>($"{id}/userBasicInfo");
            var response = wrapper.Content;
            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);

            var user = response.Payload;
            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.Equal(userId, user.Id);
        }
    }
}