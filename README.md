# Aster Szalony Autobus

**Crazy Taxi style Polish PKS bus simulator** on the Ozorków–Łódź route (DK91).
Built with **Godot 4.3 (C#)** targeting Android.

## Features

- **Realistic bus physics** using `VehicleBody3D` with tuned parameters for Polish buses (Jelcz M125M / Ikarus 280 / Solaris)
- **OSM-based map** import via Blender + Blosm → glTF chunks with collision
- **Bus stop system** with triggers, boarding timer, passenger counts, time bonuses
- **GPS navigator** arrow pointing to next waypoint/stop
- **AI traffic** cars following spline paths with player avoidance
- **Time attack gameplay**: 5 minutes Ozorków → Łódź Manufaktura, drift bonuses, perfect stop bonuses
- **Nitro system** for extra speed/drift fun
- **Android export** via GitHub Actions CI (AAB)

## Project Structure

```
AsterSzalonyAutobus/
├── .github/workflows/export-android.yml   # CI: builds .aab on push
├── src/
│   ├── Vehicles/Bus.cs                    # VehicleBody3D physics + nitro + drift
│   ├── Stops/
│   │   ├── BusStopManager.cs              # Stop logic, boarding, UI, bonuses
│   │   └── BusStop.cs                     # Individual stop trigger + marker
│   ├── Route/GPSNavigator.cs              # Waypoint arrow navigation
│   ├── AI/TrafficManager.cs               # Traffic cars on Path3D splines
│   └── GameManager.cs                     # Game loop, score, time, win/lose
├── scenes/
│   ├── Main.tscn                          # Root scene
│   ├── Bus.tscn                           # Bus VehicleBody3D + wheels
│   ├── BusStop.tscn                       # Stop Area3D + visual marker
│   ├── World.tscn                         # Imported OSM chunks (placeholder)
│   └── WorldEnvironment.tres              # Sky, fog, lighting
├── assets/
│   ├── models/bus.glb                     # Your bus model (Jelcz/Ikarus/Solaris)
│   ├── audio/                             # Engine, doors, ambient
│   └── ui/                                # Theme, fonts, icons
├── import/osm_to_gltf.py                  # Blender script: OSM → glTF chunks
├── export_presets.cfg                     # Android export (ARM64, AAB)
├── AsterSzalonyAutobus.csproj             # .NET 8 project
└── project.godot                          # Godot 4.3 config
```

## Quick Start

### Prerequisites
- **Godot 4.3** (C# edition / .NET 8 SDK)
- **Blender 4.0+** with **Blosm addon** for OSM import
- **Android SDK/NDK** (for local export) or use GitHub Actions

### 1. Clone & Open
```bash
git clone https://github.com/magicsebastian87-byte/AsterSzalonyAutobus.git
cd AsterSzalonyAutobus
# Open in Godot 4.3 or Rider/VS Code
```

### 2. Import Map (OSM → glTF)
```bash
# In Blender:
# 1. Install Blosm addon (Edit → Preferences → Add-ons → Install from zip)
# 2. Run script:
blender --background --python import/osm_to_gltf.py -- \
  --bbox "19.2,51.9,19.6,52.1" \
  --output "scenes/World.gltf" \
  --chunk-size 1000
```
This generates `scenes/World_<chunk>.gltf` + `scenes/world_manifest.json`.

### 3. Add Bus Model
Place your `.glb` bus model at `assets/models/bus.glb` and update `scenes/Bus.tscn`:
```gdscript
[node name="MeshInstance3D" type="MeshInstance3D" parent="."]
mesh = load("res://assets/models/bus.glb")
```

### 4. Run in Editor
Press **F5** - drive on flat test terrain with bus stops.

### 5. Export Android (CI)
Push to `main` or tag a release - GitHub Actions builds `builds/AsterSzalonyAutobus.aab`.

For local export:
```bash
godot --headless --export-release "Android" builds/AsterSzalonyAutobus.aab
```

## Physics Tuning (Bus.cs)

| Parameter | Value | Description |
|-----------|-------|-------------|
| `Mass` | 15000 kg | Jelcz/Ikarus weight |
| `CenterOfMass` | (0, 1.2, 0.5) | High + rear-biased |
| `EngineForce` | 85,000 N | ~250 HP equivalent |
| `RearTractionMultiplier` | 0.75 | Enables drift |
| `SteeringAngle` | 38° | Bus turning radius |
| `NitroMultiplier` | 1.35 | 35% power boost |

Adjust in Inspector while playing - changes apply live.

## Gameplay Loop

1. **Start** at Ozorków Plac Jana Pawła II
2. **Drive** DK91 through Zgierz, Pabianice toward Łódź
3. **Stop** at bus stops (trigger zone + speed ≈ 0)
4. **Wait** 3s boarding → doors close → time bonus for good parking
5. **Drift** corners, use **Nitro (Shift)** for speed
6. **Reach** Łódź Manufaktura before 5:00 timer
7. **Score** = passengers × 10 + time bonuses + style points

## CI/CD Secrets (for Release)

Add to GitHub repo Settings → Secrets → Actions:
- `ANDROID_KEYSTORE_BASE64` - base64 encoded `.keystore`
- `ANDROID_KEYSTORE_PASSWORD` - keystore password
- `ANDROID_KEY_PASSWORD` - key password
- `ANDROID_KEY_ALIAS` - key alias
- `GOOGLE_PLAY_SERVICE_ACCOUNT` - (optional) JSON for Play Console upload

## Credits

- **Engine**: Godot 4.3 (MIT)
- **Map Data**: OpenStreetMap contributors (ODbL)
- **OSM Import**: Blosm Blender addon
- **Bus Models**: Sketchfab / CGTrader (check licenses)

## License

MIT License - see LICENSE file.
