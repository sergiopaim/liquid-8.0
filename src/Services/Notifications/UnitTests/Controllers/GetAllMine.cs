using Liquid.Base;
using Liquid.Domain;
using Liquid.Platform;
using Liquid.Domain.Test;
using System.Collections.Generic;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class GetAllMine(Fixture fixture) : LightUnitTestCase<GetAllMine, Fixture>(fixture)
    {
        [Theory]
        [InlineData("d084740b-9593-4727-be8d-bb5f1f716921")]
        public void Success(string userId)
        {
            var wrapper = Fixture.Api.WithRole(userId).Get<Response<List<NotificationVM>>>("mine");
            var response = wrapper.Content;
            var all = response.Payload;

            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);
            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.Equal(2, all.Count);
        }
    }
}