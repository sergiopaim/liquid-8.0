using FluentValidation;
using Liquid;
using Liquid.Base;
using Liquid.Domain;
using Liquid.Runtime;
using Microservice.Configuration;
using Microservice.Models;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

namespace Microservice.ViewModels
{
    /// <summary>
    /// The view model with the new JWT created
    /// </summary>
    public class TokenVM : LightViewModel<TokenVM>
    {
        /// <summary>
        /// The account id to which the JWT was issued
        /// </summary>
        public string IssuedTo { get; set; }
        /// <summary>
        /// The JWT
        /// </summary>
        public string Token { get; set; }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override void ValidateModel()
        {
            RuleFor(i => false).Equal(true).WithError("This ViewModel type can only be used as response");
        }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        private static readonly AuthenticationConfig config = LightConfigurator.LoadConfig<AuthenticationConfig>("Authentication");
        private static readonly X509Certificate2 privateKey = new(config.JWTP12CertificateLocation, config.JWTCertificatePassword);

        private static readonly TokenValidationParameters validationParameters = new()
        {
            ValidateIssuerSigningKey = true,
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateLifetime = false,
            IssuerSigningKey = new X509SecurityKey(privateKey),
            ValidAudience = config.JWTSelfIssuedAudience,
            ValidIssuer = config.JWTSelfIssuer
        };

        private static readonly JwtSecurityTokenHandler tokenHandler = new();

        internal static TokenVM FactoryFor(Profile profile)
        {
            return new TokenVM
            {
                IssuedTo = profile.Id,
                Token = GenerateNewJWT(profile.Id, profile)
            };
        }

        internal static bool ValidatePrescribedJWT(string jwt, out SecurityToken validatedToken)
        {
            try
            {
                tokenHandler.ValidateToken(jwt, validationParameters, out validatedToken);
                return true;
            }
            catch (Exception e1)
            {
                if (e1.Message.Contains("The associated certificate has expired"))
                {
                    TokenValidationParameters relaxedValidationParameters = new()
                    {
                        ValidateIssuerSigningKey = false,
                        ValidateAudience = true,
                        ValidateIssuer = true,
                        ValidateLifetime = false,
                        IssuerSigningKey = new X509SecurityKey(privateKey),
                        ValidAudience = config.JWTSelfIssuedAudience,
                        ValidIssuer = config.JWTSelfIssuer
                    };

                    try
                    {
                        Telemetry.TrackException(new LightException("JWT Authenticator Certificate has expired", e1));

                        tokenHandler.ValidateToken(jwt, relaxedValidationParameters, out validatedToken);
                        return true;
                    }
                    catch { }
                }

                X509Certificate2 oldPrivateKey = new(config.JWTP12CertificateLocationOld, config.JWTCertificatePasswordOld);
                TokenValidationParameters oldValidationParameters = new()
                {
                    ValidateIssuerSigningKey = true,
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateLifetime = false,
                    IssuerSigningKey = new X509SecurityKey(oldPrivateKey),
                    ValidAudience = config.JWTSelfIssuedAudience,
                    ValidIssuer = config.JWTSelfIssuer
                };

                try
                {
                    tokenHandler.ValidateToken(jwt, oldValidationParameters, out validatedToken);
                    return true;
                }
                catch (Exception e2)
                {
                    if (e2.Message.Contains("The associated certificate has expired"))
                    {
                        oldValidationParameters.ValidateIssuerSigningKey = false;

                        try
                        {
                            tokenHandler.ValidateToken(jwt, oldValidationParameters, out validatedToken);
                            return true;
                        }
                        catch { }
                    }

                    Telemetry.TrackTrace($"Invalid token: \n Current config: {e1.Message}, \n Former config: {e2.Message}");
                }
            }

            validatedToken = default;
            return false;
        }

        private static string GenerateNewJWT(string accountId, Profile profile)
        {
            var givenName = profile.Name.Split()[0];
            var surname = string.Join(' ', profile.Name.Split().Skip(1));

            var credentials = new SigningCredentials(new X509SecurityKey(privateKey), SecurityAlgorithms.RsaSha256);

            List<Claim> userClaims =
            [
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("sub", accountId),
                new Claim("GivenName", givenName),
                new Claim("Surname", surname),
                new Claim("Email", profile.Channels.Email ?? ""),
                new Claim("CellPhone", profile.Channels.Phone ?? "")
            ];

            foreach (var role in profile.Accounts.First().Roles)
            {
                userClaims.Add(new Claim(ClaimsIdentity.DefaultRoleClaimType, role));
            }

            var token = new JwtSecurityToken(issuer: config.JWTSelfIssuer,
                                             audience: config.JWTSelfIssuedAudience,
                                             claims: userClaims,
                                             notBefore: WorkBench.UtcNow,
                                             expires: profile.GetTokenExpiration(),
                                             signingCredentials: credentials);

            //This method only works in Linux, and it required significant effort to figure it out 🙄
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}