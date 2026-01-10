// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace Samples.CustomRecords
{
    internal sealed record Track : AtProtoRecord
    {
        [JsonConstructor]
        public Track(string name, string artistName, string? albumName, DateTimeOffset? listeningStartedAt = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentException.ThrowIfNullOrEmpty(artistName);

            listeningStartedAt ??= DateTimeOffset.UtcNow;

            if (name.Length > 3000 || name.GetGraphemeLength() > 300)
            {
                 // Arbitrary limits for demonstration purposes.
                throw new ArgumentException("Track name is too long.", nameof(name));
            }

            if (artistName.Length > 3000 || artistName.GetGraphemeLength() > 300)
            {
                // Arbitrary limits for demonstration purposes.
                throw new ArgumentException("Artist name is too long.", nameof(artistName));
            }

            if (albumName is not null && (albumName.Length > 3000 || albumName.GetGraphemeLength() > 300))
            {
                // Arbitrary limits for demonstration purposes.
                throw new ArgumentException("Album name is too long.", nameof(albumName));
            }

            Name = name;
            Artist = artistName;
            AlbumName = albumName;
            ListeningStartedAt = listeningStartedAt.Value;
        }

        [JsonInclude]
        [JsonRequired]
        public string Name { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public string Artist { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public string? AlbumName { get; internal set; }

        [JsonInclude]
        public DateTimeOffset ListeningStartedAt { get; internal set; } = DateTimeOffset.UtcNow;
    }
}
