// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.FireHose;

internal static class StreamExtensions
{
    const string VarintTooBig = "Varint value is too big";

    public static async Task<int> ReadVarint32Async(this Stream stream, CancellationToken cancellationToken = default)
    {
        long result = await stream.ReadVarint64Async(cancellationToken).ConfigureAwait(false);
        return result > int.MaxValue ? throw new InvalidDataException(VarintTooBig) : (int)result;
    }

    public static async Task<long> ReadVarint64Async(this Stream stream, CancellationToken cancellationToken = default)
    {
        long value = 0;
        int shift = 0;
        int bytesRead = 0;
        byte[] buffer = new byte[1];

        while (true)
        {
            if (1 != await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false))
            {
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException();
                }

                throw new InvalidDataException("Unterminated varint");
            }
            if (++bytesRead > 9)
            {
                throw new InvalidDataException(VarintTooBig);
            }

            byte b = buffer[0];
            value |= (long)(b & 0x7F) << shift;
            if (b < 0x80)
            {
                return value;
            }

            shift += 7;
        }
    }
}
