using Liquid.Base;
using Liquid.Domain;
using Liquid.Platform;
using Liquid.Domain.Test;
using System;
using System.Collections.Generic;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class ClearAllMineViewed(Fixture fixture) : LightUnitTestCase<ClearAllMineViewed, Fixture>(fixture)
    {
        [Theory]
        [InlineData("5e179f25-8d4d-43e9-b358-5320e740329f")]
        public void Success(string userId)
        {
            var wrapper = Fixture.Api
                                 .WithRole(userId)
                                 .Delete<Response<List<NotificationVM>>>("mine");
            var response = wrapper.Content;
            var deleted = response.Payload;

            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);
            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.Single(deleted);

            wrapper = Fixture.Api
                             .WithRole(userId)
                             .Get<Response<List<NotificationVM>>>("mine");
            response = wrapper.Content;
            var fressAll = response.Payload;

            Assert.Equal(2, fressAll.Count);

            fressAll.ForEach(f => Assert.Equal(DateTime.MinValue, f.ViewedAt));
        }
    }
}