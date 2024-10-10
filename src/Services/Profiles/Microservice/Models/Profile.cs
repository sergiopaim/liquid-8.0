using FluentValidation;
using Liquid;
using Liquid.Platform;
using Liquid.Repository;
using Liquid.Runtime;
using Microservice.Messages;
using Microservice.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microservice.Models
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    //[AnalyticalSource]
    public class Profile : LightOptimisticModel<Profile>
    {
        private const string AAD_CLIENT_ROLE = "client";
        private const int AAD_TOKEN_VALIDITY_IN_HOURS = 4;
        private const int IM_TOKEN_VALIDITY_IN_HOURS = 24;
        private string name;

        public string Name { get => name; set => name = value?.Trim()?.Replace("  ", " "); }
        public string Language { get; set; }
        public string TimeZone { get; set; }
        public Channels Channels { get; set; } = new();
        public JsonDocument UIPreferences { get; set; } = JsonDocument.Parse("{}");
        public List<Account> Accounts { get; set; } = [];
        public string Status { get; set; } = ProfileStatus.Active.Code;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

        public bool? Banned { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string BanMotive { get; set; }

        public DateTime CreatedAt { get; set; } = WorkBench.UtcNow;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? LastSignedinAt { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? InactivatedAt { get; set; }

        internal bool IsFromAAD => Accounts?.Any(a => a.Source == AccountSource.AAD.Code) == true;
        internal bool IsAADInternal => IsFromAAD && Accounts.First().Roles.Count != 0 && !Accounts.First().Roles.Contains(AAD_CLIENT_ROLE);
        internal bool IsAADGuest => IsFromAAD && !IsAADInternal;

        public override void ValidateModel()
        {
            RuleFor(i => i.Name).NotEmpty().WithError("name must not be empty");
            RuleFor(i => i.Channels).NotEmpty().WithError("channels must not be empty");
            RuleFor(i => i.Status).Must(ProfileStatus.IsValid).WithError("status is invalid");

            if (Banned == true)
                RuleFor(i => i.Status).Equal(ProfileStatus.Inactive.Code).WithError("banned profiles should always be inactive");
        }

        internal static Profile FactoryFromAADClaims(ClaimsPrincipal userClaims)
        {
            Profile factored = new()
            {
                Id = userClaims.FindFirstValue(JwtClaimTypes.UserId),
                Name = userClaims.FindFirstValue("given_name") + " " + userClaims.FindFirstValue("family_name"),

                Language = "pt",
                TimeZone = "America/Sao_Paulo"
            };

            if (string.IsNullOrWhiteSpace(factored.name))
                factored.name = userClaims.FindFirstValue("name");

            factored.Channels.Email = userClaims.FindFirstValue("email")?.ToLower();
            factored.Channels.EmailIsValid = true;

            if (string.IsNullOrWhiteSpace(factored.Channels.Email))
                factored.Channels.Email = userClaims.FindFirstValue("preferred_username")?.ToLower();

            factored.AddAccount(userClaims);

            return factored;
        }

        internal static Profile FactoryFromAADUser(DirectoryUserSummaryVM user, List<string> roles, bool fromInvitation)
        {
            if (user is null)
                return null;

            Profile factored = new()
            {
                Id = user.Id,
                Name = user.Name,

                Language = "pt",
                TimeZone = "America/Sao_Paulo"
            };

            if (fromInvitation)
                factored.Status = ProfileStatus.Invited.Code;

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                factored.Channels.Email = user.Email?.ToLower();
                factored.Channels.EmailIsValid = !fromInvitation;
            }

            factored.AddAccount(user, roles);

            return factored;
        }

        internal static Profile FactoryFrom(ProfileVM vm)
        {
            var m = FactoryFrom((Liquid.Interfaces.ILightViewModel)vm);

            m.Channels.Email = vm.Email?.ToLower();
            m.Channels.EmailIsValid = vm.EmailIsValid;
            m.Channels.Phone = vm.Phone;
            m.Channels.PhoneIsValid = vm.PhoneIsValid;

            return m;
        }

        internal void MapFromEditVM(EditProfileVM vm)
        {
            MapFrom(vm);
            Channels.MapFromEditVM(vm);
        }

        internal ProfileVM FactoryVM()
        {
            var vm = ProfileVM.FactoryFrom(this);

            vm.Email = Channels.Email?.ToLower();
            vm.EmailIsValid = Channels.EmailIsValid && string.IsNullOrWhiteSpace(Channels.EmailToChange);
            vm.Phone = Channels.Phone;
            vm.PhoneIsValid = Channels.PhoneIsValid && string.IsNullOrWhiteSpace(Channels.PhoneToChange);

            vm.UIPreferences ??= JsonDocument.Parse("{}");

            vm.Roles = Accounts.SelectMany(a => a.Roles).Distinct().ToList();

            return vm;
        }

        internal ProfileWithPendingChangesVM FactoryWithPendingChangesVM()
        {
            var vm = ProfileWithPendingChangesVM.FactoryFrom(this);

            if (!string.IsNullOrWhiteSpace(Channels.EmailToChange))
            {
                vm.Email = Channels.EmailToChange?.ToLower();
                vm.EmailIsValid = false;
            }
            else
            {
                vm.Email = Channels.Email?.ToLower();
                vm.EmailIsValid = Channels.EmailIsValid;
            }

            if (!string.IsNullOrWhiteSpace(Channels.PhoneToChange))
            {
                vm.Phone = Channels.PhoneToChange;
                vm.PhoneIsValid = false;
            }
            else
            {
                vm.Phone = Channels.Phone;
                vm.PhoneIsValid = Channels.PhoneIsValid;
            }

            return vm;
        }

        internal ProfileWithOTPVM FactoryWithOTPVM()
        {
            var vm = ProfileWithOTPVM.FactoryFrom(this);

            vm.Email = Channels.Email?.ToLower();
            vm.Phone = Channels.Phone;

            vm.OTP = Accounts?.FirstOrDefault()?.Credentials?.OTP;

            return vm;
        }

        internal ProfileBasicVM FactoryBasicVM()
        {
            var vm = ProfileBasicVM.FactoryFrom(this);

            vm.Email = Channels.Email?.ToLower();
            vm.Phone = Channels.Phone;

            vm.Roles = Accounts.SelectMany(a => a.Roles).Distinct().ToList();

            return vm;
        }

        internal void MapToMSG(ProfileMSG msg)
        {
            msg.MapFrom(this);
            msg.Email = Channels.Email?.ToLower();
            msg.EmailIsValid = Channels.EmailIsValid;
            msg.Phone = Channels.Phone;
            msg.PhoneIsValid = Channels.PhoneIsValid;
            msg.Roles = Accounts.SelectMany(a => a.Roles).Distinct().ToList();
            msg.IsFromAAD = IsFromAAD;
        }

        internal DateTime GetTokenExpiration()
        {
            if (Accounts.FirstOrDefault().Source == AccountSource.AAD.Code)
                return WorkBench.UtcNow.AddHours(AAD_TOKEN_VALIDITY_IN_HOURS);

            return WorkBench.UtcNow.AddHours(IM_TOKEN_VALIDITY_IN_HOURS);
        }

        internal void UpdateFromAADSignIn(ClaimsPrincipal userClaims)
        {
            if (Status == ProfileStatus.Invited.Code)
                ActivateFromEmail();

            //Updates roles and other profile data before check to reactivate
            Accounts[0] = Account.FactoryFromAADClaims(userClaims);

            if (Status == ProfileStatus.Inactive.Code && IsAADInternal)
                ActivateFromEmail();
        }

        internal bool InvalidateEmail(StatusByEmail address)
        {
            if (Channels.Email == address.Email && (Channels.EmailIsValid || Status == ProfileStatus.Invited.Code))
            {
                Channels.EmailIsValid = false;

                //AAD users invited with invalid email addresses should be permanently inactivated
                if (IsFromAAD)
                {
                    Status = ProfileStatus.Inactive.Code;
                    Banned = true;
                    BanMotive = $"EMAIL BOUNCE:\n\n{address.Status}";
                    InactivatedAt = WorkBench.UtcNow;
                }

                return true;
            }

            return false;
        }

        internal void Unban()
        {
            Banned = null;
            BanMotive = null;
        }

        internal void ActivateFromEmail()
        {
            Channels.EmailIsValid = true;

            if (IsFromAAD)
            {
                Status = ProfileStatus.Active.Code;
                Banned = null;
                BanMotive = null;
                InactivatedAt = null;
            }
        }

        internal void AddAccount(string sourceCode, string roleCode)
        {
            Accounts.Add(Account.FactoryFromRole(Id, sourceCode, roleCode));
        }

        private void AddAccount(DirectoryUserSummaryVM user, List<string> roles)
        {
            Accounts.Add(Account.FactoryFromAADUser(user, roles));
        }

        private void AddAccount(ClaimsPrincipal userClaims)
        {
            Accounts.Add(Account.FactoryFromAADClaims(userClaims));
        }

        internal void RegisterSignin()
        {
            LastSignedinAt = WorkBench.Now;
        }

        internal void GenerateNewOTP()
        {
            Accounts.First().Credentials.GenerateNewOTP();
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}