<p align="center"><img src="https://github.com/user-attachments/assets/7fcc4750-2f59-46ef-8b85-1d2f5fee2b0a" width="450"></p>


<p align="center">
  <a href="https://github.com/IridiumIO/PolyCut/releases">
    <img alt="GitHub Downloads (all assets, all releases)" src="https://img.shields.io/github/downloads/IridiumIO/Polycut/total?style=for-the-badge&logo=github">
    <img alt="GitHub Release" src="https://img.shields.io/github/v/release/IridiumIO/Polycut?style=for-the-badge">
  </a>
  </br> 
</p>

<p align="center"><b>Use your 3D Printer as a plotter / vinyl cutter. If you've already got a 3D Printer, you shouldn't need to buy a separate Cricut or Silhouette machine.</b></p> 

<p align="center">Polycut is a tool designed to import SVG files and convert them to 2D GCode to run on 3D Printers, CNCs or any other Gcode machines that have blades/pens/knives/foil tools attached. It also directly supports uploading to a networked 3D Printer via Moonraker/Klipper. 
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

<p align="center">
<img alt="Static Badge" src="https://img.shields.io/badge/DOWNLOAD-steelblue?style=for-the-badge&logo=github&link=https%3A%2F%2Fgithub.com%2FIridiumIO%2FPolyCut%2Freleases">
</p>

# Features

### Drawing Canvas:
- Import multiple SVGs, arrange and scale them on the canvas
    - SVG groups / layers are preserved on import, including clipped geometries
- While I strongly recommend using `Inkscape` to design your SVGs exactly as you want and then use Polycut as a machine path generator, basic transformation support is available in Polycut.
    - Copy/Cut/Paste support
    - Boolean operations (Union, Subtract, Intersect, Exclude)
    - Mirror/Flip objects (handy for using heat-transfer vinyl)
    - Editing Stroke/Fill colour
    - Resize/rotate/move
 - Basic shapes (line, ellipse, rectangle, path) can be drawn directly on the canvas

   
### Tool Modes:
- **Cutting mode** - Generates optimised outline paths for a drag knife or cutter (e.g. Roland Vinyl Cutter, or cricut/silhouette blades).
    - Configurable swivel offsets that account for the blade diameter to ensure sharp corners remain sharp
    - Tracks the blade orientation when moving between cut lines to optimise and avoid tearing / scratching
- **Drawing mode** - generate paths and fills using a variety of fill patterns:
    - Hatch, Crosshatch, Spiral, Triangular Hatch, Diamond Crosshatch, and Radial fills
- **Multipass** — repeat cutting or drawing passes N times, stepping down in Z between each pass; useful for thicker materials that need multiple light passes rather than a single deep cut
- **Foiling / Engraving / Embossing / Etching** - Each can easily be done using configurable settings of the above modes 

### Saved Projects:
- Save and reload working projects to/from disk, preserving all canvas shapes and properties.
- Allows exporting the canvas to SVG as well

### Printer/Machine Configuration:
- Add and manage multiple printers (or any GCode supporting machine really) with independent profiles
- Per-printer **Custom Start / End GCode**
- Per-printer **Tool X / Y Offsets** to compensate for toolhead mounting offsets
- **Klipper bounding box preview** - Send a dry-run rectangle pass to the Klipper before actually cutting, so you can confirm the material is aligned properly before actually cutting/drawing

### Preview
- A full 2D animated render of toolpaths including travel/active line discrimination, showing the order processing will occur
- Detailed controls for **Pause**, **Resume**, **Step Forward**, **Step Back** in the preview animation
- GCode preview shows estimated time and total drawing/cutting length (also exported to Klipper if you use it) 

### Export
- Save to GCode file, or
- Send to a networked 3D printer using Klipper/Moonraker
  - Option to auto-start running the file after upload
  - **Klipper Bounding Box Preview** export — runs a travel-only rectangle so you can verify alignment before committing to a cut
 
### Monitor
- Simply takes the provided URL from the export tab and renders the webpage; handy for monitoring Klipper from within the app rather than opening a separate browser

