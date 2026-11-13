# LOTUSim-Energy wake system

The simulator visualizes the aerodynamic wake behind each floating wind turbine using a particle-based cone that models the Larsen (C Larsen) wake deficit — the region of reduced wind speed that trails downwind of a spinning rotor.

![Wind turbines with blue particle cones trailing downwind, denser at the centre and fading at the edges](docs/wake-system.jpg)

*Each turbine emits a cone of blue particles behind the rotor. The core is dense and dark blue (strong velocity deficit), eventually changing to a transparent green at the cone edge where ambient wind speed recovers.*

## How the wake works

- **Shapes:** the wake is a created using particle systems that starts at the rotor diameter, matching the blades and creating according particles.
- **Particle density:** particles are distributed densly near the centreline, thinning toward the edge. This matches the velocity deficit profile shown in the Larsen diagram.
- **Colour:** deep blue at the centre (low normalised velocity, strong deficit) changing to a red, then a transparent green at the cone edge where the wind speed has mostly recovered.
- **Emission:** the wake only appears when the turbine is spinning. Emission rate and particle lifetime scale directly with the turbine's animator speed, so a slow-spinning turbine produces a faint short wake and a fast-spinning turbine produces a full dense cone.
- **Wind driven:** the wake cone faces downwind automatically, rotating with the nacelle via `Face_The_Wind`. When wind drops below cut-in speed the blades stop and the wake disappears entirely.

## How it is built

Each turbine has three particle systems parented to an empty `Particles` GameObject on the turbine prefab. These are managed by two scripts:

- **`WakeEmissionController`** reads the turbine's `CurrentAnimSpeed` from `WindTurbineController` every frame and scales emission rate and particle lifetime accordingly. It also hard-stops all particle systems on startup so no wake appears before wind is applied.
- **`Face_The_Wind`** rotates the turbine nacelle (and by extension the particle systems parented to it) to face into the wind using `WindSliderController.CurrentWindVector`.

## Set up

### Per turbine

1. On the turbine prefab, add an empty child GameObject named `Particles` positioned just behind the rotor hub.
2. Add three `ParticleSystem` components as children of `Particles` — one for the dense core, one for the mid region and one for the outer edge.
3. Add `WakeEmissionController` to the `Particles` GameObject.
4. The controller will auto-find `WindTurbineController` in the parent — no manual assignment needed.
5. Set `maxEmissionRates` and `maxLifetimes` in the Inspector to match your three particle systems.
6. **Untick `Play On Awake`** on all three particle systems — `WakeEmissionController` manages play/stop entirely.

### Wind direction

1. Add `Face_The_Wind` to the nacelle GameObject (the part that should yaw to face the wind).
2. Assign `WindSliderController` in the Inspector.
3. Adjust `angleOffset` if the model's zero direction does not align with the wind arrow.
4. Tick `reverseWind` if the nacelle faces away from the wind instead of into it.

## Settings you'll change most

| Setting | What it does |
|---|---|
| `maxEmissionRates` | Emission rate per particle system at full turbine speed. Match your three systems. |
| `maxLifetimes` | Particle lifetime per system at full speed. Controls how far the wake extends. |
| `minSpeedThreshold` | Minimum animator speed before any wake appears. Prevents wake showing at near-zero wind. |
| `angleOffset` | Degrees added to the nacelle yaw. Compensate if the model's forward doesn't match wind direction. |
| `reverseWind` | Flip if the nacelle rotates 180° the wrong way. |

## How emission scales with wind

The `WindTurbineController` computes a speed multiplier from the current wind magnitude:

```
speedMultiplier = windSpeed / nominalWindSpeed
```

- Below `cutInSpeed` (default 5 m/s) — blades stopped, no wake.
- At `nominalWindSpeed` (default 12 m/s) — full emission, full lifetime.
- Above `cutOutSpeed` (default 25 m/s) — blades stopped for protection, no wake.

`WakeEmissionController` reads `CurrentAnimSpeed` (which is this multiplier, smoothed) and applies it:

```
emission.rateOverTime = maxEmissionRate × animSpeed
main.startLifetime    = maxLifetime     × animSpeed
```

## Troubleshooting

<details>
<summary>Wake appears even with no wind</summary>

`Play On Awake` is still ticked on one or more particle systems. Untick it on all three — `WakeEmissionController` handles play/stop itself.

</details>

<details>
<summary>Wake does not disappear when wind drops</summary>

Check that `WindTurbineController` is present on the turbine root and `useDirectWindSubscription` is enabled. If ROS is not connected the turbine will never receive a wind update and `CurrentAnimSpeed` stays at its last value.

</details>

<details>
<summary>Wake points the wrong direction</summary>

Adjust `angleOffset` on `Face_The_Wind` in 90° increments until it aligns, or tick `reverseWind` to flip it 180°.

</details>

<details>
<summary>Wake is too short or too long</summary>

Increase or decrease `maxLifetimes` on `WakeEmissionController`. Longer lifetime = particles travel further before fading = longer wake cone.

</details>

<details>
<summary>Wake renders pink</summary>

The particle material shader does not match the render pipeline. The particle systems use `Sprites/Default` — if this is missing, assign any transparent unlit material from your HDRP material library.

</details>

## Scripts

- `WakeEmissionController` scales particle emission and lifetime with turbine rotation speed and hard-stops all systems when the turbine is idle.
- `Face_The_Wind` rotates the nacelle on the Y axis to face into the wind direction from the sliders.
- `WindTurbineController` drives the blade animator speed from wind speed via the TSR formula and exposes `CurrentAnimSpeed` for other scripts to read.
