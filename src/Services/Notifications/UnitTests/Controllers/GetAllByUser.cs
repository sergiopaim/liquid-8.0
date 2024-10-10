using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using Microservice.ViewModels;
using System.Collections.Generic;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class GetAllByUser(Fixture fixture) : LightUnitTestCase<GetAllByUser, Fixture>(fixture)
    {
        [Theory]
        [InlineData("d084740b-9593-4727-be8d-bb5f1f716921")]
        public void Success(string userId)
        {
            var wrapper = Fixture.Api.Get<Response<List<HistoryVM>>>($"user/{userId}");
            var response = wrapper.Content;
            var all = response.Payload;

            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);
            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.Equal(3, all.Count);
        }
    }
}