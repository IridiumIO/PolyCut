<p align="center"><img align="center" src="PolyCut/Resources/banner_dark.svg" height="220" /></p>
<p align="center">
    <img alt="GitHub Downloads (all assets, all releases)" src="https://img.shields.io/github/downloads/IridiumIO/Polycut/total?style=for-the-badge&logo=github"> <a href="https://github.com/IridiumIO/PolyCut/releases"></img>
    <img alt="GitHub Release" src="https://img.shields.io/github/v/release/IridiumIO/Polycut?style=for-the-badge"> <a href="https://github.com/IridiumIO/PolyCut/releases"></img>
  </br> 
</p>

<p align="center"><b>Turn your 3D Printer into a vinyl cutter, pen plotter, foil machine or engraving tool. </br>If you've already got a 3D Printer, you shouldn't need to buy a separate Cricut machine.</b></p> 

<p align="center">Convert artwork into optimized 2D GCode with drag-knife compensation, drawing fills, multi-pass engraving and more.  Polycut also directly supports uploading to a networked 3D Printer via Moonraker/Klipper. 
</p> 

&nbsp;

<p align="center">
  <img alt="NewMainUI" src="https://github.com/user-attachments/assets/92d81be3-7d70-44cd-9348-eac888ac1367" width="800"/>
  </br> 
</p>

<p align="center">
  <img alt="NewPreviewUI" src="https://github.com/user-attachments/assets/3bf80186-79a1-4115-bee4-d01ea2e7768c" width="800"/>
  </br> 
</p>


&nbsp;

# Installation
Download the latest version from Github Releases. 
<p align="center">
<img alt="Static Badge" src="https://img.shields.io/badge/DOWNLOAD-steelblue?style=for-the-badge&logo=github&link=https%3A%2F%2Fgithub.com%2FIridiumIO%2FPolyCut%2Freleases">
</p>

The application itself is portable (no installation required!). Configuration data is saved in `%LocalAppData%/IridiumIO/PolyCut` by default, but placing the application in any folder called `PolyCut` will make it use that for it's config data, allowing it to be truly portable


# Features

### Drawing Canvas:
- Import arrange and scale multiple SVGs
    - SVG groups, layers and clipped geometries are preserved.
- Raster Importer and Vectoriser
    - Import regular PNG/JPG/BMP files and trace them easily into vector art for plotting/cutting, thanks to the VTracer library. 
- Basic editing tools
    - Copy/Cut/Paste
    - Boolean operations (Union, Subtract, Intersect, Exclude)
    - Mirror/Flip, Move, Rotate, Resize
    - Stroke and Fill colour editing.
    - Layer reordering
 - Draw basic shapes directly (line, ellipse, rectangle, path) as well as text.
 - Save and reload projects, or export the canvas back to SVG for use elsewhere

> [!NOTE]
> PolyCut is intended to complement tools like Inkscape, not replace them. I strongly recommend designing your artwork in Inkscape, and using Polycut for layout, basic transformations and GCode generation. An Inkscape extension for rapid export is coming soon.


### Tool Modes:
- **Cutting** - Optimised drag knife toolpaths with configurable swivel offsets and blade orientation tracking for crisp corners and tearing prevention.
- **Drawing** - Outline drawing +/- Hatch, Crosshatch, Spiral, Triangular, Diamond, Radial and Contour fill patterns
- **Multipass** - Repeat passes with configurable Z step-down
- **Foiling, Engraving, Embossing and Etching** — Configurable using the above tool modes

### Printer/Machine Configuration:
- Multiple machine profiles
- Custom Start / End GCode
- Tool X / Y Offsets to compensate for mounting offsets
- Klipper bounding box preview - Sends a dry-run to Klipper for material alignment.

### Preview & Export
- Animated 2D toolpath preview with travel moves, execution order and playback controls
- GCode previews show estimated time and total toolpath length (Metric only. Sorry Americans)
- Export GCode or upload directly to Moonraker/Klipper with optional auto-start after upload, and a built-in Klipper web interface. 


&nbsp;

# Requirements
### Operating System
- Windows 10 v1809 or higher (Windows 11 required for Mica effects).
- Linux - requires `WINE`. Some features are disabled for compatibility (In-built browser, Eyedropper tool and transparency effects)

### 3D-printable mount for holding swivel blade/pens
If you have an Ender 3 S1 or other printer that can take [this hotswap mount](https://properprinting.pro/product/creality-ender3s1-simpletoolchanger/), then you can [get my current vinyl cutter holder here](https://www.printables.com/model/741765). 

Otherwise, you'll find vinyl cutters on Printables/Thingiverse. I *strongly* recommend using one that has a spring in it, because a 3D printer bed is nowhere near level enough for the accuracy needed to consistently cut through vinyl. A spring will allow a bit of flexibility and pressure to keep the blade in contact with the cutting mat. 

### (Optional) Tutorial on setting up Klipper to quickly swap between 3D printing and non-printing modes 
[Klipper Setup.md](https://github.com/IridiumIO/PolyCut/blob/master/Klipper%20Setup.md#klipper-setup)


&nbsp;

# Background
Like many makers, I own a 3D printer. When I started getting into bookbinding and other paper crafts, I quickly discovered that many projects rely on vinyl cutters such as a Cricut or Silhouette. Spending hundreds of dollars on a machine that is essentially another 3-axis motion system didn't make much sense to me. 

A semi-modern 3D printer is already capable of extremely precise movements, often with positional accuracy much lower than 200 microns. With the right tool attached, it can perform many of the same jobs as a dedicated Cricut or Silhouette machine.
The problem was, software to achieve this was kind of rubbish. 

There are several ways to generate GCode from SVG files, but none of them quite fit what I wanted. Cura can convert SVGs into toolpaths, but it doesn't compensate for a drag knife's swivel radius, resulting in corners that are never sharp. Inkscape's built-in GCode tools are powerful but extremely klunky. Other available tools either flatly refused to work or didn't have the features needed. 

One project stood out: GCodePlot is an Inkscape extension by @arpruss. It produced excellent toolpaths and became the foundation for many of my early experiments. I initially modified it directly, created a [template](https://github.com/IridiumIO/PolyCut/assets/1491536/dd7d9973-3343-4935-85e9-bdc71f112550) for Inkscape that had a [pre-chopped cutting mat in it](https://github.com/IridiumIO/PolyCut/assets/1491536/623fe8d8-3cfd-4ae9-a5e2-e2841f8a1561). Then started adding features such as Moonraker uploads, exporting directly from Inkscape, and support for ignoring hidden or locked layers. But on it's own, it never quite felt... *smooth* enough. 

Then I got ambitious... and PolyCut is the result. 

 -----
 ### Like this project?
 Please consider leaving a tip on Ko-Fi :) 
 
 <p align="center"><a href='https://ko-fi.com/iridiumio' target='_blank'><img height='42' style='border:0px;height:42px;' src='https://cdn.ko-fi.com/cdn/kofi3.png?v=3' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a></p>
