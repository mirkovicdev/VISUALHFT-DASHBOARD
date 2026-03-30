
using System;
using System.Text;

namespace MarketConnectors.Kraken
{
    public class Crc32Calculator
    {
        private static readonly uint[] _crc32Table;

        // Static constructor: Runs only once, the first time the class is used.
        static Crc32Calculator()
        {
            _crc32Table = new uint[256];
            const uint polynomial = 0xEDB88320;
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) == 1 ? (crc >> 1) ^ polynomial : crc >> 1;
                }
                _crc32Table[i] = crc;
            }
        }

        public static long ComputeCrc32(string input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            byte[] bytes = Encoding.UTF8.GetBytes(input);
            return ComputeCrc32(bytes);
        }

        public static long ComputeCrc32(byte[] inputBytes)
        {
            if (inputBytes == null)
            {
                throw new ArgumentNullException(nameof(inputBytes));
            }

            uint crcValue = 0xFFFFFFFF;

            foreach (byte b in inputBytes)
            {
                crcValue = (_crc32Table[(crcValue ^ b) & 0xFF] ^ (crcValue >> 8));
            }

            return (long)~crcValue;
        }
    }
}