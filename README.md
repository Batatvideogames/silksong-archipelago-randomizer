# Silksong Archipelago Randomizer

An Archipelago randomizer for *Hollow Knight: Silksong*.

It randomizes Silksong checks and items into an Archipelago multiworld, allowing several players to share item progression across supported games. The randomizer includes progression items, useful items, filler, and traps, with in-game map and item feedback to help track the run.

This project is currently being tested and improved. Expect occasional bugs or logic changes between releases.

## Downloads

Download the latest release from the [Releases page](https://github.com/Batatvideogames/silksong-archipelago-randomizer/releases).

Each release contains:

- The Silksong `.apworld` for Archipelago.
- The BepInEx plugin `.dll` for the game client.

## Requirements

- *Hollow Knight: Silksong* on PC
- Archipelago
- BepInEx configured for Silksong
- The Archipelago client included with the release or your Archipelago installation

## Installation

1. Download the latest `.apworld` and `.dll` from [Releases](https://github.com/Batatvideogames/silksong-archipelago-randomizer/releases).
2. Install the `.apworld` using Archipelago, or place it in Archipelago's `custom_worlds` folder.
3. Place the `.dll` in the Silksong BepInEx plugins folder:

   ```text
   Hollow Knight Silksong/BepInEx/plugins/
   ```

4. Generate or join an Archipelago game using the Silksong world.
5. Launch Silksong with BepInEx and connect the client to the room.

Always use the `.apworld` and plugin from the same release.

## What is randomized?

The world can randomize Silksong checks and progression items, including movement abilities, tools, crests, maps, mask shards, silk skills, fleas, and other useful or filler items. Traps may also be enabled depending on the generation settings.

The exact checks and options available can change as the randomizer develops. Use the generated YAML template and the release notes for the current version.

## Troubleshooting

- If Silksong does not connect, confirm that BepInEx is installed correctly and that the plugin is in the `BepInEx/plugins` folder.
- If the Silksong world does not appear in Archipelago, confirm that the `.apworld` is in `custom_worlds` and restart the Archipelago launcher.
- If items or checks appear out of sync, reconnect the client or use the client's resynchronization command if available.
- If reporting a bug, include the randomizer version, Archipelago version, relevant YAML settings, and the BepInEx log.

## Feedback and bug reports

Please report bugs and logic issues through the repository's [Issues](https://github.com/Batatvideogames/silksong-archipelago-randomizer/issues) page. Include enough information to reproduce the problem, but do not upload save files containing personal information.