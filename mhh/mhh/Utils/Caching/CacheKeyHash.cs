
using System.Text;

namespace mhh;

// Murmur3 x64 128-bit low-collision string-hashing algorithm adapted from:
// https://blog.teamleadnet.com/2012/08/murmurhash3-ultra-fast-hash-algorithm.html
// IntHelpers moved to Utils\Extensions.cs and removed unsafe requirement
// changed to fully static
// NOT thread-safe

public static class CacheKeyHash
{
    private const ulong READ_SIZE = 16;
    private const ulong C1 = 0x87c37b91114253d5L;
    private const ulong C2 = 0x4cf5ad432745937fL;

    private static ulong length;
    private static ulong h1;
    private static ulong h2;

    public static byte[] ComputeHash(string ascii)
        => ComputeHash(Encoding.ASCII.GetBytes(ascii));

    public static byte[] ComputeHash(byte[] bb)
    {
        ProcessBytes(bb);

        h1 ^= length;
        h2 ^= length;

        h1 += h2;
        h2 += h1;

        h1 = MixFinal(h1);
        h2 = MixFinal(h2);

        h1 += h2;
        h2 += h1;

        var hash = new byte[READ_SIZE];

        Array.Copy(BitConverter.GetBytes(h1), 0, hash, 0, 8);
        Array.Copy(BitConverter.GetBytes(h2), 0, hash, 8, 8);

        return hash;
    }

    private static void ProcessBytes(byte[] bb)
    {
        h1 = 0L;
        h2 = 0L;
        length = 0L;

        int pos = 0;
        ulong remaining = (ulong)bb.Length;

        // read 128 bits, 16 bytes, 2 longs in eacy cycle
        while (remaining >= READ_SIZE)
        {
            ulong k1 = bb.GetUInt64(pos);
            pos += 8;

            ulong k2 = bb.GetUInt64(pos);
            pos += 8;

            length += READ_SIZE;
            remaining -= READ_SIZE;

            MixBody(k1, k2);
        }

        // if the input MOD 16 != 0
        if (remaining > 0) ProcessBytesRemaining(bb, remaining, pos);
    }

    private static void ProcessBytesRemaining(byte[] bb, ulong remaining, int pos)
    {
        ulong k1 = 0;
        ulong k2 = 0;
        length += remaining;

        // little endian (x86) processing
        switch (remaining)
        {
            case 15:
                k2 ^= (ulong)bb[pos + 14] << 48; // fall through
                goto case 14;
            case 14:
                k2 ^= (ulong)bb[pos + 13] << 40; // fall through
                goto case 13;
            case 13:
                k2 ^= (ulong)bb[pos + 12] << 32; // fall through
                goto case 12;
            case 12:
                k2 ^= (ulong)bb[pos + 11] << 24; // fall through
                goto case 11;
            case 11:
                k2 ^= (ulong)bb[pos + 10] << 16; // fall through
                goto case 10;
            case 10:
                k2 ^= (ulong)bb[pos + 9] << 8; // fall through
                goto case 9;
            case 9:
                k2 ^= (ulong)bb[pos + 8]; // fall through
                goto case 8;
            case 8:
                k1 ^= bb.GetUInt64(pos);
                break;
            case 7:
                k1 ^= (ulong)bb[pos + 6] << 48; // fall through
                goto case 6;
            case 6:
                k1 ^= (ulong)bb[pos + 5] << 40; // fall through
                goto case 5;
            case 5:
                k1 ^= (ulong)bb[pos + 4] << 32; // fall through
                goto case 4;
            case 4:
                k1 ^= (ulong)bb[pos + 3] << 24; // fall through
                goto case 3;
            case 3:
                k1 ^= (ulong)bb[pos + 2] << 16; // fall through
                goto case 2;
            case 2:
                k1 ^= (ulong)bb[pos + 1] << 8; // fall through
                goto case 1;
            case 1:
                k1 ^= (ulong)bb[pos]; // fall through
                break;
            default:
                throw new Exception($"{nameof(CacheKeyHash.ProcessBytesRemaining)} remaining bytes calculation failed");
        }

        h1 ^= MixKey1(k1);
        h2 ^= MixKey2(k2);
    }

    private static void MixBody(ulong k1, ulong k2)
    {
        h1 ^= MixKey1(k1);

        h1 = h1.RotateLeft(27);
        h1 += h2;
        h1 = h1 * 5 + 0x52dce729;

        h2 ^= MixKey2(k2);

        h2 = h2.RotateLeft(31);
        h2 += h1;
        h2 = h2 * 5 + 0x38495ab5;
    }

    private static ulong MixKey1(ulong k1)
    {
        k1 *= C1;
        k1 = k1.RotateLeft(31);
        k1 *= C2;
        return k1;
    }

    private static ulong MixKey2(ulong k2)
    {
        k2 *= C2;
        k2 = k2.RotateLeft(33);
        k2 *= C1;
        return k2;
    }

    private static ulong MixFinal(ulong k)
    {
        // avalanche bits

        k ^= k >> 33;
        k *= 0xff51afd7ed558ccdL;
        k ^= k >> 33;
        k *= 0xc4ceb9fe1a85ec53L;
        k ^= k >> 33;
        return k;
    }
}
