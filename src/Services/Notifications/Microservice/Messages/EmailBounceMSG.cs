using Liquid.Activation;
using Liquid.Domain;
using System;
using System.Collections.Generic;

namespace Microservice.Messages
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class EmailBounceCMD(string code) : LightEnum<EmailBounceCMD>(code)
    {
        public static readonly EmailBounceCMD Process = new(nameof(Process));
    }

    public class EmailBounceMSG : LightMessage<EmailBounceMSG, EmailBounceCMD>
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<StatusByEmail> Addresses { get; set; }

        public override void ValidateModel() { }
    }

    public class StatusByEmail
    {
        public string Email { get; set; }
        public string Status { get; set; }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}