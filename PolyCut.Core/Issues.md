# Issues

## PolyCut 
- [ ] The visual indicator for the imported Elements list does not keep up with the actual list
- [X] The toggle button to hide travel moves needs to be toggled twice to take effect
- [ ] Significant lag when trying to copy GCode from the preview window when it has a lot of lines
- [ ] Touchscreen scrolling/panning does not work
- [ ] Monitor tab won't open if the URL on the Export tab is not prefixed with `http://` or `https://`
- [x] Nested hidden elements are not visible in the editor but are still exported
- [ ] Shapes drawn on canvas directly are buggy
    - [ ] creating a shape so that it extends beyond the canvas edges will cause it to get stuck and can leave a ghost element behind
    - [x] Text cannot be edited after it is created

## PolyCut.Core 
- [x] Fills don't ignore open paths and will attempt to fill them regardless, using a naive algorithm
- [ ] Tool diameter offset paths have a fixed resolution and do not take into account the precision setting
- [ ] Shape drawing does not take into account whether the shape is filled or not, and will always fill closed shapes
- [ ] When trying to fill text, if there are a lot of spaces in a single textbox the fill will not work properly. For example `ABC       DEF`
    - *Workaround*: Use separate textboxes if there are large gaps between characters


# Pending Features 

## PolyCut
- [x] Save/load configuration to file	
	- [ ] Also allow multiple separate config files
- [X] Export to Moonraker
- [X] Export to GCode File
- [X] Switch between PolyCut.Core and GCodePlot for generation
- [ ] Light mode (need to sort out transparency of canvas)
- [ ] Allow saving copies of printers/cutting mats 
- [ ] Add additional default cutting mats
- [ ] Add additional default printers
- [ ] Add selectable Pen to preview to adjust drawing width/color

### Editor
- [ ] Manipulation features
	- [X] Move
	- [X] Rotate
	- [X] Scale
	- [ ] Mirror
	- [X] Delete
	- [ ] Duplicate
	- [ ] Undo/Redo
	- [ ] Align
	- [ ] Group/Ungroup	
	- [ ] Multiple selection
- [ ] Allow grouping/ungrouping elements, and editing nested elements
- [X] Hide cutting mat
- [X] Basic tools: Line/Rectangle/Circle/Text

## PolyCut.Core
### Parity Features
- [X] Extract single colour
- [ ] Shading threshold
- [ ] Shading density based on colour
- [ ] Shading density based on brightness?
- [ ] Cutting order (in first vs out first)

### Drawing
- [ ] Different shading modes
    - [ ] Contour
	- [X] Hatch
	- [X] Crosshatch
- [X] Optimised fill paths
    - [ ] Toggle optimisation
- [ ] Boolean Modes
    - [ ] Union
	- [ ] Intersection
	- [ ] Difference
	- [ ] XOR

### Development
- [ ] Seperate Gcode generators into separate projects
    - [ ]  Implement generators and post-processors as separate plugin DLLs so that others can create custom ones in C# or other languages
- [ ] Switch to using .NET Geometry instead of passing around SVG elements (?modify GCodePlot to use Geometry?)
- [ ] Decouple UI from Core, including separating Generator selection from UI. Intermediary layer required?: `Core/GcodePlot -> Configuration/Selector -> UI`
- [X] Confirm Windows 10 compatibility - Tested with Windows 10 1809. No Mica/Acrylic but it works

### Ambitious
- [ ] Klipper Configuration Generator
- [ ] Export over serial
- [ ] Switch to Avalonia for Linux/Mac compatibility
