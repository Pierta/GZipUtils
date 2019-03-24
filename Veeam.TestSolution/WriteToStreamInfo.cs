using System.IO.Compression;

namespace Veeam.TestSolution
{
    internal class WriteToStreamInfo
    {
        public int Number { get; set; }

        public byte[] Buffer { get; set; }

        public int BytesCount { get; set; }

        public CompressionMode CompressionMode { get; set; }
    }
}