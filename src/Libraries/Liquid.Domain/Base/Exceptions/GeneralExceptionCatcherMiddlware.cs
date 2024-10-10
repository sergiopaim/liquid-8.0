﻿using Liquid.Base;
using Liquid.Domain;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Liquid.Middleware
{
    /// <summary>
    /// Exception for catcher middleware
    /// </summary>
    /// <remarks>
    /// Building a LightException with summary data
    /// </remarks>
    /// <param name="next">The next request Service</param>
    public class GeneralExceptionCatcherMiddlware(RequestDelegate next)
    {
        /// <summary>
        /// Invokes the logic of the middleware.
        /// (Intercepet the swagger call)
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns>A Task that completes when the middleware has completed processing.</returns>
        public async Task Invoke(HttpContext httpContext)
        {
            bool handledException = false;
            try
            {
                try
                {
                    // Calls the next delegate/middleware in the pipeline
                    await next(httpContext);
                }
                catch (EnumInvalidCodeLightException ex)
                {
                    if (httpContext?.Response is not null)
                    {
                        httpContext.Response.StatusCode = 400; //Bad RequestAsync  
                        httpContext.Response.ContentType = "application/json";

                        List<Critic> critics =
                        [
                            new()
                            {
                                Code = ex.Message,
                                Message = ex.Message,
                                Type = Interfaces.CriticType.Error
                            }
                        ];

                        string jsonString = (new { critics }).ToJsonString();
                        await httpContext.Response.WriteAsync(jsonString);
                        handledException = true;
                    }
                    else
                        throw;
                }
                catch (InvalidInputLightException ex)
                {
                    if (httpContext?.Response is not null)
                    {
                        httpContext.Response.StatusCode = 400; //Bad RequestAsync  
                        httpContext.Response.ContentType = "application/json";
                        string jsonString = (new { critics = ex.InputErrors }).ToJsonString();
                        await httpContext.Response.WriteAsync(jsonString);
                        handledException = true;
                    }
                    else
                        throw;
                }
                catch (OptimisticConcurrencyLightException)
                {
                    if (httpContext?.Response is not null)
                    {
                        httpContext.Response.StatusCode = 409; //Conflict
                        httpContext.Response.ContentType = "application/json";

                        List<Critic> critics =
                        [
                            new()
                            {
                                Code = "OPTIMISTIC_CONCURRENCY_CONFLICT",
                                Message = Domain.Properties.Localization.OPTIMISTIC_CONCURRENCY_CONFLICT,
                                Type = Interfaces.CriticType.Error
                            }
                        ];

                        string jsonString = (new { critics }).ToJsonString();
                        await httpContext.Response.WriteAsync(jsonString);
                        handledException = true;
                    }
                    else
                        throw;
                }
                catch (DuplicatedInsertionLightException)
                {
                    if (httpContext?.Response is not null)
                    {
                        httpContext.Response.StatusCode = 409; //Conflict
                        httpContext.Response.ContentType = "application/json";

                        List<Critic> critics =
                        [
                            new()
                            {
                                Code = "DUPLICATED_INSERTION_CONFLICT",
                                Message = Domain.Properties.Localization.DUPLICATED_INSERTION_CONFLICT,
                                Type = Interfaces.CriticType.Error
                            }
                        ];
                       
                        string jsonString = (new { critics }).ToJsonString();
                        await httpContext.Response.WriteAsync(jsonString);
                        handledException = true;
                    }
                    else
                        throw;
                }
                catch (TimeoutException)
                {
                    if (httpContext?.Response is not null)
                    {
                        httpContext.Response.StatusCode = 429; //Too Many Requests
                        httpContext.Response.ContentType = "application/json";

                        List<Critic> critics =
                        [
                            new()                           
                            {
                               Code = "DEPENDENCY_TIME_OUT",
                               Message = Domain.Properties.Localization.DEPENDENCY_TIME_OUT,
                               Type = Interfaces.CriticType.Error                          
                            } 
                        ];

                        string jsonString = (new { critics }).ToJsonString();
                        await httpContext.Response.WriteAsync(jsonString);
                        handledException = true;
                    }
                    else
                        throw;
                }
                catch (LightException e)
                {
                    e.FilterRelevantStackTrace();

                    WorkBench.BaseTelemetry.TrackException(e);

                    if (e is BusinessLightException)
                        e = new LightException("\r\n\r\n************ BUSINESS EXCEPTION ************\r\n", e);
                    else
                        e = new LightException("\r\n\r\n************ FRAMEWORK INTERNAL EXCEPTION ************\r\n", e);

                    handledException = true;
                    throw e;
                }
                catch (Exception e)
                {
                    e.FilterRelevantStackTrace();
                    
                    WorkBench.BaseTelemetry.TrackTrace(e.StackTrace);

                    try
                    {
                        if (e.InnerException is LightException)
                            e = new LightException("\r\n\r\n************ FRAMEWORK INTERNAL EXCEPTION ************\r\n", e.InnerException);

                        else if (e.InnerException?.InnerException is LightException)
                            e = new LightException("\r\n\r\n************ FRAMEWORK INTERNAL EXCEPTION ************\r\n", e.InnerException?.InnerException);
                    }
                    catch (Exception e2)
                    {
#pragma warning disable CA2200 // Rethrow to preserve stack details
                        e2.FilterRelevantStackTrace();
                        throw e2;
                    }

                    handledException = true;
                    throw e;
#pragma warning restore CA2200 // Rethrow to preserve stack details
                }
            }
            catch (Exception ex)
            {
                if (handledException)
#pragma warning disable CA2200 // Rethrow to preserve stack details
                    throw ex;
#pragma warning restore CA2200 // Rethrow to preserve stack details
                else
                    throw new LightException("Error while handling inner exception", ex);
            }
        }
    }
}