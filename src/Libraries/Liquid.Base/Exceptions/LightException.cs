﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Liquid.Base
{
    /// <summary>
    /// Class responsible for building the Exception
    /// </summary>
    [Serializable]
    public class LightException : Exception, ISerializable
    {
        /// <summary>
        /// Gets the newline string defined for this environment. 
        /// In this case splits the Sources in a list
        /// </summary>
        public override string Source
        {
            get
            {
                List<string> source = [.. base.Source.Split(new string[] { Environment.NewLine }, StringSplitOptions.None)];
                source.RemoveAll(x => x.Contains("Liquid.Base"));
                return string.Join(Environment.NewLine, [.. source]);
            }
        }

        /// <summary>
        /// Gets the newline string defined for this environment. 
        /// In this case splits the StackTrace in a list
        /// </summary>
        public override string StackTrace => FilteredStackTrace.Filter(base.StackTrace);

        /// <summary>
        /// Throw an exception
        /// </summary>
        public LightException() : base()
        {
            this.FilterRelevantStackTrace();
        }

        /// <summary>
        /// Throw an exception with a message 
        /// </summary>
        /// <param name="message">the message to showed with the exception</param>
        public LightException(string message) : base(message)
        {
            this.FilterRelevantStackTrace();
        }

        /// <summary>
        /// Throw an exception with a message and details of the object Exception
        /// </summary>
        /// <param name="message">the message to showed with the exception</param>
        /// <param name="innerException">describes the error that caused the current exception</param>
        public LightException(string message, Exception innerException) : base(message, innerException) 
        {
            this.FilterRelevantStackTrace();
        }
    }
}