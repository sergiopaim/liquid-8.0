using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using System.Net;
using Xunit;

namespace UnitTests.Workers
{
    [Collection("General")]
    public class ProcessEmailMessageAsync(Fixture fixture) : LightUnitTestCase<ProcessEmailMessageAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("notiEmailMessageSend")]
        public void Success(string testId)
        {
            var emailMSG = LoadTestData<DomainResponse>(testId).Input;

            var wrapper = Fixture.MessageBus.SendToQueue("user/emails", emailMSG);

            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);
            Assert.False(CriticHandler.FromResponse(wrapper.Content).HasBusinessErrors);
        }
    }
}