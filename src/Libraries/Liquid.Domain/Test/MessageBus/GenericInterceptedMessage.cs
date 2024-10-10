using System.Text.Json;

namespace Liquid.Domain.Test
{
    public enum EndpointType {
        QUEUE, TOPIC
    }

    public class GenericInterceptedMessage
    {
        public JsonDocument Message { get; set; }
        public string MessageType { get; set; }
        public EndpointType EndpointType { get; set; }
        public string TagConfigName { get; set; }
        public string ChannelName { get; set; }
    }
}