using Liquid.Activation;
using Liquid.Base;
using Liquid.Domain;
using Liquid.Platform;
using Microservice.Models;
using Microservice.Services;
using Microservice.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microservice.Controllers
{
    /// <summary>
    /// API with its endpoints and exchangeable datatypes
    /// </summary>
    [Authorize]
    [Route("/")]
    [Produces("application/json")]
    public class ProfilesController : LightController
    {
        /// <summary>
        /// Sync users from AAD
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPost("sync/directory")]
        //[ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<List<string>>), 200)]
        public async Task<IActionResult> SyncFromAADAsync()
        {
            var data = await Factory<ProfileService>().SyncFromAADUsersAsync();

            return Result(data);
        }

        /// <summary>
        /// Gets a user profile by id
        /// </summary>
        /// <param name="id">User id</param>
        /// <param name="onlyIM">Indication whether only IM (except AAD) users should be considered</param>
        /// <returns>The user's profile</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Response<ProfileBasicVM>), 200)]
        public async Task<IActionResult> GetByIdAsync(string id, bool onlyIM)
        {
            var data = await Factory<ProfileService>().GetByIdAsync(id, onlyIM);
            return Result(data);
        }

        /// <summary>
        /// Gets directory (AAD) users filtered by many parameters
        /// </summary>
        /// <param name="tip">Tip to match the start of user names or emails</param>
        /// <param name="emailFilter">List of email ending parts to filter users for (ex: @gmail.com)</param>
        /// <param name="guestOnly">Indication whether only guest users should be returned (optional, false if not informed)</param>
        /// <returns>A summary directory user list</returns>
        [HttpGet("directory/filter")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<List<DirectoryUserSummaryVM>>), 200)]
        public async Task<IActionResult> FilterAADUsersByEmailFilterAsync(string tip, List<string> emailFilter, bool guestOnly = false)
        {
            var data = await Factory<AADService>().FilterAADUsersByEmailFilterAsync(tip, emailFilter, guestOnly);
            return Result(data);
        }

        /// <summary>
        /// Gets a directory (AAD) user
        /// </summary>
        /// <param name="id">The user id</param>
        /// <returns>A summary directory user</returns>
        [HttpGet("directory/{id}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<DirectoryUserSummaryVM>), 200)]
        public async Task<IActionResult> GetAADUserByIdAsync(string id)
        {
            var data = await Factory<AADService>().GetAADUserByIdAsync(id);
            return Result(data);
        }

        /// <summary>
        /// Updates a directory (AAD) user's createdAt property 
        /// </summary>
        /// <param name="id">The user id</param>
        /// <param name="date">The date and time to set</param>
        /// <returns>A summary directory user</returns>
        [HttpPut("directory/{id}/createdAt")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response), 200)]
        public async Task<IActionResult> SetDirectoryUserCreatedAtAsync(string id, DateTime date)
        {
            var data = await Factory<AADService>().SetDirectoryUserCreatedAtAsync(id, date.ToUniversalTime());
            return Result(data);
        }

        /// <summary>
        /// Gets directory (AAD) users filtered by ids
        /// </summary>
        /// <param name="ids">The list of ids</param>
        /// <returns>A summary directory user list</returns>
        [HttpPost("directory/byIds")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<List<DirectoryUserSummaryVM>>), 200)]
        public async Task<IActionResult> GetAADUsersByIdsAsync([FromBody] List<string> ids)
        {
            var data = await Factory<AADService>().GetAADUsersByIdsAsync(ids);
            return Result(data);
        }

        /// <summary>
        /// Gets a directory (AAD) user from origin (AAD)
        /// </summary>
        /// <param name="id">The user id</param>
        /// <returns>A summary directory user</returns>
        [HttpGet("directory/{id}/origin")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<DirectoryUserSummaryVM>), 200)]
        public async Task<IActionResult> GetAADUserFromOriginByIdAsync(string id)
        {
            var data = await Factory<AADService>().GetAADUserFromOriginByIdAsync(id);
            return Result(data);
        }

        /// <summary>
        /// Gets directory (AAD) users from origin (AAD) filtered by ids
        /// </summary>
        /// <param name="ids">The list of ids</param>
        /// <returns>A summary directory user list</returns>
        [HttpPost("directory/byIds/origin")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<List<DirectoryUserSummaryVM>>), 200)]
        public async Task<IActionResult> GetAADUsersFromOriginByIdsAsync([FromBody] List<string> ids)
        {
            var data = await Factory<AADService>().GetAADUsersFromOriginByIdsAsync(ids);
            return Result(data);
        }

        /// <summary>
        /// Invite user as directory (AAD) guest users
        /// </summary>
        /// <param name="name">The name of the user</param>
        /// <param name="email">The user email address to invite the user and to be used as an alternate key</param>
        /// <param name="role">The initial role the user is going to have</param>
        /// <param name="redirectUrl">The url to redirect the user after redeem process</param>
        /// <returns>A user invitation data</returns>
        [HttpPost("directory/invite")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<DirectoryUserInvitationVM>), 200)]
        public async Task<IActionResult> InviteUserToAADAsync(string name, string email, string role, string redirectUrl)
        {
            var data = await Factory<AADService>().InviteUserAsync(name, email, role, redirectUrl);
            return Result(data);
        }

        /// <summary>
        /// Updates the directory user (AAD) roles
        /// </summary>
        /// <param name="id">User id</param>
        /// <param name="roles">The list of roles the user has</param>
        /// <returns>The user profile</returns>
        [HttpPut("directory/{id}/roles")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<ProfileVM>), 200)]
        public async Task<IActionResult> UpdateAADUserRolesAsync(string id, List<string> roles)
        {
            var data = await Factory<AADService>().UpdateUserRolesAsync(id, roles);
            return Result(data);
        }

        /// <summary>
        /// Inactivates a directory user (AAD)
        /// </summary>
        /// <param name="id">User id</param>
        /// <returns>The user profile</returns>
        [HttpPut("directory/{id}/inactivate")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<DirectoryUserSummaryVM>), 200)]
        public async Task<IActionResult> InactivateAADUserAsync(string id)
        {
            var data = await Factory<AADService>().InactivateUserAsync(id);
            return Result(data);
        }

        /// <summary>
        /// Removes the ban of a directory user (AAD)
        /// </summary>
        /// <param name="id">User id</param>
        /// <returns>The user profile</returns>
        [HttpPut("directory/{id}/unban")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<DirectoryUserSummaryVM>), 200)]
        public async Task<IActionResult> UnbanAADUserAsync(string id)
        {
            var data = await Factory<AADService>().UnbanUserAsync(id);
            return Result(data);
        }

        /// <summary>
        /// Gets a user profile with pending (not confirmed and/or approved) changes, by id
        /// </summary>
        /// <param name="id">User id</param>
        /// <returns>The user's profile with pending (not confirmed and/or approved) changes</returns>
        [HttpGet("{id}/pendingChanges")]
        [ProducesResponseType(typeof(Response<ProfileWithPendingChangesVM>), 200)]
        public async Task<IActionResult> GetPendingChangesByIdAsync(string id)
        {
            var data = await Factory<ProfileService>().GetPendingChangesByIdAsync(id);
            return Result(data);
        }

        /// <summary>
        /// Edits the user profile by Id
        /// </summary>
        /// <param name="id">User id</param>
        /// <param name="profile">An existing account record with its editable properties to be saved</param>
        /// <returns>The stored profile record after edition</returns>
        [Authorize(Roles = "generalAdmin, projectManager, fieldManager, fieldAnalyst, scheduler")]
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(Response<ProfileVM>), 200)]
        public async Task<IActionResult> UpdateByIdAsync(string id, [FromBody] EditProfileVM profile)
        {
            ValidateInput(profile);
            var data = await Factory<ProfileService>().UpdateByIdAsync(id, profile);
            return Result(data);
        }

        /// <summary>
        /// Reverts a channel update for a user profile by Id
        /// </summary>
        /// <param name="otpToken">Revertion OTP sent with the reversion link email</param>
        [AllowAnonymous]
