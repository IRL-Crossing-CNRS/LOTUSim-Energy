# Build and ship the player

A scenario does not open this project: it starts a **player**, a built copy
of it, which the scenario workspace downloads on its first run. A change made
here reaches scenarios only once a new player is built and shipped.

## Build it

1. Open the project with the Unity version of the [README](../README.md#requirements).
2. **Build the Addressables first.** Vehicles are loaded by address, from the
   `SpawnInterface` group, and the project does not force a rebuild with the
   player: Window > Asset Management > Addressables > Groups, then
   Build > New Build > Default Build Script. Skipping it ships a player that
   still spawns the old prefabs, or none for a new vehicle.
3. File > Build Profiles: platform **Linux** (or Windows), with
   `Scenes/Launcher` first in the scene list.
4. Build into a folder named exactly after the player:

   | Player | Folder | Executable inside |
   |---|---|---|
   | Linux | `lotusimenergy_linux/` | `LOTUSimEnergy.x86_64` |
   | Windows | `lotusimenergy_windows/` | `LOTUSimEnergy.exe` |

## Try it before shipping

Point a scenario at the new build without replacing anything:

```bash
UNITY_EXE_DIR=~/builds/lotusimenergy_linux \
  ./src/simulation_run/executable/scenario_launch.sh --config basic_examples/waypoint_solo.json
```

(from the scenario workspace; see its **Launch options** page).

## Ship it

The players are not in git: each one is a release asset of the scenario
workspace, pinned by its sha256 in `lotusim_unity_executables/players.json`.

1. Archive the folder, with the folder itself at the top of the archive:

   ```bash
   tar -czf linux_executable.tar.gz lotusimenergy_linux
   sha256sum linux_executable.tar.gz
   ```

2. Replace the asset in the release named in `players.json`:

   ```bash
   gh release upload v1.3 linux_executable.tar.gz --clobber \
     --repo IRL-Crossing-CNRS/LOTUSim-generic-scenario
   ```

3. Put the new sha256 in `players.json`, on the scenario workspace branch the
   player goes with, and commit it there.

Every machine then downloads the new player on its next run, because its
`.player_sha256` no longer matches.

## A new scene or world

The launcher picks the player from the scenario's world, through
`WORLD_UNITY_BASENAMES` in `scenario_launch.sh`
(`energy.world` → `lotusimenergy`). A world with no entry cannot start Unity:
add one, and a `players.json` entry if the world needs a player of its own.
