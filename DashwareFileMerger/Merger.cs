using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DashwareFileMerger
{
    public class Merger
    {
        public void Run(List<string> files)
        {
            var programDir = Path.GetDirectoryName(new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath);

            // read in time column names from the directory in which this exe is located
            if (!File.Exists(Path.Combine(programDir, "TimeColumnNames.txt")))
            {
                throw new Exception("Missing file 'TimeColumnNames.txt' in the same directory as this EXE");
            }
            var timeColumns = new List<string>(File.ReadAllLines(Path.Combine(programDir, "TimeColumnNames.txt")));
            timeColumns = timeColumns.Select(l => l.Trim()).ToList();
            timeColumns = timeColumns.Where(l => !l.StartsWith("//")).ToList();
            timeColumns = timeColumns.Where(l => !string.IsNullOrEmpty(l)).ToList();

            // read in accumulative column names from the directory in which this exe is located
            if (!File.Exists(Path.Combine(programDir, "AccumulativeColumnNames.txt")))
            {
                throw new Exception("Missing file 'AccumulativeColumnNames.txt' in the same directory as this EXE");
            }
            var accumulativeColumns = new List<string>(File.ReadAllLines(Path.Combine(programDir, "AccumulativeColumnNames.txt")));
            accumulativeColumns = accumulativeColumns.Select(l => l.Trim()).ToList();
            accumulativeColumns = accumulativeColumns.Where(l => !l.StartsWith("//")).ToList();
            accumulativeColumns = accumulativeColumns.Where(l => !string.IsNullOrEmpty(l)).ToList();

            // no args, present instructions
            if (files.Count == 0)
            {
                throw new Exception("No arguments given.");
            }

            // assume bare files are in current directory
            string activeDir = Environment.CurrentDirectory;

            // if first argument is a directory (not a file) assume all files are in that directory instead
            // note: Directory.Exists returns false if presented with a file name, even if fully-qualified
            if (Directory.Exists(files[0]))
            {
                activeDir = files[0];
                files.Remove(files[0]); // ignore the args inititialization
            }

            // if only a directory is given, read the files from that directory in sorted order
            if (files.Count() == 0)
            {
                files = Directory.EnumerateFiles(activeDir, "*.csv").ToList();
                files.Sort();

                // add a generated output path as if it was input on the command line
                files.Add(files[0].Substring(0, files[0].Length - 4 /* '.csv' */) + "_combined.csv");
            }

            // prepend path as needed based on detected active dir
            foreach (string file in files)
            {
                if (!Path.IsPathRooted(file))
                {
                    files[files.IndexOf(file)] = Path.Combine(activeDir, file);
                }
            }

            // pop off output path
            string output = files[files.Count() - 1];
            files.RemoveAt(files.Count() - 1);
            if (File.Exists(output))
            {
                throw new Exception($"Output file already exists, or no output file was specified: {output}");
                return;
            }

            // grab file contents
            int fileNum = 0;
            string[] originalColumnNames = null;
            string[] timeStepPerLine = null;
            string[] lastDataLinePreviousFile = null;
            foreach (string file in files)
            {
                fileNum++;

                // read file
                Console.WriteLine($"Reading file {fileNum} at {file}");
                var lines = new List<string>(File.ReadAllLines(file));
                lines = lines.Select(l => l.Trim()).ToList();
                lines = lines.Where(l => l.Trim().Length != 0).ToList();

                // require header + two data lines for logic reasons - could make this more optimial to require header + single data line but not worth the effort
                if (lines.Count() < 3)
                {
                    throw new Exception("Not enough lines in file, it may be corrupted, aborting");
                    File.Delete(output);
                    return;
                }

                // parse header
                var headerLine = lines[0];
                lines.RemoveAt(0);
                var newColumnNames = headerLine.Split(",".ToCharArray()).Select(l => l.Trim()).ToArray();
                Console.WriteLine($"Found {newColumnNames.Length} columns and {lines.Count()} data lines");

                if (fileNum == 1)
                {
                    // save column order for later files because DashWare isn't consistent :(
                    originalColumnNames = newColumnNames;

                    // only valid for zero-based time columns, but we'll need it later - this is a proxy for "time step between data lines"
                    timeStepPerLine = lines[1].Split(",".ToCharArray()).ToArray();

                    // just copy-through on the first file
                    Console.WriteLine("Writing through all data lines since this is the first file");
                    File.AppendAllText(output, headerLine + Environment.NewLine);
                    File.AppendAllLines(output, lines);

                    // preserved so we can add the increment per-line for things like time-based columns by adding the timeStepPerLine value
                    // otherwise we'd miss a "time slice" between files by adding zero when re-calculating the column values
                    lastDataLinePreviousFile = lines[lines.Count() - 1].Split(",".ToCharArray()).ToArray();
                }
                else
                {
                    // sanity check
                    if (newColumnNames.Length != originalColumnNames.Length)
                    {
                        Console.WriteLine("Column count does not match first file, aborting");
                        File.Delete(output);
                        return;
                    }

                    // sanity check
                    if (string.Join(",", newColumnNames.OrderBy((x) => x)) != string.Join(",", originalColumnNames.OrderBy((x) => x)))
                    {
                        Console.WriteLine("Column names (sorted) do not match first file, aborting");
                        File.Delete(output);
                        return;
                    }

                    // "fixed" is a reserved keyword :P
                    var fixd = new StringBuilder();

                    Console.WriteLine("Adjusting accumulative and time column data values, and re-ordering columns");
                    for (int l = 0; l < lines.Count(); l++)
                    {
                        var line = lines[l];

                        // parse line
                        var parsed = line.Split(",".ToCharArray());

                        // fix column order (sigh)
                        {
                            var parsedReordered = new List<string>();

                            foreach (var columnName in originalColumnNames)
                            {
                                var index = Array.IndexOf(newColumnNames, columnName);
                                parsedReordered.Add(parsed[index]);
                            }

                            parsed = parsedReordered.ToArray();
                        }

                        // adjust the accumulative and time data
                        for (int i = 0; i < originalColumnNames.Length; i++)
                        {
                            // any column ending in "[Time]" is handled by default even if not explicitly specified
                            if (timeColumns.Contains(originalColumnNames[i]) || originalColumnNames[i].EndsWith("[Time]"))
                            {
                                // stepSizePerLine is needed because the first line of the next file starts at zero, 
                                // instead of "one tick after the last line in the previous file"
                                parsed[i] = (Double.Parse(parsed[i]) + Double.Parse(timeStepPerLine[i]) + Double.Parse(lastDataLinePreviousFile[i])).ToString();
                            }
                            else if (accumulativeColumns.Contains(originalColumnNames[i]))
                            {
                                parsed[i] = (Double.Parse(parsed[i]) + Double.Parse(lastDataLinePreviousFile[i])).ToString();
                            }

                            // if last column (just a CSV formatting thing)
                            if (i == originalColumnNames.Length - 1)
                            {
                                fixd.Append(parsed[i] + Environment.NewLine);
                            }
                            else
                            {
                                fixd.Append(parsed[i] + ",");
                            }
                        }

                        // if the last line in the file, save for next file, so we can continue calculating the time offsets properly
                        if (l == lines.Count() - 1)
                        {
                            lastDataLinePreviousFile = parsed;
                        }
                    }

                    // dump the entire processed file via StringBuilder.ToString()
                    File.AppendAllText(output, fixd.ToString());
                }

                Console.WriteLine("Finished processing file");
                Console.WriteLine();
            }

            Console.WriteLine("Completed processing all files successfully, output is located at: " + output);
        }
    }
}
