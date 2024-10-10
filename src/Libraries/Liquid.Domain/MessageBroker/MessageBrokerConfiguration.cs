using FluentValidation;
using Liquid.Runtime;

namespace Liquid.Domain
{
    public class MessageBrokerConfiguration : LightConfig<MessageBrokerConfiguration>
    {
        public string ConnectionString { get; set; }

        public override void ValidateModel()
        {
            RuleFor(d => ConnectionString).NotEmpty().WithError("ConnectionString settings should not be empty.");
            RuleFor(d => ConnectionString).Matches("Endpoint=sb://").WithError("No Endpoint on configuration string has been informed.");
            RuleFor(d => ConnectionString).Matches("SharedAccessKeyName=").WithError("No SharedAccessKeyName on configuration string has been informed.");
            RuleFor(d => ConnectionString).Matches("SharedAccessKey=").WithError("No SharedAccessKey on configuration string has been informed.");
        }
    }
}