using System.Runtime.InteropServices;

namespace F1_Telemetry_Console.Models;

// F1 2024 UDP Packet Structures
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PacketHeader
{
    public ushort m_packetFormat;          // 2024
    public byte m_gameYear;                // Game year - last two digits e.g. 24
    public byte m_gameMajorVersion;        // Game major version - "X.00"
    public byte m_gameMinorVersion;        // Game minor version - "1.XX"
    public byte m_packetVersion;           // Version of this packet type
    public byte m_packetId;                // Identifier for the packet type
    public ulong m_sessionUID;             // Unique identifier for the session
    public float m_sessionTime;            // Session timestamp
    public uint m_frameIdentifier;         // Identifier for the frame the data was retrieved
    public uint m_overallFrameIdentifier;  // Overall identifier for the frame
    public byte m_playerCarIndex;          // Index of player's car in the array
    public byte m_secondaryPlayerCarIndex; // Index of secondary player's car
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CarMotionData
{
    public float m_worldPositionX;     // World space X position - metres
    public float m_worldPositionY;     // World space Y position
    public float m_worldPositionZ;     // World space Z position
    public float m_worldVelocityX;     // Velocity in world space X – metres/s
    public float m_worldVelocityY;     // Velocity in world space Y
    public float m_worldVelocityZ;     // Velocity in world space Z
    public short m_worldForwardDirX;   // World space forward X direction (normalised)
    public short m_worldForwardDirY;   // World space forward Y direction (normalised)
    public short m_worldForwardDirZ;   // World space forward Z direction (normalised)
    public short m_worldRightDirX;     // World space right X direction (normalised)
    public short m_worldRightDirY;     // World space right Y direction (normalised)
    public short m_worldRightDirZ;     // World space right Z direction (normalised)
    public float m_gForceLateral;      // Lateral G-Force component
    public float m_gForceLongitudinal; // Longitudinal G-Force component
    public float m_gForceVertical;     // Vertical G-Force component
    public float m_yaw;                // Yaw angle in radians
    public float m_pitch;              // Pitch angle in radians
    public float m_roll;               // Roll angle in radians
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PacketMotionData
{
    public PacketHeader m_header;                    // Header
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
    public CarMotionData[] m_carMotionData;          // Data for all cars on track
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MarshalZone
{
    public float m_zoneStart;          // Fraction (0..1) of way through the lap the marshal zone starts
    public sbyte m_zoneFlag;           // -1 = invalid/unknown, 0 = none, 1 = green, 2 = blue, 3 = yellow
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct WeatherForecastSample
{
    public byte m_sessionType;              // 0 = unknown, 1 = P1, 2 = P2, 3 = P3, 4 = Short P, 5 = Q1
    public byte m_timeOffset;               // Time in minutes the forecast is for
    public byte m_weather;                  // Weather - 0 = clear, 1 = light cloud, 2 = overcast
    public sbyte m_trackTemperature;        // Track temp. in degrees Celsius
    public sbyte m_trackTemperatureChange;  // Track temp. change – 0 = up, 1 = down, 2 = no change
    public sbyte m_airTemperature;          // Air temp. in degrees celsius
    public sbyte m_airTemperatureChange;    // Air temp. change – 0 = up, 1 = down, 2 = no change
    public byte m_rainPercentage;           // Rain percentage (0-100)
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PacketSessionData
{
    public PacketHeader m_header;                    // Header
    public byte m_weather;                           // Weather - 0 = clear, 1 = light cloud, 2 = overcast
    public sbyte m_trackTemperature;                 // Track temp. in degrees celsius
    public sbyte m_airTemperature;                   // Air temp. in degrees celsius
    public byte m_totalLaps;                         // Total number of laps in this race
    public ushort m_trackLength;                     // Track length in metres
    public byte m_sessionType;                       // 0 = unknown, 1 = P1, 2 = P2, 3 = P3, 4 = Short P, 5 = Q1
    public sbyte m_trackId;                          // -1 for unknown, 0-31 for tracks
    public byte m_formula;                           // Formula, 0 = F1 Modern, 1 = F1 Classic, 2 = F2
    public ushort m_sessionTimeLeft;                 // Time left in session in seconds
    public ushort m_sessionDuration;                 // Session duration in seconds
    public byte m_pitSpeedLimit;                     // Pit speed limit in kilometres per hour
    public byte m_gamePaused;                        // Whether the game is paused – network game only
    public byte m_isSpectating;                      // Whether the player is spectating
    public byte m_spectatorCarIndex;                 // Index of the car being spectated
    public byte m_sliProNativeSupport;               // SLI Pro support, 0 = inactive, 1 = active
    public byte m_numMarshalZones;                   // Number of marshal zones to follow
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 21)]
    public MarshalZone[] m_marshalZones;             // List of marshal zones – max 21
    public byte m_safetyCarStatus;                   // 0 = no safety car, 1 = full, 2 = virtual, 3 = formation lap
    public byte m_networkGame;                       // 0 = offline, 1 = online
    public byte m_numWeatherForecastSamples;         // Number of weather samples to follow
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public WeatherForecastSample[] m_weatherForecastSamples; // Array of weather forecast samples
    public byte m_forecastAccuracy;                  // 0 = Perfect, 1 = Approximate
    public byte m_aiDifficulty;                      // AI Difficulty rating – 0-110
    public uint m_seasonLinkIdentifier;              // Identifier for season - persists across saves
    public uint m_weekendLinkIdentifier;             // Identifier for weekend - persists across saves
    public uint m_sessionLinkIdentifier;             // Identifier for session - persists across saves
    public byte m_pitStopWindowIdealLap;             // Ideal lap to pit on for current strategy (player)
    public byte m_pitStopWindowLatestLap;            // Latest lap to pit on for current strategy (player)
    public byte m_pitStopRejoinPosition;             // Predicted position to rejoin at (player)
    public byte m_steeringAssist;                    // 0 = off, 1 = on
    public byte m_brakingAssist;                     // 0 = off, 1 = low, 2 = medium, 3 = high
    public byte m_gearboxAssist;                     // 1 = manual, 2 = manual & suggested gear, 3 = auto
    public byte m_pitAssist;                         // 0 = off, 1 = on
    public byte m_pitReleaseAssist;                  // 0 = off, 1 = on
    public byte m_ERSAssist;                         // 0 = off, 1 = on
    public byte m_DRSAssist;                         // 0 = off, 1 = on
    public byte m_dynamicRacingLine;                 // 0 = off, 1 = corners only, 2 = full
    public byte m_dynamicRacingLineType;             // 0 = 2D, 1 = 3D
    public byte m_gameMode;                          // Game mode id - see appendix
    public byte m_ruleSet;                           // Ruleset - see appendix
    public uint m_timeOfDay;                         // Local time of day - minutes since midnight
    public byte m_sessionLength;                     // 0 = None, 2 = Very Short, 3 = Short, 4 = Medium
    public byte m_speedUnitsLeadPlayer;              // 0 = MPH, 1 = KPH
    public byte m_temperatureUnitsLeadPlayer;        // 0 = Celsius, 1 = Fahrenheit
    public byte m_speedUnitsSecondaryPlayer;         // 0 = MPH, 1 = KPH
    public byte m_temperatureUnitsSecondaryPlayer;   // 0 = Celsius, 1 = Fahrenheit
    public byte m_numSafetyCarPeriods;               // Number of safety cars called during session
    public byte m_numVirtualSafetyCarPeriods;        // Number of virtual safety cars called
    public byte m_numRedFlagPeriods;                 // Number of red flags called during session
    public byte m_equalCarPerformance;               // 0 = Off, 1 = On
    public byte m_recoveryMode;                      // 0 = None, 1 = Flashbacks, 2 = Auto-recovery
    public byte m_flashbackLimit;                    // 0 = Low, 1 = Medium, 2 = High, 3 = Unlimited
    public byte m_surfaceType;                       // 0 = Simplified, 1 = Realistic
    public byte m_lowFuelMode;                       // 0 = Easy, 1 = Hard
    public byte m_raceStarts;                        // 0 = Manual, 1 = Assisted
    public byte m_tyreTemperature;                   // 0 = Surface only, 1 = Surface & Carcass
    public byte m_pitLaneTyreSim;                    // 0 = On, 1 = Off
    public byte m_carDamage;                         // 0 = Off, 1 = Reduced, 2 = Standard, 3 = Simulation
    public byte m_carDamageRate;                     // 0 = Reduced, 1 = Standard, 2 = Simulation
    public byte m_collisions;                        // 0 = Off, 1 = Player-to-Player Off, 2 = On
    public byte m_collisionsOffForFirstLapOnly;      // 0 = Disabled, 1 = Enabled
    public byte m_mpUnsafePitRelease;                // 0 = On, 1 = Off (Multiplayer)
    public byte m_mpOffForGriefing;                  // 0 = Disabled, 1 = Enabled (Multiplayer)
    public byte m_cornerCuttingStringency;           // 0 = Regular, 1 = Strict
    public byte m_parcFermeRules;                    // 0 = Off, 1 = On
    public byte m_pitStopExperience;                 // 0 = Automatic, 1 = Broadcast, 2 = Immersive
    public byte m_safetyCar;                         // 0 = Off, 1 = Reduced, 2 = Standard, 3 = Increased
    public byte m_safetyCarExperience;               // 0 = Broadcast, 1 = Immersive
    public byte m_formationLap;                      // 0 = Off, 1 = On
    public byte m_formationLapExperience;            // 0 = Broadcast, 1 = Immersive
    public byte m_redFlags;                          // 0 = Off, 1 = Reduced, 2 = Standard, 3 = Increased
    public byte m_affectsLicenceLevelSolo;           // 0 = Off, 1 = On
    public byte m_affectsLicenceLevelMP;             // 0 = Off, 1 = On
    public byte m_numSessionsInWeekend;              // Number of session in following array
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
    public byte[] m_weekendStructure;                // List of session types to show weekend structure
    public float m_sector2LapDistanceStart;          // Distance in m around track where sector 2 starts
    public float m_sector3LapDistanceStart;          // Distance in m around track where sector 3 starts
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LapData
{
    public uint m_lastLapTimeInMS;                   // Last lap time in milliseconds
    public uint m_currentLapTimeInMS;                // Current time around the lap in milliseconds
    public ushort m_sector1TimeMSPart;               // Sector 1 time milliseconds part
    public byte m_sector1TimeMinutesPart;            // Sector 1 whole minute part
    public ushort m_sector2TimeMSPart;               // Sector 2 time milliseconds part
    public byte m_sector2TimeMinutesPart;            // Sector 2 whole minute part
    public ushort m_deltaToCarInFrontMSPart;         // Time delta to car in front milliseconds part
    public byte m_deltaToCarInFrontMinutesPart;      // Time delta to car in front whole minute part
    public ushort m_deltaToRaceLeaderMSPart;         // Time delta to race leader milliseconds part
    public byte m_deltaToRaceLeaderMinutesPart;      // Time delta to race leader whole minute part
    public float m_lapDistance;                      // Distance vehicle is around current lap in metres
    public float m_totalDistance;                    // Total distance travelled in session in metres
    public float m_safetyCarDelta;                   // Delta in seconds for safety car
    public byte m_carPosition;                       // Car race position
    public byte m_currentLapNum;                     // Current lap number
    public byte m_pitStatus;                         // 0 = none, 1 = pitting, 2 = in pit area
    public byte m_numPitStops;                       // Number of pit stops taken in this race
    public byte m_sector;                            // 0 = sector1, 1 = sector2, 2 = sector3
    public byte m_currentLapInvalid;                 // Current lap invalid - 0 = valid, 1 = invalid
    public byte m_penalties;                         // Accumulated time penalties in seconds to be added
    public byte m_totalWarnings;                     // Accumulated number of warnings issued
    public byte m_cornerCuttingWarnings;             // Accumulated number of corner cutting warnings issued
    public byte m_numUnservedDriveThroughPens;       // Num drive through pens left to serve
    public byte m_numUnservedStopGoPens;             // Num stop go pens left to serve
    public byte m_gridPosition;                      // Grid position the vehicle started the race in
    public byte m_driverStatus;                      // Status of driver - 0 = in garage, 1 = flying lap
    public byte m_resultStatus;                      // Result status - 0 = invalid, 1 = inactive, 2 = active
    public byte m_pitLaneTimerActive;                // Pit lane timing, 0 = inactive, 1 = active
    public ushort m_pitLaneTimeInLaneInMS;           // If active, the current time spent in the pit lane in ms
    public ushort m_pitStopTimerInMS;                // Time of the actual pit stop in ms
    public byte m_pitStopShouldServePen;             // Whether the car should serve a penalty at this stop
    public float m_speedTrapFastestSpeed;            // Fastest speed through speed trap for this car in kmph
    public byte m_speedTrapFastestLap;               // Lap no the fastest speed was achieved, 255 = not set
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PacketLapData
{
    public PacketHeader m_header;                    // Header
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
    public LapData[] m_lapData;                      // Lap data for all cars on track
    public byte m_timeTrialPBCarIdx;                 // Index of Personal Best car in time trial (255 if invalid)
    public byte m_timeTrialRivalCarIdx;              // Index of Rival car in time trial (255 if invalid)
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CarTelemetryData
{
    public ushort m_speed;                           // Speed of car in kilometres per hour
    public float m_throttle;                         // Amount of throttle applied (0.0 to 1.0)
    public float m_steer;                            // Steering (-1.0 (full lock left) to 1.0 (full lock right))
    public float m_brake;                            // Amount of brake applied (0.0 to 1.0)
    public byte m_clutch;                            // Amount of clutch applied (0 to 100)
    public sbyte m_gear;                             // Gear selected (1-8, N=0, R=-1)
    public ushort m_engineRPM;                       // Engine RPM
    public byte m_drs;                               // 0 = off, 1 = on
    public byte m_revLightsPercent;                  // Rev lights indicator (percentage)
    public ushort m_revLightsBitValue;               // Rev lights (bit 0 = leftmost LED, bit 14 = rightmost LED)
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public ushort[] m_brakesTemperature;             // Brakes temperature (celsius)
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public byte[] m_tyresSurfaceTemperature;         // Tyres surface temperature (celsius)
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public byte[] m_tyresInnerTemperature;           // Tyres inner temperature (celsius)
    public ushort m_engineTemperature;               // Engine temperature (celsius)
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] m_tyresPressure;                  // Tyres pressure (PSI)
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public byte[] m_surfaceType;                     // Driving surface, see appendices
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PacketCarTelemetryData
{
    public PacketHeader m_header;                    // Header
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
    public CarTelemetryData[] m_carTelemetryData;
    public byte m_mfdPanelIndex;                     // Index of MFD panel open - 255 = MFD closed
    public byte m_mfdPanelIndexSecondaryPlayer;      // See above
    public sbyte m_suggestedGear;                    // Suggested gear for the player (1-8)
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CarStatusData
{
    public byte m_tractionControl;                   // Traction control - 0 = off, 1 = medium, 2 = full
    public byte m_antiLockBrakes;                    // 0 (off) - 1 (on)
    public byte m_fuelMix;                           // Fuel mix - 0 = lean, 1 = standard, 2 = rich, 3 = max
    public byte m_frontBrakeBias;                    // Front brake bias (percentage)
    public byte m_pitLimiterStatus;                  // Pit limiter status - 0 = off, 1 = on
    public float m_fuelInTank;                       // Current fuel mass
    public float m_fuelCapacity;                     // Fuel capacity
    public float m_fuelRemainingLaps;                // Fuel remaining in terms of laps (value on MFD)
    public ushort m_maxRPM;                          // Cars max RPM, point of rev limiter
    public ushort m_idleRPM;                         // Cars idle RPM
    public byte m_maxGears;                          // Maximum number of gears
    public byte m_drsAllowed;                        // 0 = not allowed, 1 = allowed
    public ushort m_drsActivationDistance;           // 0 = DRS not available, non-zero - DRS will be available
    public byte m_actualTyreCompound;                // F1 Modern - 16 = C5, 17 = C4, 18 = C3, 19 = C2, 20 = C1
    public byte m_visualTyreCompound;                // F1 visual (can be different from actual compound)
    public byte m_tyresAgeLaps;                      // Age in laps of the current set of tyres
    public sbyte m_vehicleFiaFlags;                  // -1 = invalid/unknown, 0 = none, 1 = green
    public float m_enginePowerICE;                   // Engine power output of ICE (W)
    public float m_enginePowerMGUK;                  // Engine power output of MGU-K (W)
    public float m_ersStoreEnergy;                   // ERS energy store in Joules
    public byte m_ersDeployMode;                     // ERS deployment mode, 0 = none, 1 = medium
    public float m_ersHarvestedThisLapMGUK;          // ERS energy harvested this lap by MGU-K
    public float m_ersHarvestedThisLapMGUH;          // ERS energy harvested this lap by MGU-H
    public float m_ersDeployedThisLap;               // ERS energy deployed this lap
    public byte m_networkPaused;                     // Whether the car is paused in a network game
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PacketCarStatusData
{
    public PacketHeader m_header;                    // Header
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
    public CarStatusData[] m_carStatusData;
} 