### Generators:
There are two generators currently included with Polycut; `Polycut.Core` and `GCodePlot`
- **Polycut.Core**: Created for Polycut from the ground up, and incorporates a lot of performance and quality tweaks. 
- **GCodePlot**: Created by @arpruss, with a few tweaks by myself that haven't made it into the base repository yet. This is a more tried-and-tested generator with more consistent results; initially this was a superior processor, but over the past few months `Polycut.Core` has become a lot more capable with more supported features. GCodePlot remains for those who simply prefer it :) 
  - Note: GCodePlot doesn't support the spiral/radial/diamond/triangle fill patterns. It also cannot process SVG text elements or clipped paths directly. 


&nbsp;

# Requirements
- Windows 10 v1809 or higher (Windows 11 required for Mica effects).
    - Technically it could work as far back as Windows 7 but I haven't tested it.  

&nbsp;

# Additional Screenshots

<p>
   <img src="https://github.com/IridiumIO/PolyCut/assets/1491536/77c7abfc-bef7-4a34-a558-ab1c78c9ff5f" width="1000"/>
  <img src="https://github.com/IridiumIO/PolyCut/assets/1491536/cb2b29de-e527-42ba-9b35-a3a55a4881b2" width="1000"/>
</p>

# Background
I have a 3D printer. I wanted to get into bookbinding, which utilises a lot of vinyl designs that typically require a Cricut, Silhouette or similar vinyl cutter that costs as much as a 3D printer. A 3D printer is already a perfectly good 3-axis system, capable of <200 micron cutting/drawing precision.
General solutions do exist for creating GCode from SVG files already - You can convert SVGs to GCode from within Cura, but it doesn't account for the diameter of a swivel blade, and thus corners are never crisp; Inkscape has its own inbuilt GCodeTools but it is extremely kludgy; InkCut looks to be nice, but it refuses to run on my PC. 

GCodePlot by @arpruss is an excellent extension to Inkscape - by far the best I found (and in fact, you can use it from within Polycut) but on its own, it isn't quite *smooth* enough. You have to chop up a 12" cutting mat to fit on a standard 3d printer bed, and you never quite know where to line everything up. First I created a [template](https://github.com/IridiumIO/PolyCut/assets/1491536/dd7d9973-3343-4935-85e9-bdc71f112550) for Inkscape that had a [pre-chopped cutting mat in it](https://github.com/IridiumIO/PolyCut/assets/1491536/623fe8d8-3cfd-4ae9-a5e2-e2841f8a1561). Then modified GCodePlot to allow exporting from Inkscape's export menu, added support for ignoring hidden/locked layers, and added Moonraker upload support. That should have been enough for me.

But then I got ambitious...


### Tutorial on setting up Klipper to quickly swap between 3D printing and non-printing modes 
[Klipper Setup.md](https://github.com/IridiumIO/PolyCut/blob/master/Klipper%20Setup.md#klipper-setup)

### 3D-printable mount for holding swivel blade/pens
If you have an Ender 3 S1 or other printer that can take [this hotswap mount](https://properprinting.pro/product/creality-ender3s1-simpletoolchanger/), then you can [get my current vinyl cutter holder here](https://www.printables.com/model/741765). 

Otherwise, you'll find vinyl cutters on Printables/Thingiverse. I *strongly* recommend using one that has a spring in it, because a 3D printer bed is nowhere near level enough for the accuracy needed to consistently cut through vinyl. A spring will allow a bit of flexibility and pressure to keep the blade in contact with the cutting mat. 

# Issues and Planned Features
[See the document here](https://github.com/IridiumIO/PolyCut/blob/master/PolyCut.Core/Issues.md#issues)

 -----
 ### Like this project?
 Please consider leaving a tip on Ko-Fi :) 
 
 <p align="center"><a href='https://ko-fi.com/iridiumio' target='_blank'><img height='42' style='border:0px;height:42px;' src='https://cdn.ko-fi.com/cdn/kofi3.png?v=3' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a></p>
