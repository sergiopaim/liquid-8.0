using System;
using System.Collections.Generic;
using System.IO;

namespace Liquid.Interfaces
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public interface ILightIntelligence : IWorkBenchHealthCheck
    {
        /// <summary>
        /// Detects the faces present in a picture.
        /// </summary>
        /// <param name="pictureUri">The URI of the picture containing faces to be detected</param>
        /// <returns>The list of area for the faces detected</returns>
        IEnumerable<ImageDetection> DetectFacesInPicture(Uri pictureUri);
        /// <summary>
        /// Detects the faces present in a picture.
        /// </summary>
        /// <param name="picture">The picture in memory to get faces redacted</param>
        /// <param name="faces">The list of face rectangles to redact</param>
        /// <returns>The new version of the picture with faces redacted</returns>
        MemoryStream RedactFacesInPicture(Stream picture, IEnumerable<ImageDetection> faces);

        /// <summary>
        /// A rectangle delimiting a detection in a picture
        /// </summary>
        public class ImageDetection 
        {
            /// <summary>
            /// The top position of the detection in the image
            /// </summary>
            public int Top { get; set; }
            /// <summary>
            /// The left position of the detection in the image
            /// </summary>
            public int Left { get; set; }
            /// <summary>
            /// The width of the detection
            /// </summary>
            public int Width { get; set; }
            /// <summary>
            /// The height of the detection
            /// </summary>
            public int Height { get; set; }
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}