using FluentValidation;
using Liquid;
using Liquid.Domain;
using Liquid.Runtime;

namespace Microservice.Configuration
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class NotificationConfig : LightConfig<NotificationConfig>
    {
        public string AWSAcessKeyId { get; set; }
        public string AWSSecretAccessKey { get; set; }
        public string AWSReturnPath { get; set; }

        public string AADTenantId { get; set; }
        public string AADServicePrincipalId { get; set; }
        public string AADServicePrincipalPassword { get; set; }

        public string VapidSubject { get; set; }
        public string VapidPublicKey { get; set; }
        public string VapidPrivateKey { get; set; }
        public string TextGatewayKey { get; set; }
        public string TextSender { get; set; }
        public bool TextSendToTestUsers { get; set; }
        public string TextPhoneForTestUsers { get; set; }

        public override void ValidateModel()
        {
            RuleFor(v => v.AWSAcessKeyId).NotEmpty().WithError($"AWSAccessKeyId cannot be empty in the 'Notification' config entry in app.settings.{WorkBench.EnvironmentName} file.");
            RuleFor(v => v.AWSSecretAccessKey).NotEmpty().WithError($"AWSSecretAccessKey cannot be empty in the 'Notification' config entry in app.settings.{WorkBench.EnvironmentName} file.");
            RuleFor(v => v.AWSReturnPath).Must(EmailAddress.IsValid).WithError($"AWSReturnPath must be a valid email addressin the 'Notification' config entry in app.settings.{WorkBench.EnvironmentName} file.");

            RuleFor(v => v.AADTenantId).NotEmpty().WithError("AADTenantId must not be empty");
            RuleFor(v => v.AADServicePrincipalId).NotEmpty().WithError("AADServicePrincipalId must not be empty");
            RuleFor(v => v.AADServicePrincipalPassword).NotEmpty().WithError("AADServicePrincipalPassword must not be empty");

            RuleFor(v => v.VapidSubject).NotEmpty().WithError($"VapidSubject cannot be empty in the 'Notification' config entry in app.settings.{WorkBench.EnvironmentName} file.");
            RuleFor(v => v.VapidPublicKey).NotEmpty().WithError($"VapidPublicKey cannot be empty in the 'Notification' config entry in app.settings.{WorkBench.EnvironmentName} file.");
            RuleFor(v => v.VapidPrivateKey).NotEmpty().WithError($"VapidPrivateKey cannot be empty in the 'Notification' config entry in app.settings.{WorkBench.EnvironmentName} file.");

            RuleFor(v => v.TextGatewayKey).NotEmpty().WithError($"TextGatewayKey cannot be empty in the 'Notification' config entry in app.settings.{WorkBench.EnvironmentName} file.");
            if (TextSendToTestUsers == true)
                RuleFor(v => v.TextPhoneForTestUsers).NotEmpty().WithError($"If TextSendToTestUsers is true, TextPhoneForTestUsers cannot be empty in the 'Notification' config entry in app.settings.{WorkBench.EnvironmentName} file.");
        }

        private static readonly NotificationConfig _value = LightConfigurator.LoadConfig<NotificationConfig>("Notification");

#pragma warning disable IDE1006 // Naming Styles
        public static string awsAcessKeyId => _value?.AWSAcessKeyId;
        public static string awsSecretAccessKey => _value?.AWSSecretAccessKey;
        public static string awsReturnPath => _value?.AWSReturnPath;

        public static string aadTenantId => _value?.AADTenantId;
        public static string aadServicePrincipalId => _value?.AADServicePrincipalId;
        public static string aadServicePrincipalPassword => _value?.AADServicePrincipalPassword;

        public static string vapidSubject => _value?.VapidSubject;
        public static string vapidPublicKey => _value?.VapidPublicKey;
        public static string vapidPrivateKey => _value?.VapidPrivateKey;
        public static string textGatewayKey => _value?.TextGatewayKey;
        public static string textSender => _value?.TextSender ?? "";
        public static bool? textSendToTestUsers => _value?.TextSendToTestUsers;
        public static string textPhoneForTestUsers => _value?.TextPhoneForTestUsers;
    }
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}