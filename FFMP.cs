namespace FFMP;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

            // Split args into application and FFmpeg arguments
            var appArgs = args.TakeWhile(arg => arg != "--").ToArray();
            var ffmpegArgs = args.SkipWhile(arg => arg != "--").Skip(1).ToArray();

            if (!appArgs.Any())
            {
                Console.WriteLine("Error: No arguments provided. Please specify required arguments.");
                Environment.Exit(1);
            }

            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("Terminating processes...");
                foreach (var process in TrackedProcesses.ToList())
                {
                    try
                    {
                        if (process != null && !process.HasExited)
                        {
                            process.Kill();
                            Console.WriteLine($"Terminated FFmpeg process with ID {process.Id}");
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        Console.WriteLine($"Process already terminated or invalid: {ex.Message}");
                    }
                }
                
                // Additional fix for Windows: Kill all FFmpeg processes by name
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    try
                    {
                        var ffmpegProcesses = Process.GetProcessesByName("ffmpeg");
                        foreach (var ffmpegProcess in ffmpegProcesses)
                        {
                            try
                            {
                                ffmpegProcess.Kill();
                                Console.WriteLine($"Force-killed FFmpeg process with ID {ffmpegProcess.Id}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error killing FFmpeg process {ffmpegProcess.Id}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error retrieving FFmpeg processes: {ex.Message}");
                    }
                }

                e.Cancel = true;
                Environment.Exit(0);
            };

            Parser.Default.ParseArguments<Options>(appArgs)
                .WithParsed(options =>
                {
                    if (ValidateOptions(options))
                    {
                        Run(options, ffmpegArgs).Wait();
                    }
                    else
                    {
                        Environment.Exit(1);
                    }
                })
                .WithNotParsed(errors =>
                {
                    // Check if the user requested help/version information
                    if (errors.Any(error => error is CommandLine.HelpRequestedError || error is CommandLine.VersionRequestedError))
                    {
                        Environment.Exit(0); // Exit without error message
                    }

                    // Otherwise, print an actual error message
                    Console.WriteLine("Error: Invalid arguments provided.");

                    Environment.Exit(1);
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static readonly ConcurrentBag<Process> TrackedProcesses = new ConcurrentBag<Process>();

    static async Task Run(Options options, string[] ffmpegArgs)
    {
        try
        {
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

                        // Call the ProcessFile method
                        await ProcessFile(inputFile, options, ffmpegArgs, progress, inputFiles.Count);
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
                var excludedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // Non-Media file filter
                    ".txt",
                    ".doc",
                    ".docx",
                    ".xls",
                    ".xlsx",
                    ".csv",
                    ".json",
                    ".xml",
                    ".html",
                    ".htm",
                    ".exe",
                    ".dll",
                    ".bat",
                    ".cmd",
                    ".zip",
                    ".rar",
                    ".7z",
                    ".tar",
                    ".gz",
                    ".iso",
                    ".bin",
                    ".log",
                    ".ini",
                    ".cfg",
                    ".tmp"
                };

                return Directory
                    .EnumerateFiles(Path.GetFullPath(options.InputDirectory), "*.*", SearchOption.AllDirectories)
                    .Where(file => !excludedExtensions.Contains(Path.GetExtension(file)));
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

    static async Task ProcessFile(string inputFile, Options options, string[] ffmpegArgs, ProgressBar progress,
        int totalFiles)
    {
        string outputFile = GenerateOutputFilePath(inputFile, options.OutputPattern);

        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

        if (File.Exists(outputFile) && !options.Overwrite)
        {
            Console.WriteLine($"Skipping {inputFile}, output already exists.");
            return;
        }

        // Build FFmpeg arguments
        var arguments = options.Overwrite ? "-y " : "";
        arguments += $"-i \"{Path.GetFullPath(inputFile)}\" \"{outputFile}\"";

        if (ffmpegArgs.Any())
        {
            arguments += $" {string.Join(" ", ffmpegArgs)}";
        }

        if (options.Verbose)
        {
            arguments = $"-loglevel verbose {arguments}";
        }
        else
        {
            arguments = $"-loglevel error {arguments}";
        }

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
            
            // Capture and display output in verbose mode
            if (options.Verbose)
            {
                var outputTask = Task.Run(() =>
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        Console.WriteLine(process.StandardOutput.ReadLine());
                    }
                });

                var errorTask = Task.Run(() =>
                {
                    while (!process.StandardError.EndOfStream)
                    {
                        Console.WriteLine(process.StandardError.ReadLine());
                    }
                });

                await Task.WhenAll(outputTask, errorTask);
            }
            else
            {
                // Wait silently for process to finish
                await process.WaitForExitAsync();
            }

            if (process.ExitCode == 0)
            {
                Console.WriteLine($"FFmpeg process completed successfully for file: {inputFile}");
                progress.Report(1.0 / totalFiles);
            }
            else
            {
                Console.WriteLine($"FFmpeg process exited with code {process.ExitCode} for file: {inputFile}");
            }
            
            await process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing FFmpeg for file {inputFile}: {ex.Message}");
        }
    }

    static string GenerateOutputFilePath(string inputFile, string pattern)
    {
        var directory = Path.GetDirectoryName(inputFile);
        var fileName = Path.GetFileNameWithoutExtension(inputFile);
        var extension = Path.GetExtension(inputFile);

        var outputPath = pattern.Replace("{{dir}}", directory)
            .Replace("{{name}}", fileName)
            .Replace("{{ext}}", extension);

        // Ensure the output path has a directory; default to input file's directory
        if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(outputPath)))
        {
            outputPath = Path.Combine(directory!, outputPath);
        }

        return outputPath;
    }

    private static bool ValidateOptions(Options options)
    {
        bool isValid = true;

        if (options.Convert)
        {
            // Ensure required fields for conversion
            if (string.IsNullOrWhiteSpace(options.OutputPattern))
            {
                Console.WriteLine("Error: The 'output-pattern' argument is required when using '--convert'.");
                isValid = false;
            }
        }
        else
        {
            // Ensure required fields for transcoding
            if (string.IsNullOrWhiteSpace(options.Codec))
            {
                Console.WriteLine("Error: The 'codec' argument is required.");
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(options.OutputPattern))
            {
                Console.WriteLine("Error: The 'output-pattern' argument is required.");
                isValid = false;
            }
        }

        // Common validation for both modes
        if (string.IsNullOrWhiteSpace(options.InputDirectory) && string.IsNullOrWhiteSpace(options.InputFile))
        {
            Console.WriteLine("Error: Either 'directory' or 'file' argument must be provided for input.");
            isValid = false;
        }

        if (!string.IsNullOrWhiteSpace(options.InputFile) && !File.Exists(options.InputFile))
        {
            Console.WriteLine($"Error: Specified input file '{options.InputFile}' does not exist.");
            isValid = false;
        }

        if (!string.IsNullOrWhiteSpace(options.InputDirectory) && !Directory.Exists(options.InputDirectory))
        {
            Console.WriteLine($"Error: Specified input directory '{options.InputDirectory}' does not exist.");
            isValid = false;
        }

        return isValid;
    }
}