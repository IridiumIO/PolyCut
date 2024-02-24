<p align="center"><img src="https://raw.githubusercontent.com/IridiumIO/PolyCut/ac993f8824416a01a3d201cd3b27ee47d56aceee/PolyCut/Resources/banner_light.svg" width="450"></p>

<p align="center"><b>If you've already got a 3D Printer (or any machine that can process GCode), you don't need to buy a separate Cricut or Silhouette machine.</b></p> 

<p align="center">Polycut is yet another tool designed to import SVG files and convert them to 2D GCode to run on 3D Printers that have blades/pens/knives attached. But it looks nicer. It also directly supports uploading and monitoring the print via Klipper. 
</p> 

&nbsp;

<p align="center">
  <img src="https://github.com/IridiumIO/PolyCut/assets/1491536/4e607626-e387-4719-8142-c19fb7bac342" width="1000"/>
  </br> 
</p>

&nbsp;



# Features

### Drawing Canvas:
- Import multiple SVGs, arrange and scale them (currently you need to have them grouped how you want in advance as groups can't be separated yet) with a resizable/rotatable cutting mat to help line things up
    - (Technically) support for any size 3D printer bed since all we really need is the bed dimensions; right now the default is named `Ender 3 S1` at 235x235mm but you can adjust and save the dimensions; proper adding/editing names is planned.
- Draw basic shapes (line, ellipse, rectangle) and text elements directly onto the canvas (best used for creating cutout lines for easier weeding of vinyl)

### Tool Modes:
- Cutting mode - generates outline paths for a drag knife or cutter (e.g. Roland Vinyl Cutter); importantly, has configurable swivels at sharp corners to account for the blade diameter
- Drawing mode - generate paths and fills in a hatch / crosshatch pattern
- (More modes planned)

### Generators:
There are two generators currently included with Polycut; `Polycut.Core` and `GCodePlot`
- **Polycut.Core**: Created for Polycut, but still experimental. It supports cutting and drawing, and most importantly, it can process text elements without having to convert them to paths first. However it has a few drawbacks for now:
    - Even if a shape does not have a fill in the SVG, it will still be filled
    - Complex shapes can cause problems with cutting/drawing accuracy.
- **GCodePlot**: Created by @arpruss, with a few tweaks by myself that haven't made it into the base repository yet. This is a more tried-and-tested generator with more consistent results.
    -  but importantly it **cannot** handle text elements that haven't been converted to paths first. 
There are other features that are supported by one or the other; these are appropriately enabled/disabled when you switch between the two.

### Preview
- 2D rendering of toolpaths including a preview mode that renders the lines in the order the 3D printer will process them (at 20x speed)
- Note: This gets laggy **very** quickly if you have intricate designs or you set the precision too high
- GCode preview also shows estimated time and total drawing/cutting length. These details are also visible to Klipper

### Export
- Save to GCode file, or
- Send to a networked 3D printer using Klipper/Moonraker. Other services should be relatively straightforward to implement if requested. No support for password-protected Moonraker instances so far though
    - Option to auto-start running the file after upload

### Monitor
- Simply takes the provided URL from the export tab and renders the webpage; handy for monitoring Klipper from within the app rather than opening a separate browser


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
I have a 3D printer. I wanted to get into bookbinding, which utilises a lot of vinyl designs that typically require a Cricut or Cricut-like vinyl cutter that costs as much as a 3D printer - which is already a perfectly good 3-axis system, capable of ~200 micron cutting precision if you set up your toolhead properly. 
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
