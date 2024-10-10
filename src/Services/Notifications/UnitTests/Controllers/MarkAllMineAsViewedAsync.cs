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
    public class MarkAllMineAsViewedAsync(Fixture fixture) : LightUnitTestCase<MarkAllMineAsViewedAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("d084740b-9593-4727-be8d-bb5f1f716921")]
        public void Success(string userId)
        {
            var wrapper = Fixture.Api.WithRole(userId).Put<Response<List<NotificationVM>>>("mine");
            var response = wrapper.Content;
            var marked = response.Payload;

            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);
            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.Equal(2, marked.Count);

            wrapper = Fixture.Api.WithRole(userId).Get<Response<List<NotificationVM>>>("mine");
            response = wrapper.Content;
            var fressAll = response.Payload;

            Assert.Equal(2, fressAll.Count);

            foreach (var freshOne in fressAll)
            {
                Assert.NotEqual(DateTime.MinValue, freshOne.ViewedAt);
            }
        }
    }
}