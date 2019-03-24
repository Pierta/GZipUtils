using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Veeam.TestSolution
{
    public static class FileInfoHelper
    {
        //http://blog.lugru.com/2010/06/compressing-decompressing-web-gzip-stream/
        //https://bamcisnetworks.wordpress.com/2017/05/22/decompressing-concatenated-gzip-files-in-c-received-from-aws-cloudwatch-logs/
        public static Dictionary<int, int> GetChunksOfGZip(this FileInfo sourceFile, int blockSize)
        {
            //Result
            Dictionary<int, int> chunkInfos = new Dictionary<int, int>();
            int chunkNumber = 1;

            /*
            * This pattern indicates the start of a GZip file as found from looking at the files
            * The file header is 10 bytes in size
            * 0-1 Signature 0x1F, 0x8B
            * 2 Compression Method - 0x08 is for DEFLATE, 0-7 are reserved
            * 3 Flags
            * 4-7 Last Modification Time
            * 8 Compression Flags
            * 9 Operating System
            */
            byte[] StartOfFilePattern = new byte[] { 0x1F, 0x8B, 0x08, 0x00 };

            using (FileStream originalFileStream = sourceFile.OpenRead())
            {
                byte[] buffer = new byte[blockSize];
                int readedBytesCount = 0;
                int wholeReadedBytes = 0;
                int lastStartIndex = 0;

                //Get the bytes of the file
                while ((readedBytesCount = originalFileStream.Read(buffer, 0, blockSize)) > 0)
                {
                    //This will limit the last byte we check to make sure it doesn't exceed the end of the file
                    //If the file is 100 bytes and the file pattern is 10 bytes, the last byte we want to check is
                    //90 -> i.e. we will check index 90, 91, 92, 93, 94, 95, 96, 97, 98, 99 and index 99 is the last
                    //index in the file bytes
                    int TraversableLength = readedBytesCount - StartOfFilePattern.Length;

                    for (int i = 0; i <= TraversableLength; i++)
                    {
                        bool Match = true;

                        //Test the next run of characters to see if they match
                        for (int j = 0; j < StartOfFilePattern.Length; j++)
                        {
                            //If the character doesn't match, break out
                            //We're making sure that i + j doesn't exceed the length as part
                            //of the loop bounds
                            if (buffer[i + j] != StartOfFilePattern[j])
                            {
                                Match = false;
                                break;
                            }
                        }

                        //If we did find a pattern
                        if (Match == true)
                        {
                            int startIndex = wholeReadedBytes + i;
                            if (chunkNumber > 1)
                            {
                                //Set length for previous chunk
                                chunkInfos[chunkNumber - 1] = startIndex - lastStartIndex;
                            }
                            //Remember last start index to compute next chunk's length
                            lastStartIndex = startIndex;
                            //Add new chunk info
                            chunkInfos.Add(chunkNumber, 0);

                            i += StartOfFilePattern.Length;
                            chunkNumber++;
                        }
                    }

                    //To prevent infinite looop
                    if (readedBytesCount.Equals(blockSize))
                    {
                        originalFileStream.Position -= 3;
                        wholeReadedBytes += readedBytesCount - 3;
                    }
                    else
                    {
                        wholeReadedBytes += readedBytesCount;
                    }
                }

                //To set last chunk's length
                if (chunkNumber > 1)
                {
                    //Set length for previous chunk
                    chunkInfos[chunkNumber - 1] = wholeReadedBytes - lastStartIndex;
                }
            }

            //In case the pattern doesn't match, just start from the beginning of the file
            if (!chunkInfos.Any())
            {
                chunkInfos.Add(chunkNumber, (int)sourceFile.Length);
            }
            return chunkInfos;
        }
    }
}