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
    public class GetByIdsAsync(Fixture fixture) : LightUnitTestCase<GetByIdsAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData((string[])[ "c1183078-3c0f-4767-9f9c-ade0531fd139" ], 1)]
        [InlineData((string[])[ "c1183078-3c0f-4767-9f9c-ade0531fd139", "6c5f063d-22f7-4635-9849-02ea54d9b9cc" ], 2)]
        [InlineData((string[])[ "c1183078-3c0f-4767-9f9c-ade0531fd139", "21cc120c-7874-4a89-b971-fd7d756abcb2" ], 1)]
        [InlineData((string[])[ "" ], 0)]
        public void Success(string[] ids, int quantity)
        {
            var wrapper = Fixture.Api.Get<Response<List<JsonDocument>>>($"byIds?ids={string.Join("&ids=", ids)}");

            var response = wrapper.Content;
            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.True(response.Payload.Count == quantity);
        }

        [Theory]
        [InlineData("Guest")]
        public void Unauthorized(string role)
        {
            var wrapper = Fixture.Api.WithRole(role).Get("byIds?ids=c1183078-3c0f-4767-9f9c-ade0531fd139");

            Assert.Equal(HttpStatusCode.Unauthorized, wrapper.StatusCode);
        }
    }
}