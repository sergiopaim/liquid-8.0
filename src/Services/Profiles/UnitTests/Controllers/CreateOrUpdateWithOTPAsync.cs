using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using Liquid.Platform;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace UnitTests.Controllers
{
    [Collection("General")]
    public class CreateOrUpdateWithOTPAsync(Fixture fixture) : LightUnitTestCase<CreateOrUpdateWithOTPAsync, Fixture>(fixture)
    {
        [Theory]
        [InlineData("profCreateWithOTP_Success_New")]
        [InlineData("profCreateWithOTP_Success_Update")]
        public void Success(string testId)
        {
            var testData = LoadTestData<JsonDocument>(testId);

            var input = testData.Input;
            var expectedOutput = testData.Output.Property("payload").ToObject<ProfileWithOTPVM>();
            var expectedCommand = testData.Output.Property("command").ToObject<string>();

            var wrapper = Fixture.Api.Post<Response<ProfileWithOTPVM>>("", input);
            var response = wrapper.Content;
            var result = response.Payload;

            Assert.False(CriticHandler.FromResponse(response).HasBusinessErrors);
            Assert.Equal(expectedOutput?.Name, result?.Name);

            var msg = Fixture.MessageBus.InterceptedMessages.OfType<ProfileMSG>().FirstOrDefault();

            Assert.True((msg is null && expectedCommand is null) ||
                        (msg.CommandType == expectedCommand && msg.Id == expectedOutput?.Id));
        }
    }
}