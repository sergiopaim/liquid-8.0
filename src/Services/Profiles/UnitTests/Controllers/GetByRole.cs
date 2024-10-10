using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class GetByRole(Fixture fixture) : LightUnitTestCase<GetByRole, Fixture>(fixture)
    {

        [InlineData("generalAdmin", 1)]
        public void Success(string roleName, int quantity)
        {
            var wrapper = Fixture.Api.Get<Response<List<JsonDocument>>>($"byRole/{roleName}");

            var response = wrapper.Content;
            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.Equal(quantity, response.Payload.Count);
        }

        [Theory]
        [InlineData("Guest")]
        public void Unauthorized(string role)
        {
            var wrapper = Fixture.Api.WithRole(role).Get("byRole/someRole");

            Assert.Equal(HttpStatusCode.Unauthorized, wrapper.StatusCode);
        }
    }
}