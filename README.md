<div id="top"></div>

<p>
   <a href="https://github.com/SilenZcience/CAudioVisualizer/releases" alt="Github Downloads">
      <img src="https://img.shields.io/github/downloads/SilenZcience/CAudioVisualizer/total?color=blue&label=Github%20Downloads" align="right">
   </a>
   <a href="https://github.com/SilenZcience/CAudioVisualizer" alt="Visitors">
      <img src="https://hitscounter.dev/api/hit?url=https%3A%2F%2Fgithub.com%2FSilenZcience%2FCAudioVisualizer&label=Visitors&icon=person-circle&color=%23479f76" align="right">
   </a>
   <a href="https://github.com/SilenZcience/CAudioVisualizer/tree/main/" alt="CodeSize">
      <img src="https://img.shields.io/github/languages/code-size/SilenZcience/CAudioVisualizer?color=purple&label=Code%20Size" align="right">
   </a>
</p>

[![OS-Windows]][OS-Windows]

<br/>
<div align="center">
<h2 align="center"><b>CAudioVisualizer</b></h2>
   <p align="center">
      Simple Audio Visualizer made in C#
      <br/>
      <a href="https://github.com/SilenZcience/CAudioVisualizer/blob/main/Program.cs">
         <strong>Explore the code »</strong>
      </a>
      <br/>
      <br/>
      <a href="https://github.com/SilenZcience/CAudioVisualizer/issues/new?assignees=&labels=feature&projects=&template=feature_request.yaml">Request Feature</a>
      ·
      <a href="https://github.com/SilenZcience/CAudioVisualizer/issues/new?assignees=&labels=bug&projects=&template=bug_report.yaml&title=%F0%9F%90%9B+Bug+Report%3A+">Report Bug</a>
      ·
      <a href="https://github.com/SilenZcience/CAudioVisualizer/issues/new?assignees=&labels=docs&projects=&template=documentation_request.yaml&title=%F0%9F%93%96+Documentation%3A+">Request Documentation</a>
   </p>
</div>


<details>
   <summary>Table of Contents</summary>
   <ol>
      <li>
         <a href="#about-the-project">About The Project</a>
         <ul>
            <li><a href="#made-with">Made With</a></li>
         </ul>
      </li>
      <li>
         <a href="#getting-started">Getting Started</a>
         <ul>
            <li><a href="#prerequisites">Prerequisites</a></li>
            <li><a href="#installation">Installation</a></li>
         </ul>
      </li>
      <li><a href="#usage">Usage</a>
         <ul>
         <li><a href="#features">Features</a></li>
         <li><a href="#examples">Examples</a></li>
         </ul>
      </li>
      <li><a href="#license">License</a></li>
      <li><a href="#contact">Contact</a></li>
   </ol>
</details>

<div id="about-the-project"></div>

<h2>
	<a href="#">&#x200B;</a>
	<a href="#about-the-project" title="Noto Emoji, licensed under CC BY 4.0">
		<img unselectable="on" pointer-events="none" src="https://fonts.gstatic.com/s/e/notoemoji/latest/1f525/512.gif" width="30" />
	</a>
	<b>About The Project</b>
</h2>

