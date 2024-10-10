using Liquid.Base;
using Liquid.Domain;
using Liquid.Interfaces;
using Liquid.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCrontab;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Liquid.Activation
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// Abstract class based on IHostedService that run over OWIN and LighBackgroundTask 
    /// prepare and execute a back ground tasks signed's.
    /// </summary>
    public abstract class LightBackgroundTask : IHostedService, IDisposable
    {
        // https://github.com/pgroene/ASPNETCoreScheduler/tree/master/ASPNETCoreScheduler
        private readonly CrontabSchedule schedule;
        private DateTime NextRun;

        private readonly IServiceScopeFactory ServiceScopeFactory;
        private readonly InputValidator inputValidator = new();

        private Task ExecutingTask;
        private readonly CancellationTokenSource CancellationToken = new();

#pragma warning disable CA1822 // Mark members as static
        protected ICriticHandler CriticHandler => WorkBench.CriticHandler;
        protected ILightTelemetry Telemetry => WorkBench.Telemetry;
        public ILightContext SessionContext => WorkBench.SessionContext;

        /// <summary>
        /// Gets the id of the current user
        /// </summary>
        public  string CurrentUserId => SessionContext.CurrentUserId;

        /// <summary>
        /// Gets the first name of the current user
        /// </summary>
        public  string CurrentUserFirstName => SessionContext.CurrentUserFirstName;

        /// <summary>
        /// Gets the full name of the current user
        /// </summary>
        public  string CurrentUserFullName => SessionContext.CurrentUserFirstName;

        /// <summary>
        /// Gets the e-mail address of the current user
        /// </summary>
        public  string CurrentUserEmail => SessionContext.CurrentUserEmail;

        /// <summary>
        /// Checks if the current user is in the given security role
        /// </summary>
        /// <param name="role">Security role</param>
        /// <returns>True if the user is in the role</returns>
        public  bool CurrentUserIsInRole(string role) => SessionContext.CurrentUserIsInRole(role);

        /// <summary>
        /// Checks if the current user is in any of the given security roles
        /// </summary>
        /// <param name="roles">Security roles in a comma separated string</param>
        /// <returns>True if the user is in any role</returns>
        public bool CurrentUserIsInAnyRole(string roles) => SessionContext.CurrentUserIsInAnyRole(roles);

        /// <summary>
        /// Checks if the current user is in any of the given security roles
        /// </summary>
        /// <param name="roles">List of security roles</param>
        /// <returns>True if the user is in any role</returns>
        public  bool CurrentUserIsInAnyRole(params string[] roles) => SessionContext.CurrentUserIsInAnyRole(roles);
#pragma warning restore CA1822 // Mark members as static

        /// <summary>
        /// Crontab expression
        /// </summary>
        protected virtual string Schedule { get; } = "* * * * *"; // runs every minute by default

        public LightBackgroundTask(IServiceScopeFactory serviceScopeFactory)
        {
            schedule = CrontabSchedule.Parse(Schedule, new CrontabSchedule.ParseOptions { IncludingSeconds = Schedule.Split(' ').Length == 6 });
            NextRun = WorkBench.UtcNow;  // run on startup
            ServiceScopeFactory = serviceScopeFactory;
        }

        protected T Factory<T>() where T : LightDomain, new()
        {
            // Throws errors as a specific exception 
            if (inputValidator.ErrorsCount > 0)
                throw new InvalidInputLightException(inputValidator.Errors);

            return LightDomain.FactoryDomain<T>();
        }

        /// <summary>
        /// Start a background task async
        /// </summary>
        /// <param name="cancellationToken">Cancellation Token</param>
        /// <returns></returns>
        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            WorkBench.SetSession(new LightContext() { OperationId = WorkBench.GenerateNewOperationId(), User = JwtSecurityCustom.DecodeToken(JwtSecurityCustom.Config.SysAdminJWT) }, new CriticHandler());

            ExecutingTask = ExecuteAsync(CancellationToken.Token);

            if (ExecutingTask.IsCompleted)
                return ExecutingTask;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop a background task async
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            if (ExecutingTask is null)
                return;

            try
            {
                CancellationToken.Cancel();
            }
            finally
            {
                await Task.WhenAny(ExecutingTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }
        }

        /// <summary>
        /// ExecuteAsync a background Task async
        /// </summary>
        /// <param name="cancellationToken">Cancellation Token</param>
        /// <returns></returns>
        protected virtual async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            do
            {
                var now = WorkBench.UtcNow;
                if (now > NextRun)
                {
                    await Process();
                    NextRun = schedule.GetNextOccurrence(now);
                }
                await Task.Delay(5000, cancellationToken);
            }
            while (!cancellationToken.IsCancellationRequested);
        }

        /// <summary>
        /// Process a brackground task async.
        /// </summary>
        /// <returns></returns>
        protected async Task Process()
        {
            using var scope = ServiceScopeFactory.CreateScope();
            await ProcessInScope(scope.ServiceProvider);
        }

        public abstract Task ProcessInScope(IServiceProvider serviceProvider);

        public void Dispose()
        {
            CancellationToken.Dispose();
            GC.SuppressFinalize(this);
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}