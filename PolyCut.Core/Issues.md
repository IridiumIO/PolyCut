# Issues

## PolyCut 
- The visual indicator for the imported Elements list does not keep up with the actual list
- The toggle button to hide travel moves needs to be toggled twice to take effect
- 

## PolyCut.Core 
- Fills don't ignore open paths and will attempt to fill them regardless, using a naive algorithm
- Tool diameter offset paths have a fixed resolution and do not take into account the precision setting



# Pending Features 

## PolyCut
- Save/load configuration to file	
	- Also allow multiple separate config files
- Export to Moonraker
- Export to GCode File


## PolyCut.Core
### Parity Features
- Extract single colour
- Shading threshold
- Shading density based on colour
- Shading density based on brightness?
- Cutting order (in first vs out first)

### Drawing
- Different shading modes
    - Contour
	- Hatch
	- Crosshatch

- Boolean Modes
    - Union
	- Intersection
	- Difference
	- XOR