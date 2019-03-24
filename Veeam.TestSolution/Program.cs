using System;
using System.Collections.Generic;
using System.IO;

namespace Veeam.TestSolution
{
    public class Program
    {
        private const string ArgumentCountErrorText =
            "You missed some parameters!\nUsage: Veeam.TestSolution.exe [compress|decompress] [source file name] [destination file name]";

        public static int Main(string[] args)
        {
            if (args.Length != 3 ||
                !(new List<string> { "compress", "decompress" }).Contains(args[0]) ||
                !File.Exists(args[1]))
            {
                Console.WriteLine(ArgumentCountErrorText);
                return 1;
            }

            Console.WriteLine($"Operation will be executed in {Environment.ProcessorCount} thread(s)");

            try
            {
                switch (args[0])
                {
                    case "decompress":
                        {
                            GZipUtils.Decompress(new FileInfo(args[1]), new FileInfo(args[2]));
                            break;
                        }
                    case "compress":
                    default:
                        {
                            GZipUtils.Compress(new FileInfo(args[1]), new FileInfo(args[2]));
                            break;
                        }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                return 1;
            }

            return 0;
        }
    }
}