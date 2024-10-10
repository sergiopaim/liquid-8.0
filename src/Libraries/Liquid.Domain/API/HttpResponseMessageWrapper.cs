﻿using Liquid.Base;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Liquid.Domain.API
{
    /// <summary>
    /// A structure to generically wrapp a domain response as http response
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class HttpResponseMessageWrapper<T>
    {
        /// <summary>
        /// The type of the response content
        /// </summary>
        public T Content { get; set; }

        /// <summary>
        /// The https status code of the response
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// Builds a http response message wrapper from the http response
        /// </summary>
        /// <param name="response">the http response</param>
        public HttpResponseMessageWrapper(HttpResponseMessage response)
        {
            StatusCode = response.StatusCode;

            if (StatusCode == HttpStatusCode.NoContent || StatusCode == HttpStatusCode.NotFound)
                return;

            string stringContent = response?.Content?.ReadAsStringAsync().Result;

            try
            {
                try
                {
                    Content = (T)JsonSerializer.Deserialize(stringContent,
                                                            typeof(T),
                                                            LightGeneralSerialization.IgnoreCase);
                }
                catch
                {
                    stringContent = $"\"{JsonEncodedText.Encode(stringContent)}\"";

                    if (typeof(T) == typeof(DomainResponse))
                    {
                        Content = (T)Convert.ChangeType(new DomainResponse() { Payload = JsonDocument.Parse(stringContent) }, typeof(T));
                    }
                    else if (typeof(T) == typeof(JsonDocument))
                    {
                        Content = (T)Convert.ChangeType(JsonSerializer.Deserialize<JsonDocument>(stringContent, LightGeneralSerialization.Default), typeof(JsonDocument));
                    }
                    else
                    {
                        try
                        {
                            Content = (T)Convert.ChangeType(JsonSerializer.Deserialize<string>(stringContent, LightGeneralSerialization.Default), typeof(string));
                        }
                        catch
                        {
                            var doc = stringContent.ToJsonDocument();

                            var critics = doc.Property("critics").ToObject<List<Critic>>();

                            Content = (T)Convert.ChangeType(new DomainResponse() 
                                                            {
                                                                Critics = critics, 
                                                                OperationId = doc.Property("operationId").AsString(),
                                                            }, 
                                                            typeof(T));
                        }
                    }
                }
            }
            catch
            {
                return;
            }
        }
    }
}