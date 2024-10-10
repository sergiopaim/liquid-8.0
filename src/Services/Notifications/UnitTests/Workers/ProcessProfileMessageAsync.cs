using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using System.Net;
using Xunit;

namespace UnitTests.Workers
{
    [Collection("General")]
    public class ProcessProfileMessageAsync(Fixture fixture) : LightUnitTestCase<ProcessProfileMessageAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("notiUserMessageCreate")]
        [InlineData("notiUserMessageUpdate")]
        [InlineData("notiUserMessageDelete")]
        public void Success(string testId)
        {
            var userMSG = LoadTestData<DomainResponse>(testId).Input;

            var wrapper = Fixture.MessageBus.SendToTopic("user/profiles", userMSG);

            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);
            Assert.False(CriticHandler.FromResponse(wrapper.Content).HasBusinessErrors);
        }
    }
}