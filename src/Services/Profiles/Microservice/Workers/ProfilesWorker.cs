using Liquid.Activation;
using Microservice.Messages;
using Microservice.Services;
using System.Threading.Tasks;

namespace Microservice.Workers
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    [MessageBus("TRANSACTIONAL")]
    public class ProfilesWorker : LightWorker
    {
        [Topic("user/bounces", "profiles", maxConcurrentCalls: 1, deleteAfterRead: false/*, sqlfilter: "CommandType = 0"*/)]
        public async Task ProcessEmailBouncesAsync(EmailBounceMSG msg)
        {
            ValidateInput(msg);

            if (msg?.CommandType == EmailBounceCMD.Process.Code)
                await Factory<ProfileService>().ProcessEmailBouncesAsync(msg.From, msg.To, msg.Addresses);

            Terminate();
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}