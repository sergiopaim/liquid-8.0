using FluentValidation;
using Liquid.Domain;
using Liquid.Repository;
using Liquid.Runtime;
using Microservice.ViewModels;
using System;

namespace Microservice.Models
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    public class Channels : LightValueObject<Channels>
    {
        public string Email { get; set; }
        public bool EmailIsValid { get; set; }
        public string EmailToChange { get; set; }
        public string LastValidEmail { get; set; }
        public string EmailOTP { get; set; }

        public string Phone { get; set; }
        public bool PhoneIsValid { get; set; }
        public string PhoneToChange { get; set; }
        public string LastValidPhone { get; set; }
        public string PhoneOTP { get; set; }

        public bool Initiated { get; set; }
        public string RevertOTP { get; set; }
        public DateTime ReversableUntil { get; set; }

        public override void ValidateModel()
        {
            RuleFor(i => i.Phone).Must(PhoneNumber.IsNullOrEmptyOrValid).WithError("invalid phone number");
            RuleFor(i => i.PhoneToChange).Must(PhoneNumber.IsNullOrEmptyOrValid).WithError("invalid phoneToChange number");
            RuleFor(i => i.LastValidPhone).Must(PhoneNumber.IsNullOrEmptyOrValid).WithError("invalid lastValidPhone number");

            RuleFor(i => i.Email).Must(EmailAddress.IsNullOrEmptyOrValid).WithError("invalid email address");
            RuleFor(i => i.EmailToChange).Must(EmailAddress.IsNullOrEmptyOrValid).WithError("invalid emailToChange address");
            RuleFor(i => i.LastValidEmail).Must(EmailAddress.IsNullOrEmptyOrValid).WithError("invalid lastValidEmail address");
        }

        public void MarkAsValid(string channelType)
        {
            if (channelType == ChannelType.Email.Code)
                EmailIsValid = true;
            else if (channelType == ChannelType.Phone.Code)
                PhoneIsValid = true;

            Initiated = true;
        }

        internal bool WillChangeFrom(EditProfileVM vm)
        {
            return !(Email == vm.Email?.ToLower()) ||
                   !(Phone == vm.Phone);
        }

        internal void MapFromEditVM(EditProfileVM vm)
        {
            Email = vm.Email?.ToLower();
            Phone = vm.Phone;
        }

        internal bool WillRemoveAnyFrom(EditProfileVM vm)
        {
            return (!string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(vm.Email)) ||
                   (!string.IsNullOrWhiteSpace(Phone) && string.IsNullOrWhiteSpace(vm.Phone));
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}