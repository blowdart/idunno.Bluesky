// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using idunno.AtProto.Labels;

namespace idunno.Bluesky
{
    /// <summary>
    /// Properties for labels to apply to a post.
    /// </summary>
    public sealed record PostSelfLabels
    {
        /// <summary>
        /// Creates a new instance of <see cref="PostSelfLabels"/>
        /// </summary>
        public PostSelfLabels()
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="PostSelfLabels"/>
        /// </summary>
        /// <param name="labels">The AtProto <see cref="SelfLabels"/> to extract specific Bluesky post self labels from.</param>
        public PostSelfLabels(SelfLabels labels)
        {
            if (labels is not null)
            {
                if (labels.Contains(SelfLabelNames.GraphicMedia))
                {
                    GraphicMedia = true;
                }

                if (labels.Contains(SelfLabelNames.Nudity))
                {
                    Nudity = true;
                }

                if (labels.Contains(SelfLabelNames.Porn))
                {
                    Porn = true;
                }

                if (labels.Contains(SelfLabelNames.Sexual))
                {
                    SexualContent = true;
                }
            }
        }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains graphic media.
        /// This behaves like <see cref="Porn"/> but is for violence or gore.
        /// </summary>
        public bool GraphicMedia { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains nudity.
        /// This puts a warning on images but isn't 18+ and defaults to ignore.
        /// </summary>
        public bool Nudity { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains porn.
        /// This puts a warning on images and can only be clicked through if the user is 18+ and has enabled adult content.
        /// </summary>
        public bool Porn { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains sexual content.
        /// This behaves like <see cref="Porn"/> but is meant to handle less intense sexual content.
        /// </summary>
        public bool SexualContent { get; set; }


        /// <summary>
        /// Converts this instance of <see cref="PostSelfLabels"/> to an instance of <see cref="SelfLabels"/>.
        /// </summary>
        /// <returns>A new instance of <see cref="SelfLabels"/> containing the post specific labels set in this instance.</returns>
        public SelfLabels ToSelfLabels()
        {
            var result = new SelfLabels();

            if (GraphicMedia)
            {
                result.AddLabel(SelfLabelNames.GraphicMedia);
            }

            if (Nudity)
            {
                result.AddLabel(SelfLabelNames.Nudity);
            }

            if (Porn)
            {
                result.AddLabel(SelfLabelNames.Porn);
            }

            if (SexualContent)
            {
                result.AddLabel(SelfLabelNames.Porn);
            }

            return result;
        }

        /// <summary>
        /// Converts <paramref name="postSelfLabels"/> to an instance of <see cref="SelfLabels"/>.
        /// </summary>
        /// <param name="postSelfLabels">The instance of <see cref="PostSelfLabels"/> to convert.1</param>
        /// <returns>A new instance of <see cref="SelfLabels"/> containing the post specific labels from <paramref name="postSelfLabels"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postSelfLabels"/> is <see langword="null"/>.</exception>
        public static explicit operator SelfLabels(PostSelfLabels postSelfLabels)
        {
            ArgumentNullException.ThrowIfNull(postSelfLabels);
            return postSelfLabels.ToSelfLabels();
        }
    }
}
