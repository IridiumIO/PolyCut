# Issues

## PolyCut 
- [ ] The visual indicator for the imported Elements list does not keep up with the actual list
- [ ] The toggle button to hide travel moves needs to be toggled twice to take effect
- [ ] Significant lag when trying to copy GCode from the preview window when it has a lot of lines
- [ ] Touchscreen scrolling/panning does not work
- [ ] Monitor tab won't open if the URL on the Export tab is not prefixed with `http://` or `https://`
- [ ] Nested hidden elements are not visible in the editor but are still exported

## PolyCut.Core 
- [ ] Fills don't ignore open paths and will attempt to fill them regardless, using a naive algorithm
- [ ] Tool diameter offset paths have a fixed resolution and do not take into account the precision setting



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

### Editor
- [ ] Manipulation features
	- [X] Move
	- [ ] Rotate
	- [X] Scale
	- [ ] Mirror
	- [X] Delete
	- [ ] Duplicate
	- [X] Undo/Redo
	- [ ] Align
	- [ ] Group/Ungroup	
	- [ ] Multiple selection
- [ ] Allow grouping/ungrouping elements, and editing nested elements
- [ ] Hide cutting mat
- [ ] Basic tools: Line/Rectangle/Circle. Text?

## PolyCut.Core
### Parity Features
- [ ] Extract single colour
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