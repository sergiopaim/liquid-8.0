using Liquid.Base;
using Liquid.Base.Test;
using Liquid.Interfaces;
using Liquid.Runtime;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Liquid.Domain
{
    /// <summary>
    /// Class created to apply a message inheritance to use a liquid framework
    /// </summary> 
    public abstract class LightJobMessage<TMessage, TCommand> : LightViewModel<TMessage>, ILightJobMessage, ILightMessage
        where TMessage : LightJobMessage<TMessage, TCommand>, ILightJobMessage, new()
        where TCommand : ILightEnum
    {
        private TCommand _commandType;
        private string _operationId;
        private ILightContext _context;

        [JsonIgnore]
        public ILightContext TransactionContext
        {
            get
            {
                if (_context is null)
                {
                    _context = new LightContext();
                    CheckContext(TokenJwt);
                }
                return _context;
            }
            set
            {
                _context = value;
            }
        }
        public string TokenJwt
        {
            get
            {
                return JwtSecurityCustom.GetJwtToken((ClaimsIdentity)TransactionContext?.User?.Identity);
            }
            set
            {
                CheckContext(value);
            }
        }

        public string Microservice { get; set; }
        public string Job { get; set; }

        public string CommandType
        {
            get => _commandType?.Code;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    _commandType = default;
                else
                {
                    var command = Activator.CreateInstance(typeof(TCommand), value);
                    _commandType = (TCommand)command;
                }
            }
        }

        public string OperationId
        {
            get => _operationId ??= WorkBench.GenerateNewOperationId();
            set
            {
                _operationId = value;
            }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? ClockDisplacement
        {
            get => AdjustableClock.Displacement;
            set => AdjustableClock.Displacement = value;
        }

        public virtual Dictionary<string, object> GetUserProperties()
        {
            return [];
        }

        /// <summary>
        /// Verify if context was received otherwise create the context with mock
        /// </summary>
        /// <param name="token">Token</param> 
        private void CheckContext(string token)
        {
            _context ??= new LightContext
            {
                OperationId = WorkBench.GenerateNewOperationId()
            };

            if (!string.IsNullOrWhiteSpace(token))
            {
                _context.User ??= JwtSecurityCustom.DecodeToken(token);
            }
        }
    }
}