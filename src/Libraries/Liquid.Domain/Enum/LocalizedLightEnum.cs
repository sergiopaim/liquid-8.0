namespace Liquid.Domain
{
    /// <summary>
    /// Abstract enumeration type with localized labels
    /// </summary>
    /// <typeparam name="T">The type of the enum</typeparam>
    /// <remarks>
    /// Creates an enum instance for the given code
    /// </remarks>
    /// <param name="code">the enum code</param>
    public abstract class LightLocalizedEnum<T>(string code) : LightEnum<T>(code) where T : LightLocalizedEnum<T>
    {
        /// <summary>
        /// The localized label associated to the enum code
        /// </summary>
        public string Label => LightLocalizer.Localize(GetType().Name.ToUpper() + "_" + Code.ToUpper());
    }
}