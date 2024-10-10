using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using System.Net;
using Xunit;

namespace UnitTests.Workers
{
    [Collection("General")]
    public class ProcessNotificationMessageAsync(Fixture fixture) : LightUnitTestCase<ProcessNotificationMessageAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("notiNotificationSend")]
        [InlineData("notiNotificationRegister")]
        public void Success(string testId)
        {
            var contextualMSG = LoadTestData<DomainResponse>(testId).Input;

            var wrapper = Fixture.MessageBus.SendToQueue("user/notifs", contextualMSG);

            Assert.Equal(HttpStatusCode.OK, wrapper.StatusCode);
            Assert.False(CriticHandler.FromResponse(wrapper.Content).HasBusinessErrors);
        }
    }
}