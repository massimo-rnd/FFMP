namespace FFMP;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;

class FFMP
{
    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("Starting application...");

            // Output raw arguments for debugging
            Console.WriteLine("Arguments received:");
            foreach (var arg in args)
            {
                Console.WriteLine(arg);
            }

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(options => Run(options).Wait())
                .WithNotParsed(errors => {
                    Console.WriteLine("Failed to parse arguments.");
                    foreach (var error in errors)
                    {
                        Console.WriteLine(error.ToString());
                    }
                    Environment.Exit(1);
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    static async Task Run(Options options)
    {
        try
        {
            Console.WriteLine($"Input Directory: {options.InputDirectory}");
            Console.WriteLine($"FFmpeg Options: {options.FFmpegOptions}");
            Console.WriteLine($"Output Pattern: {options.OutputPattern}");
            Console.WriteLine($"Thread Count: {options.ThreadCount}");

            var inputFiles = GetInputFiles(options)?.ToList();

            if (inputFiles == null || !inputFiles.Any())
            {
                Console.WriteLine("No input files found.");
                return;
            }

            Console.WriteLine($"Found {inputFiles.Count} files to process:");
            foreach (var file in inputFiles)
            {
                Console.WriteLine(file);
            }

            var progress = new ProgressBar(inputFiles.Count);
            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(options.ThreadCount);

            var progressLines = new ConcurrentDictionary<string, int>();
            var currentLine = 0;

            foreach (var inputFile in inputFiles)
            {
                progressLines.TryAdd(inputFile, currentLine++);
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        Console.WriteLine($"Processing file: {inputFile}");
                        await ProcessFile(inputFile, options, progress, progressLines);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing file {inputFile}: {ex.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            progress.Dispose();
            Console.WriteLine("Processing complete.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static IEnumerable<string> GetInputFiles(Options options)
    {
        try
        {
            if (!string.IsNullOrEmpty(options.InputDirectory))
            {
                return Directory.EnumerateFiles(options.InputDirectory, "*.*")
                    .Where(file => new[] { ".mp4", ".mkv", ".avi" }.Contains(Path.GetExtension(file).ToLower()));
            }

            if (!string.IsNullOrEmpty(options.InputFile) && File.Exists(options.InputFile))
            {
                return File.ReadLines(options.InputFile);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading input files: {ex.Message}");
        }

        return Enumerable.Empty<string>();
    }

    static async Task ProcessFile(string inputFile, Options options, ProgressBar progress, ConcurrentDictionary<string, int> progressLines)
    {
        var outputFile = GenerateOutputFilePath(inputFile, options.OutputPattern);

        if (File.Exists(outputFile) && !options.Overwrite)
        {
            Console.WriteLine($"Skipping {inputFile}, output already exists.");
            return;
        }

        var ffmpegOptions = options.FFmpegOptions.TrimStart('=');
        
        var arguments = $"-i \"{inputFile}\" -c:v {options.Codec}";
        if (!string.IsNullOrEmpty(options.Preset))
        {
            arguments += $" -preset {options.Preset}";
        }
        arguments += $" \"{outputFile}\"";

        if (!options.Verbose)
        {
            arguments = $"-loglevel error {arguments}";
        }

        Console.WriteLine($"Executing FFmpeg command: ffmpeg {arguments}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();

            var lineIndex = progressLines[inputFile];

            if (options.Verbose)
            {
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        lock (progressLines)
                        {
                            Console.SetCursorPosition(0, lineIndex);
                            Console.WriteLine(e.Data.PadRight(Console.WindowWidth));
                        }
                    }
                };
                process.BeginErrorReadLine();
            }

            await process.WaitForExitAsync();

            // Ensure process streams are flushed and closed
            process.CancelOutputRead();
            process.CancelErrorRead();

            Console.WriteLine($"FFmpeg process exited with code: {process.ExitCode}");

            if (!options.Verbose)
            {
                lock (progress)
                {
                    progress.Report(1.0 / options.ThreadCount);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing FFmpeg: {ex.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    static string GenerateOutputFilePath(string inputFile, string pattern)
    {
        var directory = Path.GetDirectoryName(inputFile);
        var fileName = Path.GetFileNameWithoutExtension(inputFile);
        var extension = Path.GetExtension(inputFile);

        return pattern.Replace("{{dir}}", directory)
                      .Replace("{{name}}", fileName)
                      .Replace("{{ext}}", extension);
    }
}