[![GitHub-Last-Commit]](https://github.com/SilenZcience/CAudioVisualizer/commits/main/)
[![Github-Stars]](https://github.com/SilenZcience/CAudioVisualizer/stargazers)
[![Github-Watchers]](https://github.com/SilenZcience/CAudioVisualizer/watchers)
[![Github-Forks]](https://github.com/SilenZcience/CAudioVisualizer/network/members)

---

CAudioVisualizer is a simple audio visualizer built in C#. It provides a graphical representation of audio signals, allowing users to visualize sound in real-time. The project aims to be easy to use and extremely customizable to create a unique aesthetic look.

**Features:**
- Real-time audio waveform and spectrum visualization
- Customizable display options
- Support for multiple screens and resolutions

Feel free to explore the code, contribute, or request new features!

<div id="made-with"></div>

### Made With
[![MadeWith-C#]](https://dotnet.microsoft.com/en-us/download)
[![.Net-Version]](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

**External Libraries:**
- **[NAudio](https://github.com/naudio/NAudio)** - Audio capture & processing
- **[OpenTK](https://github.com/opentk/opentk)** - OpenGL bindings & windowing
- **[ImGui.NET](https://github.com/ImGuiNET/ImGui.NET)** - Immediate mode GUI framework
- **[MathNet.Numerics](https://github.com/mathnet/mathnet-numerics)** - Mathematical computations & FFT



<p align="right">(<a href="#top">↑back to top↑</a>)</p>
<div id="getting-started"></div>

<h2>
	<a href="#">&#x200B;</a>
	<a href="#getting-started" title="Noto Emoji, licensed under CC BY 4.0">
		<img unselectable="on" pointer-events="none" src="https://fonts.gstatic.com/s/e/notoemoji/latest/1f680/512.gif" width="30" />
	</a>
	<b>Getting Started</b>
</h2>

<div id="prerequisites"></div>

### Prerequisites

### Self-Contained Version (win-x64 and win-x86)
- **Operating System**: Windows 10/11 (x64 or x86)
- **Memory**: 500MB RAM minimum
- **Graphics**: OpenGL 3.3+ compatible graphics card
- **Audio**: Windows audio device (speakers/headphones)
- **Disk Space**: ~200MB

### Framework-Dependent Version
- **All above requirements PLUS:**
- **.NET Runtime**: .NET 10.0 Desktop Runtime
  - Download from: https://dotnet.microsoft.com/download/dotnet/10.0

<div id="installation"></div>

### Installation
[![GitHub-Release]](https://github.com/SilenZcience/CAudioVisualizer/releases)
[![GitHub-Release-Date]](https://github.com/SilenZcience/CAudioVisualizer/releases)

you can choose between using the following installation methods:
- download and use CAudioVisualizer.exe for win-x64
- download and use CAudioVisualizer.exe for win-x86
- download and use CAudioVisualizer.exe for win-framework-dependent (needs .Net 10.0 installed)
- download and install CAudioVisualizer using the CAudioVisualizer-Setup.exe as a true windows application.

<div id="download"></div>

Direct Download:
</br>
All executable files can be directly *downloaded* via the [github releases](https://github.com/SilenZcience/CAudioVisualizer/releases).

> [!CAUTION]
> **You should never trust any executable file!** Feel free to compile the application yourself (e.g. using [build-release.ps1](https://github.com/SilenZcience/CAudioVisualizer/blob/main/build-release.ps1)).\
> You can verify the creation of the executable files yourself by reading the [source code](https://github.com/SilenZcience/CAudioVisualizer/blob/main/Program.cs) and validating the [build-process](https://github.com/SilenZcience/CAudioVisualizer/blob/main/build-release.ps1) used.

<p align="right">(<a href="#top">↑back to top↑</a>)</p>
<div id="usage"></div>

<h2>
	<a href="#">&#x200B;</a>
	<a href="#usage" title="Noto Emoji, licensed under CC BY 4.0">
		<img unselectable="on" pointer-events="none" src="https://fonts.gstatic.com/s/e/notoemoji/latest/2699_fe0f/512.gif" width="30" />
	</a>
	<b>Usage</b>
</h2>

Simply run the executable/application. </br>
Use the hotkey `F3` to open the config menu.

<div id="features"></div>

### Features

- **Real-time Audio Visualization**: Multiple visualization modes
- **FFT Support**: Toggle between time-domain and frequency-domain visualizations
- **Customizable**: Extensive configuration options for colors, sizes, positions, and effects
- **Multiple Monitors**: Support for multi-monitor setups
- **Fade Trail Effects**: Beautiful trailing effects for enhanced visualization
- **Configuration Persistence**: Save and load your preferred settings (%appdata%/CAudioVisualizer/)

<div id="examples"></div>

### Examples

<details open>
   <summary><b>📂 Images 📂</b></summary>
   </br>

   <p float="left">
      <img src="https://github.com/SilenZcience/CAudioVisualizer/blob/main/img/example1.gif?raw=true" width="98%"/>
   </p>

   <p float="left">
      <img src="https://github.com/SilenZcience/CAudioVisualizer/blob/main/img/example1.png?raw=true" width="98%"/>
   </p>
   <p float="left">
      <img src="https://github.com/SilenZcience/CAudioVisualizer/blob/main/img/example2.png?raw=true" width="98%"/>
   </p>

</details>
</br>

<p align="right">(<a href="#top">↑back to top↑</a>)</p>
<div id="license"></div>

<h2>
	<a href="#">&#x200B;</a>
	<a href="#license" title="Noto Emoji, licensed under CC BY 4.0">
		<img unselectable="on" pointer-events="none" src="https://fonts.gstatic.com/s/e/notoemoji/latest/2757/512.gif" width="30" />
	</a>
	<b>License</b>
</h2>
<a href="https://github.com/SilenZcience/CAudioVisualizer/blob/main/LICENSE" alt="License">
   <img src="https://img.shields.io/badge/license-MIT-brightgreen" align="right">
</a>

> [!IMPORTANT]
> This software is provided "as is," **without warranty** of any kind. There are **no guarantees** of its functionality or suitability for any purpose. Use at your own risk—**No responsibility** for any issues, damages, or losses that may arise from using this software are taken.

This project is licensed under the MIT License - see the [LICENSE](https://github.com/SilenZcience/CAudioVisualizer/blob/main/LICENSE) file for details

<div id="contact"></div>

<h2>
	<a href="#">&#x200B;</a>
	<a href="#contact" title="Noto Emoji, licensed under CC BY 4.0">
		<img unselectable="on" pointer-events="none" src="https://fonts.gstatic.com/s/e/notoemoji/latest/1f4ab/512.gif" width="30" />
	</a>
	<b>Contact</b>
</h2>

> **SilenZcience** <br/>
[![GitHub-SilenZcience][GitHub-SilenZcience]](https://github.com/SilenZcience)

[OS-Windows]: https://img.shields.io/badge/os-windows-green?label=OS

[GitHub-Last-Commit]: https://img.shields.io/github/last-commit/SilenZcience/CAudioVisualizer/main
[GitHub-Issues]: https://img.shields.io/github/issues/SilenZcience/CAudioVisualizer
[GitHub-Release]: https://img.shields.io/github/v/release/SilenZcience/CAudioVisualizer?label=Github
[GitHub-Release-Date]: https://img.shields.io/github/release-date/SilenZcience/CAudioVisualizer?label=Release%20Date
[Github-Stars]: https://img.shields.io/github/stars/SilenZcience/CAudioVisualizer?style=flat&color=yellow
[Github-Forks]: https://img.shields.io/github/forks/SilenZcience/CAudioVisualizer?style=flat&color=purple
[Github-Watchers]: https://img.shields.io/github/watchers/SilenZcience/CAudioVisualizer?style=flat&color=purple

[MadeWith-C#]: https://img.shields.io/badge/Made%20with-C%23-brightgreen
[.NET-Version]: https://img.shields.io/badge/.NET-10.0-blue
<!-- https://img.shields.io/badge/Python-3.7%20%7C%203.8%20%7C%203.9%20%7C%203.10%20%7C%203.11%20%7C%203.12%20%7C%20pypy--3.7%20%7C%20pypy--3.8%20%7C%20pypy--3.9%20%7C%20pypy--3.10-blue -->

[GitHub-SilenZcience]: https://img.shields.io/badge/GitHub-SilenZcience-orange
