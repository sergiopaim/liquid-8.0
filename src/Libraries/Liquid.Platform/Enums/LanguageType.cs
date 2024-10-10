using Liquid.Domain;

namespace Liquid.Platform
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// Type of languages for choice of the users
    /// </summary>
    public class LanguageType(string code) : LightLocalizedEnum<LanguageType>(code)
    {
        public static readonly LanguageType pt = new(nameof(pt));
        public static readonly LanguageType en = new(nameof(en));
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}