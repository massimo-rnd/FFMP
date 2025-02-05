<div align="center">

# FFMP - FFMPEG Multi-Processing

![License](https://img.shields.io/github/license/massimo-rnd/FFMP)
![Issues](https://img.shields.io/github/issues/massimo-rnd/FFMP)
![Forks](https://img.shields.io/github/forks/massimo-rnd/FFMP)
![Stars](https://img.shields.io/github/stars/massimo-rnd/FFMP)
![Last Commit](https://img.shields.io/github/last-commit/massimo-rnd/FFMP)
![GitHub release (latest by date including pre-releases)](https://img.shields.io/github/v/release/massimo-rnd/FFMP?include_prereleases)
![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/massimo-rnd/FFMP/total?label=Total%20Users)

</div>

## 🚀 Overview

A multithreaded C# CLI for digital media processing using FFMPEG. Transcode as many files in parallel as your system can handle.

## 🎯 Features

- Processing Multiple video files at once
- As many concurrent processes as your system can handle

## ℹ️ Requirements
- .NET 8.0 Runtime

## 🛠️ Installation

1. Go to the [Releases](https://github.com/massimo-rnd/FFMP/releases/latest) page and Download the latest Version for your OS
2. Open a Terminal/CMD Window

## 💻 Usage

### General Usage

FFMP's usage is almost identical to FFMPEG, consider this simple example:

```bash
dotnet path/to/FFMP.dll --codec libx265 --preset fast -d "/path/to/input/files" --output-pattern "/path/to/output/files/{{name}}_compressed{{ext}}" --threads 2
```
Using FFMP for a single file doesn't really make sense. Use this tool if you have a folder of videos you want to process.

You can also use a text-file with paths to video files as an input like this:

```bash
dotnet path/to/FFMP.dll --codec libx265 --preset fast -t "/path/to/videos.txt" --output-pattern "/path/to/output/files/{{name}}_compressed{{ext}}" --threads 2
```

You can adjust your codec by using any codec that is installed on your system behind the `--codec` parameter. Same goes for the preset in `--preset`.

If you need to pass any other arguments to FFMPEG (like audio codecs, video bitrate, subtitle processing, etc.) you can do it like this:

### Advanced Arguments for FFMPEG

```bash
dotnet path/to/FFMP.dll --codec libx265 --preset fast -t "/path/to/videos.txt" --output-pattern "/path/to/output/files/{{name}}_compressed{{ext}}" --threads 2 -- -crf 22 -pix_fmt yuv420p10le -c:a libopus -b:a 320k -c:s copy
```
Everything behind the `--` indicator is passed directly to FFMPEG.

### Mass-Converting Files

Introduced in Version 1.3.0, FFMP now features "Mass-Converting" Files. This takes advantage of everything FFMP already offers and enables Mass-Converting files from one format to another.
Not only can you provide directories or txt-files as sources, multiple videos are converted in parallel.

**Converting using a directory**

```bash
dotnet path/to/FFMP.dll --convert -d "/path/to/videos/directory" --output-pattern "/path/to/output/files/{{name}}_compressed.mkv"
```

**Converting using a txt-file**
```bash
dotnet path/to/FFMP.dll --convert -d "/path/to/videos.txt" --output-pattern "/path/to/output/files/{{name}}_compressed.mkv"
```

If you want to see all of FFMPEGs output, just use the `--verbose` flag.

For more ffmpeg options, visit [ffmpeg's documentation](https://ffmpeg.org/ffmpeg.html).

## 🤝 Contributing

Contributions, issues, and feature requests are welcome!  
Feel free to check the [issues page](https://github.com/massimo-rnd/FFMP/issues).

1. Fork the project.
2. Create your feature branch (`git checkout -b feature/AmazingFeature`).
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`).
4. Push to the branch (`git push origin feature/AmazingFeature`).
5. Open a pull request.

## 📜 License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## 📊 Repository Metrics

![Repo Size](https://img.shields.io/github/repo-size/massimo-rnd/FFMP)
![Contributors](https://img.shields.io/github/contributors/massimo-rnd/FFMP)
![Commit Activity](https://img.shields.io/github/commit-activity/m/massimo-rnd/FFMP)

---

### 🌟 Acknowledgments

- [Inspiration for this Project comes from CodeF0x](https://github.com/CodeF0x)
- [FFZAP - The same thing just based on RUST](https://github.com/CodeF0x/ffzap)

---

### 📞 Contact

For any inquiries, feel free to reach out:
- email: [hi@massimo.gg](mailto:hi@massimo.gg)
- X: [massimo-rnd](https://x.com/massimo-rnd)
- [Discord](https://discord.gg/wmC5AA6c)