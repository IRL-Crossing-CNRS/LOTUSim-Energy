# LOTUSim-Energy cable system

The simulator needs some submarine cables for the offshore wind farm: one cable per turbine, the cable will head to a connection point, then to shore, and random faults will spawn along it for drones to find.

![Floating wind turbines over a sandy seabed, each with a cable that dips down, arches up on yellow buoyancy modules, and settles onto the seabed. (working on this)](docs/cable-system.jpg)

*Each cable starts from the back part of the base and has an initial dip, buoyancy modules cause it to arch back up relieving (nonexistent) stress on the cable, the cable then settles onto the seabed.*

## How a cable is built

- **Path:** the cable leaves the turbine's cable cover, drops to a low point, rises into a hump, comes down onto the seabed, runs along it, and rises to the collection point.
- **Tube mesh:** the path is turned into a real 3D tube, so it looks round from any angle and takes lighting. Damage, floats and sleeves all need this switched on (`Use Tube Mesh`).
- **Buoyancy modules:** a string of yellow floats sits around the top of the hump and holds it up.
- **Touchdown protection:** a red sleeve follows the cable where it meets the sand, to guard against abrasion.
- **Damage:** some cables get a break, a tear, or a corroded patch. At launch, one or two cables also lose a float: those cables lose their hump, and the failed module shows cracks.

## Set up

### Turbine cables

1. Add `TurbineCableGenerator` to an empty GameObject.
2. Assign the `Collection Point`.
3. On each turbine, add an empty child named `CableAttach` at the tip of the cable cover.
4. Set `Turbine Parent` (or fill the `Turbines` list) and `Seabed Y`.
5. Press Play, or right-click the component header and choose `Generate Cables`.

### Export cable to shore

1. Add `SeabedCableRoute` to another empty GameObject.
2. Assign `Start Point` (the collection point), `Ascent Point` and `Shore Point`. Waypoints are optional.
3. Set `Seabed Y` and `Shore Y`. Select the object to preview the route in the Scene view, then generate.

## Settings you'll change most

| Setting | What it does |
|---|---|
| `Seabed Y` | World height of the seabed. |
| `Lazy Wave Length` | Distance from the turbine to where the cable lands. About 2 to 3 times the water depth looks right. |
| `Sag Height`, `Hump Height` | Height of the low point and the crest, as a fraction of the water depth. |
| `Failed Modules` | How many floats fail each launch (default 1 to 2, each on a different cable). |
| `Float Failure Seed` | 0 gives different failures every launch. Any other number repeats them. |
| `Damaged Cable Chance` | Fraction of cables that get damage. The Damage `Seed` repeats the same pattern. |
| `Cable Width` | Cable diameter in metres. |
| `Touchdown Protection` | Sleeve length, thickness and colour. Assign a material to change the look. |

## Finding the damage

Every damage spot is a child object with a trigger collider and a `CableDamageMarker` that records its type, severity, cable name and distance along the cable. `CableDamageMarker.All` lists every marker in the scene, which is useful for scoring a drone run. Set `Damage Layer Name` to put the colliders on a layer your sensors can filter. Failed floats are also written to the Console, naming the turbine.

## Custom looks

- **Striped cable:** assign a seamless stripe material to `Cable Material`. Set Tiling X to a whole number, and Tiling Y to 5 × X ÷ (π × Cable Width).
- **Cracks on a failed float:** add your decal materials to `Crack Decal Materials`, or your own prefabs to `Cut Prefabs`.
- **Sleeve and floats:** each has its own `Material` slot.

## Troubleshooting

<details>
<summary>Something renders pink</summary>

The material's shader doesn't match your render pipeline. Convert it with Edit > Rendering > Materials > Convert Selected Built-in Materials to URP.

</details>

<details>
<summary>Cracks don't appear</summary>

Decal projectors need URP with the Decal renderer feature switched on in your URP Renderer asset. The decal material must use a URP Decal shader.

</details>

<details>
<summary>Stripes show a seam</summary>

Tiling X must be a whole number. Recalculate Tiling Y if you change Cable Width.

</details>

<details>
<summary>No damage appears</summary>

Damage only works with `Use Tube Mesh` on, and cables shorter than twice the `Edge Margin` get none.

</details>

## Scripts

- `TurbineCableGenerator` builds the turbine cables and picks the float failures.
- `SeabedCableRoute` builds the export cable to shore.
- `CableTubeBuilder` turns a path into the 3D tube.
- `CableBuoyancy` builds the float modules and the cracked one.
- `CableProtection` builds the touchdown sleeve.
- `CableDamage` plans and builds breaks, tears and corrosion, and holds the detection markers.
