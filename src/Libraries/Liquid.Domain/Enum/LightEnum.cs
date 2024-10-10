﻿using Liquid.Base;
using Liquid.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Liquid.Domain
{
    /// <summary>
    /// Exception for invalid code of a lightEnum
    /// </summary>
    /// <remarks>
    /// Building a LightException with summary data
    /// </remarks>
    /// <param name="code">The code the the enum</param>
    /// <param name="type">The LightEnum type</param>
    [Serializable]
    public class EnumInvalidCodeLightException(string code, Type type) : LightException($"`{code}` is not a valid code for `{type?.Name}` class") { }

    /// <summary>
    /// Enum style class easier to be stored, received and sent REST APIs, Message buses e reactive hubs
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class LightEnum<T> : ILightEnum, IComparable where T : LightEnum<T>
    {
        private static int _instantiationOrderCounter = -1;
        private int InternalOrder { get; }

        private static List<T> _all = null;
        private static List<T> All
        {
            get
            {
                if (_all is null)
                {
                    var type = typeof(T);
                    var fields = type.GetFields(BindingFlags.Public |
                                                BindingFlags.Static |
                                                BindingFlags.DeclaredOnly);

                    _all = [.. fields.Select(f => (T)f.GetValue(type)).OrderBy(f => f.InternalOrder)];
                }

                return _all;
            }
        }

        /// <summary>
        /// The code of the enum 
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Creates an enum instance for the given code
        /// </summary>
        /// <param name="code">the enum code</param>
        protected LightEnum(string code)
        {
            code = code.FirstToLower();

            InternalOrder = Interlocked.Increment(ref _instantiationOrderCounter);

            var codeNames = typeof(T).GetFields(BindingFlags.Public
                                             | BindingFlags.Static
                                             | BindingFlags.DeclaredOnly)
                                     .Select(f => f.Name.FirstToLower())
                                     .ToList();

            if (!codeNames.Any(c => c == code))
                throw new EnumInvalidCodeLightException(code, typeof(T));

            Code = code;
        }

        /// <summary>
        /// Gets all valid type codes
        /// </summary>
        /// <returns></returns>
        public static List<string> GetAllCodes()
        {
            return All.Select(f => f.Code).ToList();
        }

        /// <summary>
        /// Gets all valid type codes other than the ones to ignore
        /// </summary>
        /// <param name="codeToIgnore">Code to ignore</param>
        /// <returns></returns>
        public static List<string> GetAllCodesOtherThan(string codeToIgnore)
        {
            return GetAllCodesOtherThan([codeToIgnore]);
        }

        /// <summary>
        /// Gets all valid type codes other than the ones to ignore
        /// </summary>
        /// <param name="codesToIgnore">Codes to ignore</param>
        /// <returns></returns>
        public static List<string> GetAllCodesOtherThan(List<string> codesToIgnore)
        {
            List<string> all = GetAllCodes();

            if (codesToIgnore is null)
                return all;
            else
                return all.Where(c => !codesToIgnore.Contains(c)).ToList();
        }

        /// <summary>
        /// Gets all valid types
        /// </summary>
        /// <returns></returns>
        public static List<T> GetAll()
        {
            return All;
        }

        /// <summary>
        /// Gets the LightEnum of a given code
        /// </summary>
        /// <param name="code">code to ignore</param>
        /// <returns></returns>
        public static T OfCode(string code)
        {
            if (code is null)
                return null;

            var lightEnum = All.FirstOrDefault(e => e.Code == code);

            return lightEnum is null
                ? throw new LightException($"The code '{code}' is not defined in LightEnum '{typeof(T)}'")
                : All.FirstOrDefault(e => e.Code == code);
        }

        /// <summary>
        /// Gets all valid type other than the code to ignore
        /// </summary>
        /// <param name="codeToIgnore">Code to ignore</param>
        /// <returns></returns>
        public static List<T> GetAllOtherThan(string codeToIgnore)
        {
            return GetAllOtherThan([codeToIgnore]);
        }

        /// <summary>
        /// Gets all valid type other than the codes to ignore
        /// </summary>
        /// <param name="codesToIgnore">Codes to ignore</param>
        /// <returns></returns>
        public static List<T> GetAllOtherThan(List<string> codesToIgnore)
        {
            List<T> all = GetAll();

            if (codesToIgnore is null)
                return all;
            else
                return all.Where(c => !codesToIgnore.Contains(c.Code)).ToList();
        }

        /// <summary>
        /// Gets the order for code inside the LightEnum type
        /// </summary>
        /// <param name="code">code</param>
        /// <returns>The order</returns>
        public static int GetOrder(string code)
        {
            return OfCode(code)?.InternalOrder ?? 0;
        }

        /// <summary>
        /// Checks if code is a valid one
        /// </summary>
        /// <param name="code">code</param>
        /// <returns></returns>
        public static bool IsValid(string code)
        {
            return GetAllCodes().Contains(code);
        }

        /// <summary>
        /// Checks if all codes in a list are valid ones and if the list is not empty or null
        /// </summary>
        /// <param name="codes">List of codes</param>
        /// <returns></returns>
        public static bool IsValid(List<string> codes)
        {
            if (codes is null || codes.Count == 0)
                return false;

            foreach (string code in codes)
                if (!GetAllCodes().Contains(code))
                    return false;

            return true;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            LightEnum<T> otherValue = obj as LightEnum<T>;

            var typeMatches = GetType().Equals(obj.GetType());
            var valueMatches = Code.Equals(otherValue.Code);

            return typeMatches && valueMatches;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => base.GetHashCode();

        /// <summary>
        /// Compares to other LightEnum instance indicatings whether
        /// this instance precedes, follows, or appears in the same position in the sort
        /// order as the specified instance.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(object other) => Code.CompareTo(((LightEnum<T>)other)?.Code);

        /// <inheritdoc/>
        public override string ToString() => Code;

        public IEnumerable<ILightEnum> ListAll()
        {
            return All;
        }
    }
}