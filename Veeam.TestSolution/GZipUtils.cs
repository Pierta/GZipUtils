using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Veeam.TestSolution
{
    public static class GZipUtils
    {
        private static readonly int BlockSize = 1000000; // ~1Mb
        private static readonly object _locker = new object();
        private readonly static ConcurrentDictionary<int, WriteToStreamInfo> _cache = new ConcurrentDictionary<int, WriteToStreamInfo>();
        private static FileStream _outputFileStream;

        public static void Compress(FileInfo sourceFile, FileInfo destinationFile)
        {
            InternalGZipOperation(sourceFile, destinationFile, CompressionMode.Compress);
        }

        public static void Decompress(FileInfo sourceFile, FileInfo destinationFile)
        {
            InternalGZipOperation(sourceFile, destinationFile, CompressionMode.Decompress);
        }

        //Common logic for both operations
        private static void InternalGZipOperation(FileInfo sourceFile,
            FileInfo destinationFile, CompressionMode compressionMode)
        {
            using (_outputFileStream = File.Create(destinationFile.FullName))
            {
                Console.WriteLine($"Created file {destinationFile.FullName}");
                Thread.Sleep(1000);

                using (FileStream originalFileStream = sourceFile.OpenRead())
                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();

                    Thread fileWriterThread = new Thread(new ParameterizedThreadStart(WriteMemoryStreamToFile));
                    fileWriterThread.Start(Math.Ceiling((double)sourceFile.Length / BlockSize));

                    if (compressionMode == CompressionMode.Compress)
                    {
                        InternalCompressOperation(originalFileStream, compressionMode);
                    }
                    else
                    {
                        InteralDecompressOperation(sourceFile, originalFileStream, compressionMode);
                    }

                    fileWriterThread.Join();

                    sw.Stop();
                    Console.WriteLine($"Finished at {sw.Elapsed.TotalMilliseconds:0.0000}");
                }
            }

            Console.WriteLine($"{compressionMode}ed {sourceFile.Name} from {sourceFile.Length.ToString()} to {destinationFile.Length.ToString()} bytes.");
        }

        //Main compress multi-thread logic
        private static void InternalCompressOperation(FileStream originalFileStream, CompressionMode compressionMode)
        {
            ThreadFactory threadFactory = new ThreadFactory(WriteDataToMemoryStream);
            byte[] buffer = new byte[BlockSize];
            int readedBytesCount = 0;
            int number = 0;

            while ((readedBytesCount = originalFileStream.Read(buffer, 0, BlockSize)) > 0)
            {
                threadFactory.CreateNew().Start(new WriteToStreamInfo
                {
                    Number = ++number,
                    Buffer = (byte[])buffer.Clone(),
                    BytesCount = readedBytesCount,
                    CompressionMode = compressionMode
                });
            }
            while (threadFactory.IsAlive) { }
        }

        //Main decompress multi-thread logic
        private static void InteralDecompressOperation(FileInfo sourceFile, FileStream originalFileStream, CompressionMode compressionMode)
        {
            ThreadFactory threadFactory = new ThreadFactory(WriteDataToMemoryStream);
            int readedBytesCount = 0;
            var chunkInfos = sourceFile.GetChunksOfGZip(BlockSize);

            foreach (var chunkInfo in chunkInfos)
            {
                int chunkNumber = chunkInfo.Key;
                int chunkLength = chunkInfo.Value;
                byte[] buffer = new byte[chunkLength];

                if ((readedBytesCount = originalFileStream.Read(buffer, 0, chunkLength)) > 0)
                {
                    threadFactory.CreateNew().Start(new WriteToStreamInfo
                    {
                        Number = chunkNumber,
                        Buffer = (byte[])buffer.Clone(),
                        BytesCount = readedBytesCount,
                        CompressionMode = compressionMode
                    });
                }
            }
            while (threadFactory.IsAlive) { }
        }

        //Compress/Decompress next chunk to memory stream (in few threads)
        private static void WriteDataToMemoryStream(object input)
        {
            WriteToStreamInfo inputInfo = (WriteToStreamInfo)input;
            byte[] buffer;
            using (MemoryStream mStream = new MemoryStream())
            {
                if (inputInfo.CompressionMode == CompressionMode.Compress)
                {
                    using (GZipStream compressionStream = new GZipStream(mStream, inputInfo.CompressionMode))
                    {
                        compressionStream.Write(inputInfo.Buffer, 0, inputInfo.BytesCount);
                    }
                }
                else
                {
                    using (GZipStream decompressionStream = new GZipStream(new MemoryStream(inputInfo.Buffer), inputInfo.CompressionMode))
                    {
                        decompressionStream.CopyTo(mStream);
                    }
                }
                buffer = mStream.ToArray();
                Console.WriteLine($"{inputInfo.CompressionMode} package #{inputInfo.Number} (from {inputInfo.BytesCount} to {buffer.Length} bytes)...");
            }
            _cache.TryAdd(inputInfo.Number, new WriteToStreamInfo
            {
                Number = inputInfo.Number,
                Buffer = (byte[])buffer.Clone(),
                BytesCount = buffer.Length
            });
        }

        //Write memory stream chunk to output file
        private static void WriteMemoryStreamToFile(object input)
        {
            double chunksCount = (double)input;
            int number = 1;
            while (number <= chunksCount)
            {
                lock (_locker)
                {
                    if (_cache.TryGetValue(number, out WriteToStreamInfo cachedInfo))
                    {
                        _outputFileStream.Write(cachedInfo.Buffer, 0, cachedInfo.BytesCount);
                        Console.WriteLine($"Write package #{cachedInfo.Number} to destination file ({cachedInfo.BytesCount} bytes)...");
                        number++;
                    }
                }
            }
        }
    }
}