using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeepBackup.CompressionAndEncryption
{
    internal static class KeepLzmaCompressor
    {
        public static void Compress(Stream input, Stream output)
        {
            SevenZip.Compression.LZMA.Encoder encoder = new SevenZip.Compression.LZMA.Encoder();
            
            encoder.WriteCoderProperties(output);
            output.Write(BitConverter.GetBytes(input.Length), 0, 8);
            encoder.Code(input, output, input.Length, -1, null);
            output.Flush();
        }

        public static void Decompress(Stream input, Stream output)
        {
            SevenZip.Compression.LZMA.Decoder decoder = new SevenZip.Compression.LZMA.Decoder();

            byte[] properties = new byte[5];
            input.Read(properties, 0, 5);

            byte[] fileLengthBytes = new byte[8];
            input.Read(fileLengthBytes, 0, 8);
            long fileLength = BitConverter.ToInt64(fileLengthBytes, 0);

            decoder.SetDecoderProperties(properties);
            decoder.Code(input, output, input.Length, fileLength, null);
            output.Flush();
        }

    }
}
