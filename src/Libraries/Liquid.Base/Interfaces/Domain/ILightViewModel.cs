namespace Liquid.Interfaces
{
    /// <summary>
    /// Interface delegates to the ViewModel the validation
    /// </summary>
    public interface ILightViewModel
    {
        /// <summary>
        /// Validation of model structure.
        /// </summary>
        void ValidateModel();
    }
}