#pragma warning disable ASP0018 // Unused route parameter
        [HttpPut("{id}/channel/revert")]
#pragma warning restore ASP0018 // Unused route parameter
        [ProducesResponseType(typeof(Response<ProfileVM>), 200)]
        public async Task<IActionResult> RevertChannelByIdAsync(string otpToken)
        {
            JsonDocument token = JsonDocument.Parse("{}");
            try
            {
                token = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(otpToken)));
            }
            catch
            {
                AddInputError("otpToken is invalid");
            }

            var data = await Factory<ProfileService>().RevertChannelByIdAsync(token);
            return Result(data);
        }

        /// <summary>
        /// Create or update a local user with new OTP
        /// </summary>
        /// <param name="newProfile">New profile to create or update a user for</param>
        /// <returns></returns>
        [HttpPost]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<ProfileWithOTPVM>), 200)]
        public async Task<IActionResult> CreateOrUpdateWithOTPAsync([FromBody] ProfileVM newProfile)
        {
            ValidateInput(newProfile);

            var data = await Factory<ProfileService>().CreateOrUpdateWithOTPAsync(newProfile);
            return Result(data);
        }

        /// <summary>
        /// Create service account user 
        /// </summary>
        /// <param name="userId">The id of the service account user to create</param>
        /// <param name="name">The name of the service account</param>
        /// <param name="email">The (admin) email of the service account</param>
        /// <returns>The service account user credentials</returns>
        [HttpPost("serviceAccount/{userId}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<ServiceAccountVM>), 200)]
        public async Task<IActionResult> CreateSAAsync(string userId, string name, string email)
        {
            email = email?.ToLower().Trim();
            if (!EmailAddress.IsValid(email))
                AddInputError("invalid email address");

            var data = await Factory<ProfileService>().CreateSAAsync(userId, name, email);
            return Result(data);
        }

        /// <summary>
        /// Updates an existing service account user secret
        /// </summary>
        /// <param name="userId">The id of the service account</param>
        /// <param name="name">The name of the service account</param>
        /// <param name="email">The (admin) email of the service account</param>
        /// <returns>The service account user new credentials</returns>
        [HttpPut("serviceAccount/{userId}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<ServiceAccountVM>), 200)]
        public async Task<IActionResult> UpdateSAAsync(string userId, string name, string email = null)
        {
            if (email is not null)
            {
                email = email.ToLower().Trim();
                if (!EmailAddress.IsValid(email))
                    AddInputError("invalid email address");
            }

            var data = await Factory<ProfileService>().UpdateSAAsync(userId, name, email);
            return Result(data);
        }

        /// <summary>
        /// Generates a new service account user secret
        /// </summary>
        /// <param name="userId">The id of the service account user to create</param>
        /// <returns>The service account user new credentials</returns>
        [HttpPut("serviceAccount/{userId}/newSecret")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<ServiceAccountVM>), 200)]
        public async Task<IActionResult> GenerateSASecretAsync(string userId)
        {
            var data = await Factory<ProfileService>().GenerateSASecretAsync(userId);
            return Result(data);
        }

        /// <summary>
        /// Deletes an existing service account
        /// </summary>
        /// <param name="userId">The id of the service account</param>
        /// <returns>The deleted service account</returns>
        [HttpDelete("serviceAccount/{userId}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<ServiceAccountVM>), 200)]
        public async Task<IActionResult> DeleteSAAsync(string userId)
        {
            var data = await Factory<ProfileService>().DeleteSAAsync(userId);
            return Result(data);
        }

        /// <summary>
        /// Gets basic user profiles by a list of ids
        /// </summary>
        /// <param name="ids">The list of ids</param>
        /// <returns>The user basic profiles</returns>
        [HttpGet("byIds")]
        [ProducesResponseType(typeof(Response<List<ProfileBasicVM>>), 200)]
        public async Task<IActionResult> GetByIdsAsync(List<string> ids)
        {
            var data = await Factory<ProfileService>().GetByIdsAsync(ids);
            return Result(data);
        }

        /// <summary>
        /// Gets user profiles by role
        /// </summary>
        /// <param name="role">The role</param>
        /// <param name="all">Indication whether all or only active profiles should be returned. Default false</param>
        /// <returns>The user profiles</returns>
        [HttpGet("byRole/{role}")]
        [ProducesResponseType(typeof(Response<List<ProfileVM>>), 200)]
        public IActionResult GetByRole(string role, bool all = false)
        {
            var data = Factory<ProfileService>().GetByRole(role, all);
            return Result(data);
        }

        /// <summary>
        /// Gets user profiles by any of the informed roles
        /// </summary>
        /// <param name="roles">List of roles</param>
        /// <param name="all">Indication whether all or only active profiles should be returned. Default false</param>
        /// <returns>The user profiles</returns>
        [HttpGet("byRoles")]
        [ProducesResponseType(typeof(Response<List<ProfileVM>>), 200)]
        public IActionResult GetByRoles(List<string> roles, bool all = false)
        {
            var data = Factory<ProfileService>().GetByRoles(roles, all);
            return Result(data);
        }

        /// <summary>
        /// Gets a user profile by email address
        /// </summary>
        /// <param name="email">The E-mail address</param>
        /// <returns>The user's profile</returns>
        [HttpGet("byEmail/{email}")]
        [ProducesResponseType(typeof(Response<ProfileVM>), 200)]
        public IActionResult GetByEmail(string email)
        {
            var data = Factory<ProfileService>().GetByEmail(email);
            return Result(data);
        }

        /// <summary>
        /// Gets the user basic profile by contact channels 
        /// </summary>
        /// <param name="channel">The channel id (phone number or email address)</param>
        /// <returns>The user's basic profile</returns>
        [AllowAnonymous]
        [HttpGet("byChannel/{channel}")]
        [ProducesResponseType(typeof(Response<ProfileBasicVM>), 200)]
        public IActionResult GetIdByChannel(string channel)
        {
            var data = Factory<ProfileService>().GetIdByChannel(channel);
            return Result(data);
        }

        /// <summary>
        /// Deletes the profile of the authenticated member user
        /// </summary>
        /// <param name="feedback">The feedback on why the member user is opting out</param>
        [Authorize(Roles = "member")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpDelete("me")]
        [ProducesResponseType(typeof(Response<ProfileVM>), 200)]
        public async Task<IActionResult> DeleteMeAsync(string feedback)
        {
            if (feedback is null || feedback.Length < 20)
                AddInputError("feedback must be at least 20 charecters long");

            var data = await Factory<ProfileService>().DeleteMeAsync(feedback);
            return Result(data);
        }

        /// <summary>
        /// Gets the profile of the authenticated user
        /// </summary>
        /// <returns>The user's profile</returns>
        [HttpGet("me")]
        [ProducesResponseType(typeof(Response<ProfileVM>), 200)]
        public async Task<IActionResult> GetMeAsync()
        {
            var data = await Factory<ProfileService>().GetOfCurrentAccountAsync();
            return Result(data);
        }

        /// <summary>
        /// Gets the profile of the authenticated user with pending (not confirmed and/or approved) changes
        /// </summary>
        /// <returns>The user's profile</returns>
        [HttpGet("me/pendingChanges")]
        [ProducesResponseType(typeof(Response<ProfileWithPendingChangesVM>), 200)]
        public async Task<IActionResult> GetMeWithPendingChangesAsync()
        {
            var data = await Factory<ProfileService>().GetPendingChangesOfCurrentAccountAsync();
            return Result(data);
        }

        /// <summary>
        /// Edits the profile of the authenticated user
        /// </summary>
        /// <param name="profile">An existing account record with its editable properties to be saved</param>
        /// <returns>The stored profile record after edition</returns>
        [HttpPut("me")]
        [ProducesResponseType(typeof(Response<ProfileVM>), 200)]
        public async Task<IActionResult> UpdateMeAsync([FromBody] EditProfileVM profile)
        {
            ValidateInput(profile);
            var data = await Factory<ProfileService>().UpdateOfCurrentAccountAsync(profile);
            return Result(data);
        }

        /// <summary>
        /// Requests the validation of channels of the authenticated user
        /// </summary>
        [HttpPut("me/channel/requestValidation")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> RequestMyChannelsValidationAsync()
        {
            var data = await Factory<ChannelService>().RequestMyChannelsValidationAsync();

            return Result(data);
        }

        /// <summary>
        /// Validates the a channel of the authenticated user
        /// </summary>
        /// <param name="channelType">Channel to validate (see channelTypes)</param>
        /// <param name="validationOTP">Channel validation OTP</param>
        [HttpPut("me/channel/{channelType}/validate")]
        [ProducesResponseType(typeof(Response<ProfileVM>), 200)]
        public async Task<IActionResult> ValidateMyChannelAsync(string channelType, string validationOTP)
        {
            if (!ChannelType.IsValid(channelType))
                AddInputError("channelType is invalid");

            var data = await Factory<ProfileService>().ValidateMyChannelAsync(channelType, validationOTP);
            return Result(data);
        }

        /// <summary>
        /// Resends the channel validation link of the authenticated user
        /// </summary>
        /// <param name="channelType">Channel to resend the validation link (see channelTypes)</param>
        [HttpPut("me/channel/{channelType}/validationResend")]
        [ProducesResponseType(typeof(Response), 200)]
        public async Task<IActionResult> ResendMyChannelValidationLinkAsync(string channelType)
        {
            if (!ChannelType.IsValid(channelType))
                AddInputError("channelType is invalid");

            var data = await Factory<ChannelService>().ResendMyValidationLinkAsync(channelType);
            return Result(data);
        }

        /// <summary>
        /// Gets the current account information of the authenticated user
        /// </summary>
        /// <returns>The account record</returns>
        [HttpGet("me/account")]
        [ProducesResponseType(typeof(Response<AccountVM>), 200)]
        public async Task<IActionResult> GetMyAccountAsync()
        {
            var data = await Factory<ProfileService>().GetCurrentAccountAsync();
            return Result(data);
        }

        /// <summary>
        /// Gets list of channels to send links and codes to the user
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("types/channels")]
        [ProducesResponseType(typeof(Response<List<ChannelType>>), 200)]
        public IActionResult GetChannelTypes()
        {
            var data = Factory<AuthenticationService>().GetChannelTypes();
            return Result(data);
        }
    }
}