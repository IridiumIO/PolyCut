# Klipper Setup
We're going to set up Klipper to have two separate profiles that will allow us to easily switch between 3D Printing and Plotting/Cutting. After all, we've gone through the effort of making the toolhead hotswappable, so it would be nice to set up Klipper to do the same. 

I'm also using Mainsail for the Web interface. You can use Fluidd if you want, but you'll have to figure out those specifics yourself. 

This looks complicated, but it really isn't; it's just a lot of copy-and-pasting. 

> This is *not* a guide on setting up Klipper from scratch. It assumes you already have your Ender 3 S1 set up in Klipper the way you want for 3D Printing.

## Step 1 - Separating out Printer.cfg

The first thing to do is to **move** everything (except the `SAVE_CONFIG` section!) from the `Printer.cfg` file into its own `3DPrint.cfg` file. 

Then we add an `[include 3DPrint.cfg]` line in the `Printer.cfg` and you should be left with *only* that line and the `SAVE_CONFIG` stuff. 

**Before (Printer.cfg):** 
```yml

[include macros.cfg]
[include mainsail.cfg]

[stepper_x]
step_pin: PC2
dir_pin: PB9
enable_pin: !PC3
microsteps: 16
rotation_distance: 40
...
...other stuff here... 
...



#*# <---------------------- SAVE_CONFIG ---------------------->
#*# DO NOT EDIT THIS BLOCK OR BELOW. The contents are auto-generated.
#*#
#*# [bltouch]
#*# z_offset = -0.000
#*#

```

**After (Printer.cfg):** 
```yml

[include 3DPrint.cfg]

#*# <---------------------- SAVE_CONFIG ---------------------->
#*# DO NOT EDIT THIS BLOCK OR BELOW. The contents are auto-generated.
#*#
#*# [bltouch]
#*# z_offset = -0.000
#*#

```

Note: You'll see that my Z_Offset is set to 0. This is **intentional**. You can mess with the Z offset later but it is important to make sure it's set to zero for now, because that offset will persist between both the 3D Print mode and the Cutting Mode.

What we've done now is simply moved everything from the Printer.cfg to its own file, freeing up the ability to make swaps easier. 



## Step 2 - Setting up PolyCut.cfg

Duplicate the `3DPrint.cfg` file and call it `PolyCut.cfg`. You should have two files now both with the same content. Not to worry, we'll fix that now. Open up `PolyCut.cfg`

### Deleting Useless Sections

**Delete** the following sections entirely:

 - `[include macros.cfg]`
 -  `[extruder]`
 - `[heater_fan hotend_fan]`
 - `[fan]`
 - `[safe_z_home]`

These are all things you don't need running while in cutting mode. Technically you can delete the `[heater_bed]` section as well but it's not necessary. 
> **Make sure you delete `[include macros.cfg]`! We don't need all those printing macros and we'll use our own here**

### Bypass BLTouch
Ideally you would delete the `[bltouch]` section too, but it doesn't like it when you do that. Instead, go to the `[bltouch]` section and change the following line:
```yml
[bltouch]
sensor_pin: ^PC14
...
```
to:
```yml
[bltouch]
sensor_pin: ^!PC14
...
```
Note the exclamation mark, which inverts the default signal and stops it making a fuss when you're not using it. 

### Set Manual Homing

Now, we've disabled automatic homing so we have to rig together our own Z-homing mechanism. The new way to home will simply be to lower the Z gantry by hand until the blade touches the cutting mat. 

Copy and paste the following into the file:

```yml
[homing_override]
gcode:
  {% if printer.toolhead.homed_axes == "xyz" %}
    RESPOND TYPE=command MSG="Already Homed"
  {% else %}
    G91
    G1 Z10
    G28.1 X Y
    G90
    G1 Z0
    G92 Z0
  {% endif %}

axes: xyz
set_position_z: 0.0

[gcode_macro G28]
# Only home if needed.
rename_existing: G28.1
gcode:
  {% if printer.toolhead.homed_axes != "xyz" %}
    G28.1
  {% endif %}
  
[gcode_macro G00]
gcode:
  G0 {rawparams}

[gcode_macro G01]
gcode:
  G1 {rawparams}
```

