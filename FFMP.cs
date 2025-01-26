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

            Console.WriteLine("Arguments received:");
            foreach (var arg in args)
            {
                Console.WriteLine(arg);
            }

            Console.CancelKeyPress += (sender, e) => {
                Console.WriteLine("Terminating processes...");
                Process.GetProcessesByName("ffmpeg").ToList().ForEach(p => p.Kill());
                Environment.Exit(0);
            };

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

            foreach (var inputFile in inputFiles)
            {
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        Console.WriteLine($"Processing file: {inputFile}");
                        await ProcessFile(inputFile, options, progress, inputFiles.Count);
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
                return Directory.EnumerateFiles(Path.GetFullPath(options.InputDirectory), "*.*")
                    .Where(file => new[] { ".mp4", ".mkv", ".avi" }.Contains(Path.GetExtension(file).ToLower()));
            }

            if (!string.IsNullOrEmpty(options.InputFile) && File.Exists(options.InputFile))
            {
                return File.ReadLines(Path.GetFullPath(options.InputFile));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading input files: {ex.Message}");
        }

        return Enumerable.Empty<string>();
    }

    static async Task ProcessFile(string inputFile, Options options, ProgressBar progress, int totalFiles)
    {
        var outputFile = GenerateOutputFilePath(inputFile, options.OutputPattern);

        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

        if (File.Exists(outputFile) && !options.Overwrite)
        {
            Console.WriteLine($"Skipping {inputFile}, output already exists.");
            return;
        }

        var ffmpegOptions = options.FFmpegOptions.TrimStart('=');

        inputFile = $"\"{Path.GetFullPath(inputFile)}\"";
        outputFile = $"\"{Path.GetFullPath(outputFile)}\"";

        var arguments = $"-i {inputFile} -c:v {options.Codec}";
        if (!string.IsNullOrEmpty(options.Preset))
        {
            arguments += $" -preset {options.Preset}";
        }
        arguments += $" {outputFile}";

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
                RedirectStandardInput = true, // Added to allow input redirection
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var errorOutput = new StringBuilder();
        try
        {
            process.Start();

            // Write 'Y' to the StandardInput stream to confirm overwrite
            if (options.Overwrite)
            {
                using (var writer = process.StandardInput)
                {
                    writer.WriteLine("Y");
                }
            }

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorOutput.AppendLine(e.Data);
                    if (options.Verbose)
                        Console.Error.WriteLine(e.Data);
                }
            };

            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"FFmpeg process exited with code: {process.ExitCode}");
                Console.WriteLine("FFmpeg encountered an error:");
                Console.WriteLine(errorOutput.ToString());
            }
            else
            {
                Console.WriteLine($"FFmpeg process completed successfully for file: {inputFile}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing FFmpeg: {ex.Message}");
        }
        finally
        {
            progress.Report(1.0 / totalFiles);
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
