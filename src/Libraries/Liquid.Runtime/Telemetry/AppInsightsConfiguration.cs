﻿using FluentValidation;

namespace Liquid.Runtime
{
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// AppInsights Configurator will dynamically assign the authorization key for writing to AppInsights.
    /// The function caller should pass a configuration file containing the Azure key.
    /// </summary>
    internal class AppInsightsConfiguration : LightConfig<AppInsightsConfiguration>
    {
        //Duplicated with Liquid.OnAzure.Telemetry due to a non generalization of Runtime.Telemetry for many providers (such as AddTelemetry<T>()).

        /// <summary>
        /// Necessary connection string for sending data to telemetry. Otherwise no data will tracked.
        /// </summary>
        public string ConnectionString { get; set; }
        public bool EnableKubernetes { get; set; }
        public override void ValidateModel()
        {
            RuleFor(d => ConnectionString).NotEmpty().WithError("connectionString must not be empty");
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore IDE0079 // Remove unnecessary suppression
}