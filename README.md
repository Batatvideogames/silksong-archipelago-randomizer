# Silksong Archipelago Randomizer

An Archipelago randomizer for *Hollow Knight: Silksong*.

It randomizes Silksong checks and items into an Archipelago multiworld, allowing several players to share item progression across supported games. The randomizer includes progression items, useful items, filler, and traps, with in-game map and item feedback to help track the run.

This project is currently being tested and improved. Expect occasional bugs or logic changes between releases.

## What is randomized?

The world can randomize Silksong checks and progression items, including movement abilities, tools, crests, maps, mask shards, silk skills, fleas, and other useful or filler items. Traps may also be enabled depending on the generation settings.

The exact checks and options available can change as the randomizer develops. Use the generated YAML template and the release notes for the current version.

## Requirements

- *Hollow Knight: Silksong* on PC
- Archipelago
- BepInEx configured for Silksong
- The Archipelago client included with the release or your Archipelago installation

## Installation

There are multiple installation methods due to different issues with each of them at times, follow one of the methods below to install the Archipelago randomizer.

### Using Cogfly (1.2.3+)
1.) First start off by installing [Cogfly](https://github.com/Nix-main/Cogfly/releases) to the latest version for your preferred operating system.

2.) Launch Cogfly and go to settings and make sure your path to your Silksong installation are valid.

3.) Go to profiles, select import from code, and enter the following profile code. The profile code contains the Archipelago Randomizer Mod, Custom Music Rando, Crest Wheel Mod, Mod Menu, Shakra Always Onsite, and all other dependencies.

**Cogfly Import Code:**`019ffd16-ebf3-286a-1c4c-ea0680a14bbd`

### Using R2Modman (3.2.18+) / Thunderstore Manager (1.123.1+)
1.) First start off by installing [R2Modman](https://r2modman.com/download-latest/) to the latest version for your preferred operating system.

2.) Select Hollow Knight: Silksong in the Game Selection Menu and select your Silksong storefront.

3.) In profile selection, select 'Import / Update', select 'From code', and enter the following profile code. The profile code contains the Archipelago Randomizer Mod, Custom Music Rando, Crest Wheel Mod, Mod Menu, Shakra Always Onsite, and all other dependencies.

**R2Modman Import Code:** `019ffd04-7e37-cda2-fa95-cfa351e40455`

### Manually
1.) Find your Hollow Knight: Silksong installation folder based on your Silksong storefront.

2.) Download the latest release of [BepInEx](https://github.com/BepInEx/BepInEx/releases) for your preferred operating system.

3.) Drag all the contents of the BepInEx zip file that you downloaded into your Silksong game folder where the game's .exe resides.

4.) Launch Silksong once for the BepInEx files to initialize and then close the game.

5.) Download the Silksong Randomizer's [latest release](https://github.com/Batatvideogames/silksong-archipelago-randomizer/releases/latest) .rar file.

6.) There will be a folder named "PUT IN SILKSONG FOLDER" with a 'BepInEx' folder inside it. Drag the 'BepInEx' folder from the zip file into your Silksong installation folder.

 **OPTIONAL**: If you want any of the other additional mods mentioned, you can find them on Thunderstore, download them, and place them into the 'plugins' folder found in the 'BepInEx' folder in your Silksong game installation folder.

## Archipelago Setup

1.) Download the [Archipelago Launcher](https://github.com/ArchipelagoMW/Archipelago/releases/latest) for your preferred operating system.

2.) Download the Silksong Randomizer's [latest release](https://github.com/Batatvideogames/silksong-archipelago-randomizer/releases/latest) .apworld file.

3.) Launch the Archipelago Launcher and drag the .apworld file into the client. Restart the Archipelago Launcher after installing the apworld.

4.) You can then generate a options configuration for yourself using one of the following two methods:

  - Press the "Options Creator" on the Launcher. Select Hollow Knight: Silksong on the left hand side of the screen that pops up and select all your preferred options. Finally, press export options and save it.

  - Press the "Generate Template Options" on the Launcher which will create .yaml's for all your installed .apworlds. Look for the 'Hollow Knight Silksong.yaml' file, this will be the file you edit for your options.

5.) Take the file that you generated in step 4 and place it in your Archipelago 'Players' folder alongside any other yaml that you will be using for your multiworld.

6.) Go back to the Archipelago Launcher and press generate. A script will run that will generate your multiworld seed and place a .zip file into your Archipelago 'output' folder.

7.) You can then upload the zip to host the game as you wish. Archipelago's upload hosting page can be found [here](https://archipelago.gg/uploads).

## Frequently Asked Questions (FAQ)

### I got to Act 3 and missed some checks in Act 2?
Some items become unavailable once you enter act 3, go to the always open Bellhome in Bellhart. 
Once you reach act 3 once, sit on the bed, press dash + needolin to get a pop-up asking to revert the world back to Act 2 and vice versa.

### What are the Map Markers showing?
When enabled in the yaml "check_map_markers", the map can show randomized checks at their physical locations.

Key for these markers are as follows:
- **Fully opaque (with AP style rosary icon)**: Reachable / in logic.
- **Half transparent (with AP style rosary icon)**: Not reachable / not in logic.
- **? / inverted icons**: Missing logic so far / guaranteed junk.

### I’m stuck somewhere, what do I do?
**Press F4.**

Seriously. It allows warping to homepoints like Bone Bottom, Bellhart, and Songclave as you reach them, skips the widow cutscene to stop softlocks and can save you from the slab. 
Also you can press F3 and it'll allow changing of warp locations as of v0.4.3.

### Do you have a feature like Benchwarp?
Short answer, no. Please refer to I’m stuck somewhere, what do I do?

Long answer, it can cause softlocks and other issues, it can be used but at your own risk of issues and potential woodchipper related incidents.

## Troubleshooting

- If Silksong does not connect, confirm that BepInEx is installed correctly and that the plugin is in the `BepInEx/plugins` folder.
- If the Silksong world does not appear in Archipelago, confirm that the `.apworld` is in `custom_worlds` and restart the Archipelago launcher.
- If items or checks appear out of sync, reconnect the client or use the client's resynchronization command if available.
- If reporting a bug, include the randomizer version, Archipelago version, relevant YAML settings, and the BepInEx log.

## Feedback and bug reports

Please report bugs and logic issues through the repository's [issues](https://github.com/Batatvideogames/silksong-archipelago-randomizer/issues) page. Include enough information to reproduce the problem, but do not upload save files containing personal information.

A second place to give this feedback can be this [Google Form](https://forms.gle/bAsQ2o453eccpL2i7).

A [trello board](https://trello.com/b/as8YLuLA/skonging) is also available to post known bugs, what's been fixed, and what's upcoming.

## Support and Additional Information

The best place to get any further support would be [Archipelago's Discord](https://discord.gg/archipelago) where you can access the [Hollow Knight: Silksong (Batsong Implementation)](https://discord.com/channels/731205301247803413/1536792372321583124) thread. 

This is a reminder that this is a passion project and people will try to help to the best of their ability but as always, please be respectful.
