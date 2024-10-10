using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using System.Net;
using Xunit;

namespace UnitTests.Workers
{
    [Collection("General")]
    public class ProcessTextMessagesAsync(Fixture fixture) : LightUnitTestCase<ProcessTextMessagesAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("notiTextMessageSend")]
        public void Success(string testId)
        {
            var shortTextMSG = LoadTestData<DomainResponse>(testId).Input;

            var wrapper = Fixture.MessageBus.SendToQueue("user/text", shortTextMSG);

            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);
            Assert.False(CriticHandler.FromResponse(wrapper.Content).HasBusinessErrors);
        }
    }
}