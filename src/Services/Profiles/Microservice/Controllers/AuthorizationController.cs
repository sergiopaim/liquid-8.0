using Liquid.Activation;
using Liquid.Base;
using Liquid.Platform;
using Microservice.Models;
using Microservice.Services;
using Microservice.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Microservice.Controllers
{
    /// <summary>
    /// API with its endpoints and exchangeable datatypes
    /// </summary>
    [Authorize]
    [Route("/")]
    [Produces("application/json")]
    public class AuthorizationController : LightController
    {
        /// <summary>
        /// Gets a new JWT based on a previously valid one
        /// </summary>
        /// <returns>A newly created valid JWT</returns>
        [AllowAnonymous]
        [HttpPut("auth/token/refresh")]
        [ProducesResponseType(typeof(Response<TokenVM>), 200)]
        public async Task<IActionResult> RefreshJWTAsync(string oldToken)
        {
            if (string.IsNullOrWhiteSpace(oldToken))
                AddInputError("oldToken must not be empty");

            var data = await Factory<AuthenticationService>().RefreshJWTAsync(oldToken);
            return Result(data);
        }

        /// <summary>
        /// Authenticates with provided IdToken
        /// </summary>
        /// <param name="idToken">The id_token to authenticate with</param>
        /// <returns>A newly created valid JWT</returns>
        [AllowAnonymous]
        [HttpPost("auth/token/byIdToken")]
        [ProducesResponseType(typeof(Response<TokenVM>), 200)]
        public async Task<IActionResult> AuthenticateByIdTokenAsync(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken))
                AddInputError("idToken is empty or null");
            var data = await Factory<AuthenticationService>().AuthenticateByIdTokenAsync(idToken);
            return Result(data);
        }

        /// <summary>
        /// Authenticates with provided OTP
        /// </summary>
        /// <param name="accountId">The id for the account being authenticated</param>
        /// <param name="otp">The OTP to be validated</param>
        /// <param name="channelType">Channel to send the invitation link (see channelTypes)</param>
        /// <returns>A newly created valid JWT</returns>
        [AllowAnonymous]
        [HttpPost("auth/token/byOTP")]
        [ProducesResponseType(typeof(Response<TokenVM>), 200)]
        public async Task<IActionResult> AuthenticateByOTPAsync(string accountId, string otp, string channelType)
        {
            if (string.IsNullOrWhiteSpace(accountId))
                AddInputError("accountId must not be empty");
            if (string.IsNullOrWhiteSpace(otp))
                AddInputError("otp must not be empty");
            if (!ChannelType.IsValid(channelType))
                AddInputError("channelType is invalid");

            var data = await Factory<AuthenticationService>().AuthenticateByOTPAsync(accountId, otp, channelType);
            return Result(data);
        }

        /// <summary>
        /// Authenticates a ServiceAccount (for API integration) with provided secret
        /// </summary>
        /// <param name="serviceAccountId">The id for the ServiceAccount being authenticated</param>
        /// <param name="secret">The secret string associated to the ServiceAccount</param>
        /// <returns>A newly created valid JWT</returns>
        [AllowAnonymous]
        [HttpPost("auth/token/bySecret")]
        [ProducesResponseType(typeof(Response<TokenVM>), 200)]
        public async Task<IActionResult> AuthenticateBySecretAsync(string serviceAccountId, string secret)
        {
            if (string.IsNullOrWhiteSpace(serviceAccountId))
                AddInputError("accountId must not be empty");
            if (string.IsNullOrWhiteSpace(secret))
                AddInputError("otp must not be empty");

            var data = await Factory<AuthenticationService>().AuthenticateBySecretAsync(serviceAccountId, secret);
            return Result(data);
        }

        /// <summary>
        /// Generates a new OTP for the provided account
        /// </summary>
        /// <param name="accountId"></param>
        /// <returns>A newly created OTP associated to the account</returns>
        [AllowAnonymous]
        [HttpGet("auth/otp")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(Response<ProfileWithOTPVM>), 200)]
        public async Task<IActionResult> RequestNewOTPAsync(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
                AddInputError("accountId must not be empty");
            var data = await Factory<AuthenticationService>().RequestNewOTPAsync(accountId);
            return Result(data);
        }

        /// <summary>
        /// Request login via other signed in application sessions of the user
        /// </summary>
        /// <param name="accountId">User account id</param>
        /// <param name="connectionId">ReactiveHub anonymous connection id (of the requesting session)</param>
        /// <returns>Void</returns>
        [AllowAnonymous]
        [HttpPut("auth/request")]
        public IActionResult RequestLogin(string accountId, string connectionId)
        {
            var data = Factory<AuthenticationService>().RequestLogin(accountId, connectionId);
            return Result(data);
        }

        /// <summary>
        /// Request login via other signed in application sessions of the user
        /// </summary>
        /// <param name="accountId">User account id </param>
        /// <param name="connectionId">ReactiveHub anonymous connection id (of the requesting session)</param>
        /// <returns>Void</returns>
        [AllowAnonymous]
        [HttpPut("auth/allow")]
        public async Task<IActionResult> AllowLoginAsync(string accountId, string connectionId)
        {
            var data = await Factory<AuthenticationService>().AllowLoginAsync(accountId, connectionId);
            return Result(data);
        }

        /// <summary>
        /// Send authentication link with OTP through the channel selected
        /// </summary>
        /// <param name="accountId">User account id </param>
        /// <param name="channelType">Channel to send the invitation link (see channelTypes)</param>
        /// <returns>Void</returns>
        [AllowAnonymous]
        [HttpPut("auth/sendLink")]
        public async Task<IActionResult> SendAuthLinkAsync(string accountId, string channelType)
        {
            if (string.IsNullOrWhiteSpace(channelType))
                channelType = ChannelType.Email.Code;

            if (!ChannelType.IsValid(channelType))
                AddInputError("channelType is invalid");

            var data = await Factory<AuthenticationService>().SendAuthLinkAsync(accountId, channelType);
            return Result(data);
        }

        #region WebAuthN (commented)

        ///// <summary>
        ///// Authorizes WebAuthN device
        ///// </summary>
        ///// <returns>A newly created JWT</returns>
        //[HttpPost("auth/token/byWebAuthN")]
        //[AllowAnonymous]
        //public async Task<IActionResult> AuthenticateByWebAuthNAsync([FromBody]WebAuthNRequestVM viewModel)
        //{
        //    ValidateInput(viewModel);
        //    var data = await Factory<AuthenticationService>().AuthenticateByWebAuthNAsync(viewModel);
        //    return Result(data);
        //}

        ///// <summary>
        ///// Generates a new challenge for the provided account for registering a new device
        ///// </summary>
        ///// <returns>A newly created challenge</returns>
        //[HttpGet("auth/webAuthN")]
        //public async Task<IActionResult> StartRegisteringWebAuthNAsync()
        //{
        //    var data = await Factory<AuthenticationService>().StartRegisteringWebAuthNAsync();
        //    return Result(data);
        //}

        ///// <summary>
        ///// Registers a new device with WebAuthN
        ///// </summary>
        ///// <param name="deviceId"></param>
        ///// <param name="viewModel"></param>
        ///// <returns>A copy of the view model sent</returns>
        //[HttpPost("auth/webAuthN/devices/{deviceId}")]
        //public async Task<IActionResult> RegisterWebAuthNDeviceAsync(string deviceId, [FromBody]WebAuthNRequestVM viewModel)
        //{
        //    ValidateInput(viewModel);

        //    var data = await Factory<WebAuthNService>().RegisterWebAuthNDeviceAsync(deviceId, viewModel);
        //    return Result(data);
        //}

        /// <summary>
        /// Starts WebAuthN authentication process
        /// </summary>
        /// <param name="deviceId">Device unique identifyer</param>
        /// <returns></returns>
        //[HttpGet("auth/webAuthN/devices/{deviceId}")]
        //[AllowAnonymous]
        //public async Task<IActionResult> StartAuthenticationWebAuthNAsync(string deviceId)
        //{
        //    var data = await Factory<WebAuthNService>().StartAuthenticationWebAuthNAsync(deviceId);

        //    return Result(data);
        //}

        #endregion
    }
}