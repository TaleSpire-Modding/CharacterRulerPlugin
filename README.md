# Character Ruler Plugin
[![Push to nuget feed on release](https://github.com/TaleSpire-Modding/CharacterRulerPlugin/actions/workflows/release.yml/badge.svg)](https://github.com/TaleSpire-Modding/CharacterRulerPlugin/actions/workflows/release.yml)

Adds abilities to create rulers snapped to Character Base

## Install

Currently you need to either follow the build guide down below or use the R2ModMan. 

## Usage
Player: While having a selected mini, open the radial menu and select "Attack" => "Measure Distance". A line ruler will be created snapped to the base of your mini to the selected mini.
When either mini moves, the Ruler will be updated to the new positions. If Line of Sight is broken, then the ruler will disappear. You can delete the ruler by going through the radial menu same as before and select "Remove Ruler".
GM: Same as Player, but the Ruler will not dissapear if Line Of Sight is broken. This doesn't apply if you're in player mode.

## How to Compile / Modify

Open ```CharacterRulerPlugin.sln``` in Visual Studio.

Build the project (We now use Nuget).

Browse to the newly created ```bin/Debug``` or ```bin/Release``` folders and copy the ```.dll``` to ```Steam\steamapps\common\TaleSpire\BepInEx\plugins```

## Changelog
- 1.0.1: Update README documentation. No code changes
- 1.0.0: Initial release

## Shoutouts
<!-- CONTRIBUTORS-START -->
Shoutout to my past [Patreons](https://www.patreon.com/HolloFox), recognising your mighty support and contribution to my caffeine addiction:
- [Demongund](https://www.twitch.tv/demongund) - Introduced me to TaleSpire
- [Tales Tavern/MadWizard](https://talestavern.com/)
- Joaqim Planstedt
<!-- CONTRIBUTORS-END -->