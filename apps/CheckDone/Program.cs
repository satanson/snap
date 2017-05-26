﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ASELib;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace CheckDone
{
    class Program
    {

        static Dictionary<string, List<string>> FilenamesByDataDirectory = new Dictionary<string, List<string>>();
        static ASETools.ASEConfirguation configuration;
        static int totalFiles = 0;

        static void ProcessOneDataDirectory(List<string> filenames)
        {
            const int nBytesToCheck = 10; // **done**\n\r
            var lastBitOfFile = new byte[nBytesToCheck];   

            foreach (var filename in filenames)
            {
                if (filename.ToLower().EndsWith(".gz"))
                {
                    //
                    // We can't seek in gzipped files, so just read the whole thing.
                    //
                    var reader = ASETools.CreateCompressedStreamReaderWithRetry(filename);

                    string line;
                    bool seenDone = false;
                    while (null != (line = reader.ReadLine()))
                    {
                        if (seenDone)
                        {
                            Console.WriteLine(filename + " continues after **done**");
                            break;
                        }

                        if (line == "**done**")
                        {
                            seenDone = true;
                        }
                    } // while we have an input line

                    if (!seenDone)
                    {
                        Console.WriteLine(filename + " is truncated.");
                    }
                } else
                {
                    //
                    // Not compressed, so just seek to the end of the file.  This skips checking for multiple **done** lines, but
                    // that's not really a problem I can forsee happening.
                    //
                    FileStream filestream;
                    try
                    {
                        filestream = new FileStream(filename, FileMode.Open);
                    } catch
                    {
                        Console.WriteLine("Exception opening " + filename + ", ignoring.");
                        continue;
                    }

                    if (filestream.Length < nBytesToCheck)
                    {
                        Console.WriteLine(filename + " is truncated.");
                        continue;
                    }

                    filestream.Position = filestream.Length - nBytesToCheck;
                    if (nBytesToCheck != filestream.Read(lastBitOfFile, 0, nBytesToCheck))
                    {
                        Console.WriteLine("Error reading " + filename);
                        continue;
                    }

                    if (lastBitOfFile[0] != '*' ||
                        lastBitOfFile[1] != '*' ||
                        lastBitOfFile[2] != 'd' ||
                        lastBitOfFile[3] != 'o' ||
                        lastBitOfFile[4] != 'n' ||
                        lastBitOfFile[5] != 'e' ||
                        lastBitOfFile[6] != '*' ||
                        lastBitOfFile[7] != '*' ||
                        lastBitOfFile[8] != '\r' ||
                        lastBitOfFile[9] != '\n')
                    {
                        Console.WriteLine(filename + " is truncated.");
                    }
                } // If it's not gzipped
            } // foreach filename
        } // ProcessOneDataDirectory

        static void Main(string[] args)
        {
            var timer = new Stopwatch();
            timer.Start();

            configuration = ASETools.ASEConfirguation.loadFromFile(args);

            if (configuration.commandLineArgs.Count() != 0)
            {
                Console.WriteLine("usage: CheckDone {-configuration configurationFileName}");
                return;
            }

            var cases = ASETools.Case.LoadCases(configuration.casesFilePathname);

            if (null == cases)
            {
                Console.WriteLine("Unable to load cases.  You must generate it before running this tool.");
                return;
            }

            foreach (var caseEntry in cases)
            {
                var case_ = caseEntry.Value;

                HandleFilename(case_.normal_dna_allcount_filename);
                HandleFilename(case_.normal_rna_allcount_filename);
                HandleFilename(case_.tumor_dna_allcount_filename);
                HandleFilename(case_.tumor_rna_allcount_filename);
                HandleFilename(case_.regional_expression_filename);
                HandleFilename(case_.gene_expression_filename);
                HandleFilename(case_.selected_variants_filename);
                HandleFilename(case_.normal_dna_reads_at_selected_variants_index_filename);
                HandleFilename(case_.normal_rna_reads_at_selected_variants_index_filename);
                HandleFilename(case_.tumor_dna_reads_at_selected_variants_index_filename);
                HandleFilename(case_.tumor_rna_reads_at_selected_variants_filename);
                HandleFilename(case_.annotated_selected_variants_filename);
                HandleFilename(case_.tumor_dna_gene_coverage_filname);
                HandleFilename(case_.extracted_maf_lines_filename);
            }

            //
            // Run one thread per data directory, since this is likely IO bound by the server disks rather than
            // by the local processors.
            //
            var threads = new List<Thread>();
            foreach (var dataDirectoryEntry in FilenamesByDataDirectory)
            {
                threads.Add(new Thread(() => ProcessOneDataDirectory(dataDirectoryEntry.Value)));
            }

            threads.ForEach(t => t.Start());
            threads.ForEach(t => t.Join());

            Console.WriteLine("Processed " + totalFiles + " files in " + ASETools.ElapsedTimeInSeconds(timer));
        } // Main

        static void HandleFilename(string filename)
        {
            var dataDirectory = ASETools.GetDataDirectoryFromFilename(filename, configuration);

            if (!FilenamesByDataDirectory.ContainsKey(dataDirectory))
            {
                FilenamesByDataDirectory.Add(dataDirectory, new List<string>());
            }

            FilenamesByDataDirectory[dataDirectory].Add(filename);
            totalFiles++;
        }
    }
}