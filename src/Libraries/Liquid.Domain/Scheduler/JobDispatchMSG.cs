using FluentValidation;
using Liquid.Runtime;
using System;

namespace Liquid.Domain
{
    public class JobDispatchCMD(string code) : LightEnum<JobDispatchCMD>(code)
    {
        public static readonly JobDispatchCMD Trigger = new(nameof(Trigger));
        public static readonly JobDispatchCMD Abort = new(nameof(Abort));
    }

    public class JobDispatchMSG : LightJobMessage<JobDispatchMSG, JobDispatchCMD>
    {
        // The partition Id of the dispatch
        public int Partition { get; set; }
        // Datetime the dispatch was sheduled to activate
        public DateTime Activation { get; set; }

        public override void ValidateModel()
        {
            RuleFor(i => i.Microservice).NotEmpty().WithError("microservice must not be empty");
            RuleFor(i => i.Job).NotEmpty().WithError("job must not be empty");
        }
    }
}