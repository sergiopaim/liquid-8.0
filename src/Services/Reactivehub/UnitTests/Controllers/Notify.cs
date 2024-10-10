using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using Liquid.Platform;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class Notify(Fixture fixture) : LightUnitTestCase<Notify, Fixture>(fixture)
    {
        [Theory]
        [InlineData("notiNotificationSend")]
        public void NotSentAndForwarded(string testId)
        {
            var testData = LoadTestData<DomainResponse>(testId);
            var notiflVM = testData.Input;

            var wrapper = Fixture.Api.Post<DomainResponse>("notify", notiflVM);
            var response = wrapper.Content;

            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);

            var msgs = Fixture.MessageBus.InterceptedMessages.OfType<NotificationMSG>();
            Assert.Contains(msgs, i => i.CommandType == NotificationCMD.Send.Code);
        }

        [Theory]
        [InlineData("notiNotificationRegister")]
        public void SentAndRegistered(string testId)
        {
            var testData = LoadTestData<DomainResponse>(testId);
            var notiflVM = testData.Input;

            var wrapper = Fixture.Api.Post<DomainResponse>("notify", notiflVM);
            var response = wrapper.Content;

            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);

            var msgs = Fixture.MessageBus.InterceptedMessages.OfType<NotificationMSG>();

            Assert.Contains(msgs, i => i.CommandType == NotificationCMD.Register.Code);
        }
    }
}