What each part does:

 - **`[homing_override]`** - This section bypasses homing on the Z axis since we've removed the BLTouch. **The Z position is set to wherever the print head's height (cutting head?) was when homing was run, so make sure you move the Z gantry down so the blade is touching the cutting surface before you home!** If the blade is 20mm above the bed when you home the printer, that 20mm height becomes the new "zero". 
 - **`[gcode_macro G28]`** -  This bypasses homing entirely if the axes are already homed, so you don't have to keep re-homing between every cutout. 
 - **`[gcode_macro G00]` and `[gcode_macro G01]`**- These just tell Klipper to interpret all G00 and G01 codes as G0 and G1 instead. Inskcape likes to output double-digits which Klipper can't understand. 


### Fixing the Pause and Cancel Buttons
By default, the pause and cancel buttons in Klipper contain code that depends on the extruder being present. Copy and paste the following instead (If you already have these sections in your `PolyCut.cfg` file, overwrite them. I'm assuming most of your macros would be in `macros.cfg` however). 

```yml

[gcode_macro CANCEL_PRINT]
description: Cancel the actual running print
rename_existing: CANCEL_PRINT_BASE
gcode:
  ##### get user parameters or use default #####
  {% set allow_park = False if printer['gcode_macro _CLIENT_VARIABLE'] is not defined
                 else False if printer['gcode_macro _CLIENT_VARIABLE'].park_at_cancel is not defined
                 else True  if printer['gcode_macro _CLIENT_VARIABLE'].park_at_cancel|lower == 'true' 
                 else False %}
  {% set retract      = 5.0  if not macro_found else client.cancel_retract|default(5.0)|abs %}
  {% set sp_retract   = 2100 if not macro_found else client.speed_retract|default(35) * 60 %}
  ##### end of definitions #####
  {% if not printer.pause_resume.is_paused and allow_park %} _TOOLHEAD_PARK_PAUSE_CANCEL {% endif %}
  
  TURN_OFF_HEATERS
  M106 S0
  CANCEL_PRINT_BASE

[gcode_macro PAUSE]
description: Pause the actual running print
rename_existing: PAUSE_BASE
gcode:
  PAUSE_BASE
  _TOOLHEAD_PARK_PAUSE_CANCEL {rawparams}

[gcode_macro RESUME]
description: Resume the actual running print
rename_existing: RESUME_BASE
gcode:
  ##### get user parameters or use default #####
  {% set macro_found = True if printer['gcode_macro _CLIENT_VARIABLE'] is defined else False %}
  {% set client = printer['gcode_macro _CLIENT_VARIABLE'] %}
  {% set velocity = printer.configfile.settings.pause_resume.recover_velocity %}
  {% set use_fw_retract = False if not macro_found
                     else False if client.use_fw_retract is not defined
                     else True  if client.use_fw_retract|lower == 'true' and printer.firmware_retraction is defined
                     else False %} 
  {% set unretract      = 1.0  if not macro_found else client.unretract|default(1.0)|abs %}
  {% set sp_unretract   = 2100 if not macro_found else client.speed_unretract|default(35) * 60 %}
  {% set sp_move        = velocity if not macro_found else client.speed_move|default(velocity) %}
  ##### end of definitions #####
  
  RESUME_BASE VELOCITY={params.VELOCITY|default(sp_move)}  

[gcode_macro _TOOLHEAD_PARK_PAUSE_CANCEL]
description: Helper: park toolhead used in PAUSE and CANCEL_PRINT
gcode:
  ##### get user parameters or use default #####
  {% set macro_found = True if printer['gcode_macro _CLIENT_VARIABLE'] is defined else False %}
  {% set client = printer['gcode_macro _CLIENT_VARIABLE'] %}
  {% set velocity = printer.configfile.settings.pause_resume.recover_velocity %}
  {% set use_custom     = False if not macro_found
                     else False if client.use_custom_pos is not defined
                     else True  if client.use_custom_pos|lower == 'true' 
                     else False %}
  {% set custom_park_x  = 0.0 if not macro_found else client.custom_park_x|default(0.0) %}
  {% set custom_park_y  = 0.0 if not macro_found else client.custom_park_y|default(0.0) %}
  {% set park_dz        = 12.0 if not macro_found else client.custom_park_dz|default(2.0)|abs %}
  {% set use_fw_retract = False if not macro_found
                     else False if client.use_fw_retract is not defined
                     else True  if client.use_fw_retract|lower == 'true' and printer.firmware_retraction is defined
                     else False %}
  {% set retract      = 1.0  if not macro_found else client.retract|default(1.0)|abs %}
  {% set sp_retract   = 2100 if not macro_found else client.speed_retract|default(35) * 60 %}
  {% set sp_hop       = 900  if not macro_found else client.speed_hop|default(15) * 60 %}
  {% set sp_move      = velocity * 60 if not macro_found else client.speed_move|default(velocity) * 60 %}
  ##### get config and toolhead values #####
  {% set act = printer.toolhead.position %}
  {% set max = printer.toolhead.axis_maximum %}
  {% set cone = printer.toolhead.cone_start_z|default(max.z) %} ; hight as long the toolhead can reach max and min of an delta
  {% set round_bed = True if printer.configfile.settings.printer.kinematics is in ['delta','polar','rotary_delta','winch'] 
                else False %}
  ##### define park position #####
  {% set z_min = params.Z_MIN|default(0)|float %}
  {% set z_park = [[(act.z + park_dz), z_min]|max, max.z]|min %}
  {% set x_park = params.X       if params.X is defined
             else custom_park_x  if use_custom
             else 0.0            if round_bed 
             else (max.x - 5.0) %}
  {% set y_park = params.Y       if params.Y is defined
             else custom_park_y  if use_custom
             else (max.y - 5.0)  if round_bed and z_park < cone
             else 0.0            if round_bed
             else (max.y - 5.0) %}
  ##### end of definitions #####
  
  {% if "xyz" in printer.toolhead.homed_axes %}
    G90
    G1 Z{z_park} F{sp_hop}
    G1 X{x_park} Y{y_park} F{sp_move}
    {% if not printer.gcode_move.absolute_coordinates %} G91 {% endif %}
  {% else %}
    {action_respond_info("Printer not homed")}
  {% endif %}
```

### Adding PolyCut.cfg to the main Printer.cfg file

Let's add `PolyCut.cfg` to `Printer.cfg` but we'll leave it commented out for now: 

Printer.cfg:
```yml

[include 3DPrint.cfg]
#[include PolyCut.cfg]

#*# <---------------------- SAVE_CONFIG ---------------------->
#*# DO NOT EDIT THIS BLOCK OR BELOW. The contents are auto-generated.
#*#
#*# [bltouch]
#*# z_offset = -0.000
#*#

```
You could stop here actually. When you want to switch to cutting mode, you comment out the `[include 3DPrint.cfg]` line and uncomment the `[include PolyCut.cfg]` line, then do a Firmware restart. Vice-versa to switch back to 3D Printing mode. 

But we can do better. 

## Step 3 - Macros to switch modes
Commenting/Uncommenting is tedious. Let's set up some macros to do this for us. 

### Installing G-Code Shell Command
The first thing we need is the ability to run our own scripts. This is the only way I know of to actually modify the files. 
The easiest way to get this extension is to get [KIAUH](https://github.com/dw-0/kiauh) which you hopefully had as part of installing Klipper in the first place. If you didn't use it, just install it anyway, no big deal. 

SSH into your Raspberry PI (Other boards are available), then run the following to install and run KIAUH:
```bash
sudo apt-get update && sudo apt-get install git -y
```
```bash
cd ~ && git clone https://github.com/dw-0/kiauh.git
```
```bash
./kiauh/kiauh.sh
```
You'll see the KIAUH interface come up. 
Press `4` to enter the `Advanced Menu` 
Then press `8` to install G-Code Shell Command. 

Done!

>Note! Be careful from hereon out. You have enabled a way to run arbitrary system code from within GCode - which means don't download and run random GCode files  because they could contain malicious code that can now run. 


### ShellCommand File
Open up `printer.cfg` and at the very top, add a new line 
```
[include shellcommand.cfg]
```
You guessed it, create a new file called `shellcommand.cfg` and open it. 

Paste the following into that file:

```yaml
[gcode_shell_command set_mode]
command: python /home/pi/printer_data/config/SwitchMode.py
timeout: 2.
verbose: False

[gcode_macro SET]
gcode:
    RUN_SHELL_COMMAND CMD=set_mode PARAMS={params.MODE}
    RESPOND PREFIX=! MSG="Mode set to {params.MODE}"
    RESTART

[gcode_macro CUTTING_MODE]
gcode:
    SET MODE=CUT

[gcode_macro POLYCUT_MODE]
gcode:
    SET MODE=PRINT
```

What did I *just* tell you about downloading random G code and sticking it into your files all willy-nilly? You see that second line? 
```yaml
command: python /home/pi/printer_data/config/SwitchMode.py
```
*Any* shell command can be run in that line. I could have entered a command to delete all your files instead. Be careful. 

But what we're actually doing is telling it to run a certain python file to switch the mode (We'll create that file in a second). 
> Note: If your username is not creatively called "pi" like mine is, make sure you change it in the filepath above, e.g. 
> `command: python /home/[YOUR_USERNAME_HERE]/printer_data/config/SwitchMode.py`

 - `[gcode_macro SET]` - This is a macro that lets you type `SET MODE=CUT` or `SET MODE=PRINT` into your console, and it passes the `MODE` variable to the python script. It then automatically restarts the firmware. 
 - `[gcode_macro CUTTING_MODE]` and `[gcode_macro POLYCUT_MODE]` - Shortcuts to the above. We'll use these for creating the interface buttons in Mainsail later. 

### SwitchMode File
Let's create that python file now and call it `SwitchMode.py` (create it in the same place your `printer.cfg` and other similar files are)

Open `SwitchMode.py` and paste the following in:

```py
import sys

config_file_path = '/home/pi/printer_data/config/printer.cfg'

def modifyLine(mode):
    
    with open(config_file_path, 'r') as f:
        lines = f.readlines()

    include_lines = ''

    if mode =="CUT" or mode =="C":
        include_lines = ['[include 3DPrint.cfg]\n', '#[include PolyCut.cfg]\n']
        print("Mode changed to CUT")
    elif mode =="PRINT" or mode =="P":
        include_lines = ['#[include 3DPrint.cfg]\n', '[include PolyCut.cfg]\n']
        print("Mode changed to PRINT")

    with open(config_file_path, 'w') as f:
        for line in lines:
            if line in include_lines:
                line = line.lstrip('#') if line.startswith('#') else '#' + line
            f.write(line)

if __name__ == "__main__":
    # Check if at least one command-line argument is provided
    if len(sys.argv) < 2:
        print("Usage: python myscript.py <your_variable>")
        sys.exit(1)
        
    mode:str = sys.argv[1]

    modifyLine(mode.upper())

```

All this is doing is exactly what we could've done manually: Commenting out one line in `printer.cfg` and uncommenting out the other line, depending on what mode you want.  

Again, if your username is not "pi", make sure you change it in the `config_file_path` variable at the start of the file. 

### Mainsail Buttons
We'll now add buttons to the Mainsail interface so you don't have to keep typing `SET MODE=CUT` and `SET MODE=PRINT`


 1. Go to the Dashboard in Mainsail, and in the top right corner click on the cogs icon to open the Interface Settings. 
 2. Under the `Macros` section, click on `ADD GROUP` at the bottom
 3. Call it whatever you want (`Printer Mode` will do) then under `Status`, make sure only the "zzz" icon is enabled (disable the other two options for showing the macro when the printer is paused or printing. You probbaly don't want to switch modes while halfway through something)
 4. Under the `Available Macros`, add `CUTTING_MODE` and `PRINTING_MODE` 
 5. Go to the `Dashboard` settings (Still in the Interface Settings window!) and drag the `Printer Mode` section wherever you want. I like to keep it near the top. 




