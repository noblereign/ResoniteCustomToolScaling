# CustomToolScaling

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that lets you specify a custom tool scale on your avatar.

If you like this mod, there's a relevant issue on Resonite's issue tracker you might want to thumbs-up: [#5669](https://github.com/Yellow-Dog-Man/Resonite-Issues/issues/5669)

## Usage
The mod works by reading dynamic variables under the `Avatar` space. If your avatar doesn't have a DynamicVariableSpace named Avatar, you should add that first.
Afterwards, you can configure the following parameters using DynamicValueVariables:

- bool `Noble.ToolScale.DirectScaling`: By default, the mod uses your custom values as a multiplier against the default tool scale. Set this to `true` to apply your custom scale directly as an absolute value.
- float `Noble.ToolScale`: The custom scale applied to tools in both hands.
- float `Noble.ToolScale.Right`: The custom scale applied specifically to tools in your right hand. If set, this overrides the shared `Noble.ToolScale`.
- float `Noble.ToolScale.Left`: The custom scale applied specifically to tools in your left hand. If set, this overrides the shared `Noble.ToolScale`.

## Installation
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
1. Place [CustomToolScaling.dll](https://github.com/noblereign/ResoniteCustomToolScaling/releases/latest/download/CustomToolScaling.dll) into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a default install. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create this folder for you.
1. Start the game. If you want to verify that the mod is working you can check your Resonite logs.
