﻿using Liquid.Base;

namespace Liquid.Interfaces
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// Inteface to LightWorker controller
    /// </summary>
    public interface ILightWorker : IWorkBenchService
    {
        public ILightTelemetry Telemetry { get; }
        public ILightContext SessionContext { get; }
        public ICriticHandler CriticHandler { get; }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}