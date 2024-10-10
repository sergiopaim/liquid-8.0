﻿using FluentValidation;
using Liquid.Domain;
using Liquid.Platform;
using Liquid.Repository;
using Liquid.Runtime;
using System.Collections.Generic;

namespace Microservice.Models
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class PhoneChannel : LightValueObject<PhoneChannel>
    {
        public string Phone { get; set; }
        public bool IsValid { get; set; }
        public List<string> NotificationTypes { get; set; } = [];

        public override void ValidateModel()
        {
            RuleFor(i => i.Phone).Must(PhoneNumber.IsNullOrEmptyOrValid).WithError("invalid phone number");
            RuleFor(e => e.NotificationTypes).NotEmpty().WithError("notificationTypes must not be empty");
            RuleFor(e => e.NotificationTypes).Must(NotificationType.IsValid).WithError("notificationTypes are invalid");
        }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}