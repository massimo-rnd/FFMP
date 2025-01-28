namespace FFMP;

using CommandLine;

public class Options
{
    [Option('t', "threads", Default = 2, HelpText = "Number of threads to use. Default is 2.")]
    public int ThreadCount { get; set; }

    [Option("codec", Required = true, HelpText = "Codec to use for video processing, e.g., 'libx265'.")]
    public string Codec { get; set; } = string.Empty;

    [Option("preset", Required = false, HelpText = "Preset to use for FFmpeg encoding, e.g., 'fast'.")]
    public string Preset { get; set; } = string.Empty;
    public string FFmpegOptions { get; set; } = string.Empty;

    [Option('d', "directory", HelpText = "Directory containing files to process.")]
    public string? InputDirectory { get; set; }

    [Option('f', "file", HelpText = "Path to a file containing a list of input files.")]
    public string? InputFile { get; set; }

    [Option("overwrite", Default = false, HelpText = "Overwrite files if they already exist.")]
    public bool Overwrite { get; set; }

    [Option("verbose", Default = false, HelpText = "Enable verbose logging.")]
    public bool Verbose { get; set; }

    [Option("delete", Default = false, HelpText = "Delete the source file after processing.")]
    public bool DeleteSource { get; set; }

    [Option("output-pattern", Required = true, HelpText = "Pattern for output file paths. Use {{name}}, {{ext}}, and {{dir}} placeholders.")]
    public string OutputPattern { get; set; } = string.Empty;
    
    [Value(0, Required = false, HelpText = "Arguments to pass directly to FFmpeg after '--'.")]
    public IEnumerable<string> FFmpegArguments { get; set; } = Enumerable.Empty<string>();

}