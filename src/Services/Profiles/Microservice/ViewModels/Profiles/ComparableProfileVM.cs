using FluentValidation;
using Liquid.Domain;
using Liquid.Platform;
using Liquid.Runtime;
using Microservice.Models;
using System.Collections.Generic;
using System.Linq;

namespace Microservice.ViewModels
{
    /// <summary>
    /// A user's profile used for compare update changes
    /// </summary>
    public class ComparableProfileVM : LightViewModel<ComparableProfileVM>
    {
        /// <summary>
        /// User's id
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// User´s name 
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Language selected by the user
        /// </summary>
        public string Language { get; set; }
        /// <summary>
        /// Timezone selected by the user
        /// </summary>
        public string TimeZone { get; set; }
        /// <summary>
        /// User's roles from all accounts
        /// </summary>
        public List<string> Roles { get; set; } = [];
        /// <summary>
        /// The status of the profile
        /// </summary>
        public string Status { get; set; }
        /// <summary>
        /// The communication channels of the profile
        /// </summary>
        public Channels Channels { get; set; } = new();

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override void ValidateModel()
        {
            RuleFor(i => false).Equal(true).WithError("This ViewModel type can only be used as response");
        }

        internal static ComparableProfileVM FactoryFrom(Profile profile)
        {
            ComparableProfileVM comparable = new()
            {
                Id = profile.Id,
                Name = profile.Name,
                Language = profile.Language,
                TimeZone = profile.TimeZone,
                Roles = profile.Accounts.SelectMany(a => a.Roles).Distinct().ToList(),
                Status = profile.Status
            };

            comparable.Channels.Email = profile.Channels.Email?.ToLower();
            comparable.Channels.EmailIsValid = profile.Channels.EmailIsValid;
            comparable.Channels.EmailToChange = profile.Channels.EmailToChange?.ToLower();
            comparable.Channels.LastValidEmail = profile.Channels.LastValidEmail?.ToLower();
            comparable.Channels.EmailOTP = profile.Channels.EmailOTP;

            comparable.Channels.Phone = profile.Channels.Phone;
            comparable.Channels.PhoneIsValid = profile.Channels.PhoneIsValid;
            comparable.Channels.PhoneToChange = profile.Channels.PhoneToChange;
            comparable.Channels.LastValidPhone = profile.Channels.LastValidPhone;
            comparable.Channels.PhoneOTP = profile.Channels.PhoneOTP;

            comparable.Channels.Initiated = profile.Channels.Initiated;
            comparable.Channels.RevertOTP = profile.Channels.RevertOTP;

            return comparable;
        }

        internal void MapToMSG(ProfileMSG msg)
        {
            msg.MapFrom(this);
            msg.Email = Channels.Email?.ToLower();
            msg.EmailIsValid = Channels.EmailIsValid;
            msg.Phone = Channels.Phone;
            msg.PhoneIsValid = Channels.PhoneIsValid;
            msg.Roles = Roles.Distinct().ToList();
        }

        internal ProfileMSG MapChangingRoles(ProfileMSG updated)
        {
            updated.RemovedRoles = Roles.Where(r => !updated.Roles.Contains(r)).ToList();
            updated.AddedRoles = updated.Roles.Where(r => !Roles.Contains(r)).ToList();

            return updated;
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}