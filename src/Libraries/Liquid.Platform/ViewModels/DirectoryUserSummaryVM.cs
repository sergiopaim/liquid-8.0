using Liquid.Domain;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Liquid.Platform
{
    /// <summary>
    /// A directory user's summary profile 
    /// </summary>
    public class DirectoryUserSummaryVM : LightViewModel<DirectoryUserSummaryVM>
    {
        /// <summary>
        /// User id
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// User´s name 
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The user's email address
        /// </summary>
        public string Email { get; set; }
        /// <summary>
        /// Motive the user has been banned
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string BanMotive { get; set; }
        /// <summary>
        /// List of all user's email addresses
        /// </summary>
        public List<string> OtherMails { get; set; } = [];
        /// <summary>
        /// The status of the invite to the guest user (null if not a guest user)
        /// </summary>
        public InviteStatus InviteStatus { get; set; }
        /// <summary>
        /// The date and time the user was created
        /// </summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// The date and time the user was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }
        
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override void ValidateModel() { }

    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}