using FluentValidation;

namespace Liquid.Runtime
{
    /// <summary>
    /// Validates the host property from APIWrapper
    /// </summary>
    public class ApiConfiguration : LightConfig<ApiConfiguration>
    {
        public string Host { get; set; }
        public int? Port { get; set; }
        public string Suffix { get; set; }
        public bool Stub { get; set; }

        public override void ValidateModel()
        { 
            RuleFor(x => x.Host).NotEmpty().WithError("The Host property should be informed on API settings");
        }
    }
}