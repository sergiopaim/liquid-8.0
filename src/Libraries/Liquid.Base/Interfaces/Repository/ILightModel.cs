﻿using System.Collections.Generic;

namespace Liquid.Interfaces
{
    /// <summary>
    /// Model entity class handled by the LightRepository
    /// </summary>
    public interface ILightModel
    {
        /// <summary>
        /// Id of the model entity
        /// </summary>
        string Id { get; set; }
        /// <summary>
        /// The partition value automatically derived from the id (if AutoPartition attribute is set for the LightModel)
        /// </summary>
        string AutoPartition { get; set; }
        /// <summary>
        /// List of attachments of the Domain entity
        /// </summary>
        public List<string> Attachments { get; set; }
        /// <summary>
        /// Validation of model structure
        /// </summary>
        void ValidateModel();
    }
}