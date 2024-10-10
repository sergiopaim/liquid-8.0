using FluentValidation;
using Liquid.Activation;
using Liquid.Domain;
using Liquid.Runtime;
using System;

namespace Microservice.ViewModels
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class UpdateScheduleVM : LightViewModel<UpdateScheduleVM>
    {
        public string Id { get; set; }
        public string Microservice { get; set; }    // the microservice (e.g. Tasks)
        public string Job { get; set; }   // the name of the job to be run (e.g. Notify user of upcoming checkin)
        public string Frequency { get; set; }
        public int? PartitionCount { get; set; }
        public int? DayOfMonth { get; set; }
        public DayOfWeek? DayOfWeek { get; set; }
        public int? Hour { get; set; }
        public int? Minute { get; set; }
        public string Status { get; set; }

        public override void ValidateModel()
        {
            RuleFor(i => Id).NotEmpty().WithError("id must not be empty");
            RuleFor(i => Microservice).NotEmpty().WithError("microservice must not be empty");
            RuleFor(i => Job).NotEmpty().WithError("job must not be empty");

            RuleFor(i => Frequency).Must(
                f => string.IsNullOrWhiteSpace(f) || LightJobFrequency.IsValid(f)
            ).WithError("frequency is invalid");
            RuleFor(i => Status).Must(
                s => string.IsNullOrWhiteSpace(s) || LightJobStatus.IsValid(s)
            ).WithError("status is invalid");

            RuleFor(i => PartitionCount).NotEmpty().WithError("partitionCount must not be empty");
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}