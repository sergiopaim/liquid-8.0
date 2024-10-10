using Liquid.Base;
using Liquid.Domain;
using Liquid.Platform;
using Liquid.Domain.Test;
using System;
using System.Net;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class MarkMineAsViewedByIdAsync(Fixture fixture) : LightUnitTestCase<MarkMineAsViewedByIdAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("3aa9455a-ec1c-4178-8bfa-97d8891f1856", "c1d17649-4a01-41e9-b12f-2e56e403e8a7")]
        public void Success(string userId, string id)
        {
            var wrapper = Fixture.Api.WithRole(userId).Put<Response<NotificationVM>>($"mine/{id}");
            var response = wrapper.Content;
            var one = response.Payload;

            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);
            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.NotNull(one);

            wrapper = Fixture.Api.WithRole(userId).Get<Response<NotificationVM>>($"mine/{id}");
            response = wrapper.Content;
            var freshOne = response.Payload;
            Assert.NotEqual(DateTime.MinValue, freshOne.ViewedAt);
        }
    }
}