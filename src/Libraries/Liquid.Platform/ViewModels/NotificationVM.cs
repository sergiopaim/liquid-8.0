﻿using FluentValidation;
using Liquid.Domain;
using Liquid.Runtime;
using System;

namespace Liquid.Platform
{
    /// <summary>
    /// A user's profile with its editable attributes
    /// </summary>
    public class NotificationVM : LightViewModel<NotificationVM>
    {
        /// <summary>
        /// Notification's id
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// User's id
        /// </summary>
        public string UserId { get; set; }
        /// <summary>
        /// Type of notification
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// Target type of the notification
        /// </summary>
        public string Target { get; set; } = NotificationTargetType.Member.Code;
        /// <summary>
        /// Short message to be promptly shown to user
        /// </summary>
        public string ShortMessage { get; set; }
        /// <summary>
        /// Long message to be shown to user as further detail
        /// </summary>
        public string LongMessage { get; set; }
        /// <summary>
        /// Relative URI to solve/open the notification
        /// </summary>
        public string ContextUri { get; set; }
        /// <summary>
        /// DataTime at which the notification was sent
        /// </summary>
        public DateTime SentAt { get; set; }
        /// <summary>
        /// DataTime at which the notification was viewed by the user
        /// </summary>
        public DateTime ViewedAt { get; set; }
        /// <summary>
        /// Indication whether the notification is urgent
        /// </summary>
        public bool Urgent { get; set; }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override void ValidateModel()
        {
            RuleFor(i => i.UserId).NotEmpty().WithError("userId must not be empty");
            RuleFor(i => i.Type).NotEmpty().Must(NotificationType.IsValid).WithError("notificationType is invalid");
            RuleFor(i => i.Target).NotEmpty().Must(NotificationTargetType.IsValid).WithError("target is invalid");
            RuleFor(i => i.ShortMessage).NotEmpty().WithError("shortMessage must not be empty");
            RuleFor(i => i.ShortMessage).MaximumLength(140).WithError("shortMessage must be up to 140 chars");
        }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}