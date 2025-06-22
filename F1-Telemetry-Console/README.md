# F1 2024 Telemetry Receiver

A comprehensive C# application that receives and displays F1 2024 telemetry data in real-time via UDP. Available in both **console** and **WPF desktop** versions, this application decodes F1 2024's UDP telemetry packets and presents the data in formatted, easy-to-read interfaces.

## 🏎️ Versions Available

### 🖥️ **WPF Desktop Dashboard** (Recommended)
A beautiful, real-time F1-style telemetry dashboard with:
- Live gauges for speed, RPM, and gear
- Driver input indicators (throttle, brake, steering)
- G-force displays
- Tyre temperature and pressure monitoring
- Fuel and ERS status
- **Track position widget** with live mini-map and lap progress
- Session information and lap times
- Professional F1-themed UI

### 📟 **Console Version**
A text-based interface perfect for:
- Lightweight telemetry monitoring
- Server environments
- Development and debugging
- Running alongside other applications

## Features

- **Real-time telemetry data** from F1 2024 game
- **Comprehensive packet support** including:
  - Motion data (position, velocity, G-forces, rotation)
  - Session information (track, weather, session type)
  - Lap data (lap times, sectors, position, penalties)
  - Car telemetry (speed, throttle, brake, RPM, gear, temperatures)
  - Car status (fuel, tyre compound, ERS, DRS)
- **Formatted display** with track names, compound names, and time formatting
- **Live updates** at the game's telemetry frequency (10-60Hz)

## Requirements

- .NET 8.0 or later
- F1 2024 game with UDP telemetry enabled
- Network connection between the game and this application

## Setup Instructions

### 1. Configure F1 2024 Game

In F1 2024, navigate to:
**Game Options > Settings > Telemetry Settings**

Set the following:
- **UDP Telemetry**: `On`
- **UDP Broadcast Mode**: `Off` (recommended)
- **UDP IP Address**: IP address of the machine running this console app
  - Use `127.0.0.1` if running on the same machine as the game
  - Use your PC's network IP if running on a different machine
- **UDP Port**: `20777` (default)
- **UDP Send Rate**: `20Hz` or higher
- **UDP Format**: `2024`

### 2. Build and Run the Application

```bash
# Clone or download this repository
cd F1-Telemetry-Console

# Build the application
dotnet build
```

#### Option A: WPF Desktop Dashboard (Recommended)
```bash
# Run the WPF dashboard
dotnet run

# OR use the batch file
run-wpf.bat
```

#### Option B: Console Version
```bash
# Use the console batch file
run-console.bat

# OR manually run console mode
dotnet run -- console
```

### 3. Start F1 2024 and Begin Driving

The console will display "Waiting for telemetry data..." until you start driving in the game. Once you're on track, you'll see real-time telemetry data.

## Displayed Data

### Motion Data
- World position (X, Y, Z coordinates)
- Velocity in all directions
- G-forces (lateral, longitudinal, vertical)
- Car rotation (yaw, pitch, roll in degrees)

### Session Information
- Current track name and length
- Session type (Practice, Qualifying, Race, etc.)
- Weather conditions and temperatures
- Remaining session time
- Safety car status

### Lap Data
- Current position and lap number
- Last lap time and current lap time
- Sector times
- Lap distance and total distance
- Pit status and pit stop count
- Penalties and warnings

### Track Position (WPF Dashboard Only)
- **Live track position bar** with sector markers
- **Mini track map** showing car location on simplified circuit layout
- **Lap progress percentage** and distance remaining
- **Current sector indicator** with color-coded status
- **Track-specific layouts** for famous circuits (Monaco, Spa, Silverstone, etc.)

### Car Telemetry
- Speed in km/h
- Throttle, brake, and steering input percentages
- Current gear and RPM
- DRS status
- Engine temperature
- Brake temperatures (all four wheels)
- Tyre surface temperatures and pressures

### Car Status
- Fuel level and remaining laps
- Traction control and ABS settings
- Fuel mix setting and brake bias
- DRS availability
- Tyre compound and age
- ERS energy store and deployment mode

## Controls

- **ESC**: Exit the application

## Troubleshooting

### "Waiting for telemetry data..." Message
This usually means:
1. F1 2024 UDP telemetry is not enabled
2. Incorrect IP address configuration
3. Firewall blocking UDP packets on port 20777
4. You're not actively driving (telemetry only sends during timed laps)

### No Data Displayed
- Ensure you're driving in a timed session (not in menus or garage)
- Check that the UDP port 20777 is not blocked by firewall
- Verify the IP address in F1 2024 telemetry settings

### Network Setup
- If running on the same PC as the game: use `127.0.0.1`
- If running on different PCs: ensure both are on the same network
- Xbox/PlayStation: connect console to same network as PC running this app

## Technical Details

- **UDP Port**: 20777 (configurable in game settings)
- **Packet Format**: F1 2024 format (little-endian)
- **Supported Packets**: Motion, Session, Lap Data, Car Telemetry, Car Status
- **Framework**: .NET 8.0
- **Real-time Processing**: Asynchronous UDP packet handling

## Data Format

This application implements the official F1 2024 UDP specification as documented by Codemasters/EA. All packet structures are accurately mapped to handle the binary telemetry data.

## Future Enhancements

Potential improvements for this basic telemetry receiver:
- Data logging to files
- Additional packet types (Car Damage, Events, etc.)
- Web interface for remote viewing
- Data analysis and visualization
- Export to popular telemetry tools

## License

This project is for educational and personal use. F1 2024 is a trademark of Codemasters/EA Sports. 