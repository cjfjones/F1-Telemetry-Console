using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using F1_Telemetry_Console.Models;

namespace F1_Telemetry_Console
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private UdpClient? _udpClient;
        private bool _isRunning;
        private readonly DispatcherTimer _uiUpdateTimer;
        private readonly DispatcherTimer _connectionTimer;
        private int _packetCount;
        private DateTime _lastDataReceived;

        // Current telemetry data
        private CarMotionData? _currentMotion;
        private PacketSessionData? _currentSession;
        private LapData? _currentLapData;
        private CarTelemetryData? _currentTelemetry;
        private CarStatusData? _currentStatus;
        private byte _playerCarIndex;
        
        // Track history data
        private readonly List<Point> _positionHistory = new List<Point>();
        private readonly List<DateTime> _positionTimes = new List<DateTime>();
        private readonly List<byte> _positionLaps = new List<byte>(); // Track which lap each point belongs to
        private readonly Dictionary<byte, Polyline> _lapTrails = new Dictionary<byte, Polyline>(); // Separate trail for each lap
        private bool _showHistory = true;
        private const int MaxHistoryPoints = 1000; // Maximum number of history points to keep
        private const double HistoryDistanceThreshold = 2.0; // Minimum distance between points to add to history
        
        // Zoom and pan functionality
        private ScaleTransform? _scaleTransform;
        private TranslateTransform? _translateTransform;
        private TransformGroup? _transformGroup;
        private double _zoomFactor = 1.0;
        private Point _lastPanPoint;
        private bool _isPanning = false;
        
        // Lap-based color cycling
        private byte _currentLapNumber = 1;
        private readonly Dictionary<byte, uint> _completedLapTimes = new Dictionary<byte, uint>();
        
        // Lap restart detection
        private float _lastLapDistance = 0f;
        private bool _isLapRestarting = false;
        private const float LapRestartTeleportThreshold = 1000f; // Minimum distance jump to detect restart teleport (in meters)
        private const float LapStartThreshold = 100f; // Distance threshold to consider lap as "started" (in meters)
        
        private readonly Color[] _lapColors = new Color[]
        {
            Color.FromArgb(200, 0, 255, 255),   // Cyan
            Color.FromArgb(200, 255, 0, 255),   // Magenta
            Color.FromArgb(200, 255, 255, 0),   // Yellow
            Color.FromArgb(200, 0, 255, 0),     // Lime Green
            Color.FromArgb(200, 255, 165, 0),   // Orange
            Color.FromArgb(200, 255, 0, 0),     // Red
            Color.FromArgb(200, 0, 191, 255),   // Deep Sky Blue
            Color.FromArgb(200, 255, 20, 147),  // Deep Pink
            Color.FromArgb(200, 50, 205, 50),   // Lime Green
            Color.FromArgb(200, 186, 85, 211),  // Medium Orchid
            Color.FromArgb(200, 255, 140, 0),   // Dark Orange
            Color.FromArgb(200, 30, 144, 255),  // Dodger Blue
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize timers
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 20 FPS update
            };
            _uiUpdateTimer.Tick += UpdateUI;

            _connectionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _connectionTimer.Tick += CheckConnection;
            _connectionTimer.Start();

            // Initialize zoom and pan transforms
            InitializeCanvasTransforms();

            // Initialize UI
            UpdateConnectionStatus("Disconnected");
            StatusText.Text = "Ready - Click Start to begin receiving telemetry data";
        }

        private void InitializeCanvasTransforms()
        {
            // Create transforms for zoom and pan
            _scaleTransform = new ScaleTransform(1.0, 1.0);
            _translateTransform = new TranslateTransform(0, 0);
            _transformGroup = new TransformGroup();
            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_translateTransform);

            // Apply transforms to the canvas
            MiniTrackCanvas.RenderTransform = _transformGroup;
            MiniTrackCanvas.RenderTransformOrigin = new Point(0.5, 0.5);

            // Add event handlers for zoom and pan
            MiniTrackCanvas.MouseWheel += MiniTrackCanvas_MouseWheel;
            MiniTrackCanvas.MouseLeftButtonDown += MiniTrackCanvas_MouseLeftButtonDown;
            MiniTrackCanvas.MouseLeftButtonUp += MiniTrackCanvas_MouseLeftButtonUp;
            MiniTrackCanvas.MouseMove += MiniTrackCanvas_MouseMove;
            MiniTrackCanvas.MouseLeave += MiniTrackCanvas_MouseLeave;
        }

        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                await StopTelemetry();
            }
            else
            {
                await StartTelemetry();
            }
        }

        private async Task StartTelemetry()
        {
            try
            {
                _udpClient = new UdpClient(20777);
                _isRunning = true;
                _packetCount = 0;
                
                // Clear track history for new session
                ClearTrackHistory();
                
                StartStopButton.Content = "Stop";
                UpdateConnectionStatus("Connecting...");
                StatusText.Text = "Starting UDP listener on port 20777...";

                _uiUpdateTimer.Start();
                
                // Start listening in background
                _ = Task.Run(ListenForPacketsAsync);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start telemetry receiver: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                await StopTelemetry();
            }
        }

        private async Task StopTelemetry()
        {
            _isRunning = false;
            _udpClient?.Close();
            _udpClient = null;

            _uiUpdateTimer.Stop();
            
            StartStopButton.Content = "Start";
            UpdateConnectionStatus("Disconnected");
            StatusText.Text = "Telemetry receiver stopped";
        }

        private async Task ListenForPacketsAsync()
        {
            while (_isRunning)
            {
                try
                {
                    var result = await _udpClient!.ReceiveAsync();
                    var data = result.Buffer;

                    if (data.Length >= Marshal.SizeOf<PacketHeader>())
                    {
                        var header = ByteArrayToStructure<PacketHeader>(data);

                        if (header.m_packetFormat == 2024)
                        {
                            _packetCount++;
                            _lastDataReceived = DateTime.Now;
                            _playerCarIndex = header.m_playerCarIndex;

                            await ProcessPacket(header, data);
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = $"Error receiving packet: {ex.Message}";
                    });
                }
            }
        }

        private async Task ProcessPacket(PacketHeader header, byte[] data)
        {
            await Task.Run(() =>
            {
                switch (header.m_packetId)
                {
                    case 0: // Motion
                        if (data.Length >= Marshal.SizeOf<PacketMotionData>())
                        {
                            var motionPacket = ByteArrayToStructure<PacketMotionData>(data);
                            _currentMotion = motionPacket.m_carMotionData[_playerCarIndex];
                            
                            Dispatcher.Invoke(() =>
                            {
                                SessionTimeText.Text = FormatSessionTime(header.m_sessionTime);
                            });
                        }
                        break;
                    case 1: // Session
                        if (data.Length >= Marshal.SizeOf<PacketSessionData>())
                        {
                            _currentSession = ByteArrayToStructure<PacketSessionData>(data);
                        }
                        break;
                    case 2: // Lap Data
                        if (data.Length >= Marshal.SizeOf<PacketLapData>())
                        {
                            var lapPacket = ByteArrayToStructure<PacketLapData>(data);
                            _currentLapData = lapPacket.m_lapData[_playerCarIndex];
                        }
                        break;
                    case 6: // Car Telemetry
                        if (data.Length >= Marshal.SizeOf<PacketCarTelemetryData>())
                        {
                            var telemetryPacket = ByteArrayToStructure<PacketCarTelemetryData>(data);
                            _currentTelemetry = telemetryPacket.m_carTelemetryData[_playerCarIndex];
                        }
                        break;
                    case 7: // Car Status
                        if (data.Length >= Marshal.SizeOf<PacketCarStatusData>())
                        {
                            var statusPacket = ByteArrayToStructure<PacketCarStatusData>(data);
                            _currentStatus = statusPacket.m_carStatusData[_playerCarIndex];
                        }
                        break;
                }
            });
        }

        private void UpdateUI(object? sender, EventArgs e)
        {
            try
            {
                // Update telemetry data
                if (_currentTelemetry.HasValue)
                {
                    var telemetry = _currentTelemetry.Value;
                    
                    // Speed and RPM
                    SpeedText.Text = telemetry.m_speed.ToString();
                    RPMText.Text = telemetry.m_engineRPM.ToString("#,0");
                    
                    // Gear
                    GearText.Text = GetGearDisplay(telemetry.m_gear);
                    
                    // RPM Progress (assuming max RPM of 15000)
                    RPMProgress.Value = (double)telemetry.m_engineRPM / 15000 * 100;
                    
                    // Driver inputs
                    ThrottleBar.Value = telemetry.m_throttle * 100;
                    ThrottleText.Text = $"{telemetry.m_throttle * 100:F1}%";
                    
                    BrakeBar.Value = telemetry.m_brake * 100;
                    BrakeText.Text = $"{telemetry.m_brake * 100:F1}%";
                    
                    SteeringText.Text = $"{telemetry.m_steer * 100:F1}°";
                    
                    // DRS Status
                    DRSIndicator.Background = telemetry.m_drs == 1 ? Brushes.LimeGreen : Brushes.Red;
                    
                    // Engine Temperature
                    EngineTemperatureText.Text = $"{telemetry.m_engineTemperature}°C";
                    
                    // Brake temperatures
                    BrakeTempFrontText.Text = $"{telemetry.m_brakesTemperature[2]}°C / {telemetry.m_brakesTemperature[3]}°C";
                    BrakeTempRearText.Text = $"{telemetry.m_brakesTemperature[0]}°C / {telemetry.m_brakesTemperature[1]}°C";
                    
                    // Tyre temperatures and pressures
                    TyreTempFLText.Text = $"{telemetry.m_tyresSurfaceTemperature[2]}°C";
                    TyreTempFRText.Text = $"{telemetry.m_tyresSurfaceTemperature[3]}°C";
                    TyreTempRLText.Text = $"{telemetry.m_tyresSurfaceTemperature[0]}°C";
                    TyreTempRRText.Text = $"{telemetry.m_tyresSurfaceTemperature[1]}°C";
                    
                    TyrePressureFLText.Text = $"{telemetry.m_tyresPressure[2]:F1} PSI";
                    TyrePressureFRText.Text = $"{telemetry.m_tyresPressure[3]:F1} PSI";
                    TyrePressureRLText.Text = $"{telemetry.m_tyresPressure[0]:F1} PSI";
                    TyrePressureRRText.Text = $"{telemetry.m_tyresPressure[1]:F1} PSI";
                }

                // Update G-Forces
                if (_currentMotion.HasValue)
                {
                    var motion = _currentMotion.Value;
                    LateralGText.Text = $"{motion.m_gForceLateral:F2}G";
                    LongitudinalGText.Text = $"{motion.m_gForceLongitudinal:F2}G";
                    VerticalGText.Text = $"{motion.m_gForceVertical:F2}G";
                }

                // Update lap data
                if (_currentLapData.HasValue)
                {
                    var lapData = _currentLapData.Value;
                    PositionText.Text = lapData.m_carPosition.ToString();
                    LapText.Text = lapData.m_currentLapNum.ToString();
                    
                    CurrentLapTimeText.Text = FormatTime(lapData.m_currentLapTimeInMS);
                    LastLapTimeText.Text = FormatTime(lapData.m_lastLapTimeInMS);
                    
                    // Update track position widget
                    UpdateTrackPosition(lapData);
                }

                // Update car status
                if (_currentStatus.HasValue)
                {
                    var status = _currentStatus.Value;
                    
                    // Fuel
                    FuelLevelText.Text = $"{status.m_fuelInTank:F1}L";
                    FuelRemainingText.Text = $"{status.m_fuelRemainingLaps:F1} laps";
                    FuelMixText.Text = GetFuelMix(status.m_fuelMix);
                    
                    // Tyre compound
                    TyreCompoundText.Text = GetTyreCompound(status.m_actualTyreCompound);
                    TyreAgeText.Text = $"({status.m_tyresAgeLaps} laps)";
                    
                    // ERS
                    ERSEnergyText.Text = $"{status.m_ersStoreEnergy / 1000000:F1} MJ";
                    ERSIndicator.Background = status.m_ersDeployMode > 0 ? Brushes.Orange : Brushes.Gray;
                }

                // Update session info
                if (_currentSession.HasValue)
                {
                    var session = _currentSession.Value;
                    
                    TrackNameText.Text = GetTrackName(session.m_trackId);
                    TrackLengthText.Text = $"{session.m_trackLength / 1000.0:F3} km";
                    WeatherText.Text = GetWeatherName(session.m_weather);
                    TemperatureText.Text = $"Air: {session.m_airTemperature}°C Track: {session.m_trackTemperature}°C";
                    SessionTypeText.Text = GetSessionTypeName(session.m_sessionType);
                    SessionTimeLeftText.Text = FormatTimeLeft(session.m_sessionTimeLeft);
                    SafetyCarText.Text = GetSafetyCarStatus(session.m_safetyCarStatus);
                }

                // Update packet count
                PacketCountText.Text = $"Packets: {_packetCount:N0}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"UI Update Error: {ex.Message}";
            }
        }

        private void CheckConnection(object? sender, EventArgs e)
        {
            if (_isRunning)
            {
                var timeSinceLastData = DateTime.Now - _lastDataReceived;
                
                if (timeSinceLastData.TotalSeconds > 5)
                {
                    UpdateConnectionStatus("Waiting for data...");
                    StatusText.Text = "No telemetry data received. Make sure F1 2024 UDP is enabled and you're driving.";
                }
                else if (_packetCount > 0)
                {
                    UpdateConnectionStatus("Connected");
                    StatusText.Text = $"Receiving telemetry data - {_packetCount:N0} packets received";
                }
            }
        }

        private void UpdateConnectionStatus(string status)
        {
            ConnectionStatusText.Text = status;
            ConnectionStatusText.Foreground = status switch
            {
                "Connected" => Brushes.LimeGreen,
                "Connecting..." or "Waiting for data..." => Brushes.Orange,
                _ => Brushes.Red
            };
        }

        // Helper methods (same as console version)
        private static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        private static string FormatTime(uint milliseconds)
        {
            if (milliseconds == 0) return "N/A";
            var timespan = TimeSpan.FromMilliseconds(milliseconds);
            return $"{timespan.Minutes:D2}:{timespan.Seconds:D2}.{timespan.Milliseconds:D3}";
        }

        private static string FormatSessionTime(float seconds)
        {
            var timespan = TimeSpan.FromSeconds(seconds);
            return $"{timespan.Hours:D2}:{timespan.Minutes:D2}:{timespan.Seconds:D2}.{timespan.Milliseconds:D3}";
        }

        private static string FormatTimeLeft(ushort seconds)
        {
            if (seconds == 0) return "Unlimited";
            var timespan = TimeSpan.FromSeconds(seconds);
            return $"{timespan.Minutes:D2}:{timespan.Seconds:D2} remaining";
        }

        private static string GetGearDisplay(sbyte gear) => gear switch
        {
            0 => "N",
            -1 => "R",
            _ => gear.ToString()
        };

        private static string GetWeatherName(byte weather) => weather switch
        {
            0 => "Clear",
            1 => "Light Cloud",
            2 => "Overcast",
            3 => "Light Rain",
            4 => "Heavy Rain",
            5 => "Storm",
            _ => "Unknown"
        };

        private static string GetSessionTypeName(byte sessionType) => sessionType switch
        {
            0 => "Unknown",
            1 => "Practice 1",
            2 => "Practice 2",
            3 => "Practice 3",
            4 => "Short Practice",
            5 => "Qualifying 1",
            6 => "Qualifying 2",
            7 => "Qualifying 3",
            8 => "Short Qualifying",
            9 => "One-Shot Qualifying",
            15 => "Race",
            18 => "Time Trial",
            _ => $"Session {sessionType}"
        };

        private static string GetTrackName(sbyte trackId) => trackId switch
        {
            0 => "Melbourne",
            1 => "Paul Ricard",
            2 => "Shanghai",
            3 => "Sakhir (Bahrain)",
            4 => "Catalunya",
            5 => "Monaco",
            6 => "Montreal",
            7 => "Silverstone",
            8 => "Hockenheim",
            9 => "Hungaroring",
            10 => "Spa",
            11 => "Monza",
            12 => "Singapore",
            13 => "Suzuka",
            14 => "Abu Dhabi",
            15 => "Texas",
            16 => "Brazil",
            17 => "Austria",
            18 => "Sochi",
            19 => "Mexico",
            20 => "Baku",
            21 => "Sakhir Short",
            22 => "Silverstone Short",
            23 => "Texas Short",
            24 => "Suzuka Short",
            25 => "Hanoi",
            26 => "Zandvoort",
            27 => "Imola",
            28 => "Portimão",
            29 => "Jeddah",
            30 => "Miami",
            31 => "Las Vegas",
            32 => "Losail",
            _ => $"Track {trackId}"
        };

        private static string GetSafetyCarStatus(byte status) => status switch
        {
            0 => "No Safety Car",
            1 => "Full Safety Car",
            2 => "Virtual Safety Car",
            3 => "Formation Lap Safety Car",
            _ => "Unknown"
        };

        private static string GetFuelMix(byte mix) => mix switch
        {
            0 => "Lean",
            1 => "Standard",
            2 => "Rich",
            3 => "Max",
            _ => "Unknown"
        };

        private static string GetTyreCompound(byte compound) => compound switch
        {
            16 => "C5 (Soft)",
            17 => "C4",
            18 => "C3",
            19 => "C2",
            20 => "C1 (Hard)",
            21 => "C0",
            7 => "Intermediate",
            8 => "Wet",
            _ => $"Compound {compound}"
        };

        private void UpdateTrackPosition(LapData lapData)
        {
            try
            {
                // Calculate lap progress percentage
                var trackLength = _currentSession?.m_trackLength ?? 5000; // Default 5km if unknown
                var lapDistance = Math.Max(0, lapData.m_lapDistance);
                var lapProgress = Math.Min(100, (lapDistance / trackLength) * 100);
                
                // Update progress text
                LapDistanceText.Text = $"{lapDistance / 1000:F3} km";
                LapProgressText.Text = $"{lapProgress:F1}%";
                DistanceRemainingText.Text = $"{(trackLength - lapDistance) / 1000:F3} km";
                
                // Update sector information
                var currentSector = lapData.m_sector + 1; // Convert 0-based to 1-based
                CurrentSectorText.Text = currentSector.ToString();
                
                // Color code the sector indicator
                CurrentSectorIndicator.Background = currentSector switch
                {
                    1 => Brushes.LimeGreen,
                    2 => Brushes.Orange,
                    3 => Brushes.Red,
                    _ => (SolidColorBrush)FindResource("F1AccentBrush")
                };
                
                // Update track progress bar
                var progressBarWidth = 200.0; // Approximate width of the progress bar
                TrackProgressBar.Width = (lapProgress / 100) * progressBarWidth;
                
                // Update car position indicator
                var carPosition = (lapProgress / 100) * progressBarWidth;
                var carMargin = new Thickness(Math.Max(0, carPosition - 8), 2, 0, 2); // Center the indicator
                CarPositionIndicator.Margin = carMargin;
                
                // Update mini track map
                UpdateMiniTrackMap(lapProgress, currentSector);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Track Position Update Error: {ex.Message}";
            }
        }
        
        private void UpdateMiniTrackMap(double lapProgress, int currentSector)
        {
            try
            {
                var canvasWidth = MiniTrackCanvas.ActualWidth > 0 ? MiniTrackCanvas.ActualWidth : 200;
                var canvasHeight = MiniTrackCanvas.ActualHeight > 0 ? MiniTrackCanvas.ActualHeight : 240;
                
                // No static track outline - we'll only show the dynamic trail
                
                Point currentPosition;
                
                // Use actual telemetry position data if available
                if (_currentMotion.HasValue)
                {
                    var motion = _currentMotion.Value;
                    currentPosition = MapWorldPositionToCanvas(motion.m_worldPositionX, motion.m_worldPositionZ, canvasWidth, canvasHeight);
                    
                    // Update car dot position (will be adjusted for size later)
                    Canvas.SetLeft(CarDot, currentPosition.X);
                    Canvas.SetTop(CarDot, currentPosition.Y);
                }
                else
                {
                    // Fallback to lap progress if no motion data available
                    var angle = (lapProgress / 100) * 2 * Math.PI - Math.PI / 2;
                    var centerX = canvasWidth / 2;
                    var centerY = canvasHeight / 2;
                    var radius = Math.Min(centerX, centerY) * 0.9;
                    
                    var carX = centerX + radius * Math.Cos(angle);
                    var carY = centerY + radius * Math.Sin(angle);
                    
                    currentPosition = new Point(carX, carY);
                    
                    Canvas.SetLeft(CarDot, carX);
                    Canvas.SetTop(CarDot, carY);
                }
                
                // Check for lap change
                if (_currentLapData.HasValue)
                {
                    var lapData = _currentLapData.Value;
                    var newLapNumber = lapData.m_currentLapNum;
                    
                    if (newLapNumber != _currentLapNumber && newLapNumber > 0)
                    {
                        // Store the completed lap time (use the lastLapTime which is for the lap we just finished)
                        // But only if we're not in the middle of a lap restart
                        if (lapData.m_lastLapTimeInMS > 0 && _currentLapNumber > 0 && !_isLapRestarting)
                        {
                            _completedLapTimes[_currentLapNumber] = lapData.m_lastLapTimeInMS;
                            UpdateLapTimesDisplay();
                        }
                        
                        _currentLapNumber = newLapNumber;
                        // No need to update existing trails - they keep their colors
                    }
                }
                
                // Update position history
                UpdatePositionHistory(currentPosition);
                
                // Scale car dot size with zoom level
                var scaledCarSize = 8.0 / Math.Max(_zoomFactor, 0.5);
                CarDot.Width = scaledCarSize;
                CarDot.Height = scaledCarSize;
                
                // Center the car dot at its position
                var halfSize = scaledCarSize / 2;
                Canvas.SetLeft(CarDot, Canvas.GetLeft(CarDot) - halfSize);
                Canvas.SetTop(CarDot, Canvas.GetTop(CarDot) - halfSize);
                
                // Color the car dot based on sector
                CarDot.Fill = currentSector switch
                {
                    1 => Brushes.LimeGreen,
                    2 => Brushes.Orange,
                    3 => Brushes.Red,
                    _ => Brushes.Yellow
                };
            }
            catch (Exception ex)
            {
                // Mini map update is non-critical, just log the error
                StatusText.Text = $"Mini Map Update Error: {ex.Message}";
            }
        }
        
        private void UpdatePositionHistory(Point currentPosition)
        {
            try
            {
                var currentTime = DateTime.Now;
                
                // Check for lap restart teleport using lap distance
                if (_currentLapData.HasValue)
                {
                    var lapData = _currentLapData.Value;
                    var currentLapDistance = lapData.m_lapDistance;
                    
                    // Detect sudden jump in lap distance (indicating restart lap teleport)
                    if (!_isLapRestarting && _lastLapDistance > 0 && 
                        (currentLapDistance - _lastLapDistance) > LapRestartTeleportThreshold)
                    {
                        _isLapRestarting = true;
                        StatusText.Text = "Lap restart detected - pausing trail recording";
                        
                        // Remove the current lap's trail since it's being restarted
                        if (_lapTrails.ContainsKey(_currentLapNumber))
                        {
                            MiniTrackCanvas.Children.Remove(_lapTrails[_currentLapNumber]);
                            _lapTrails.Remove(_currentLapNumber);
                        }
                        
                        // Remove position history for current lap to start fresh
                        for (int i = _positionLaps.Count - 1; i >= 0; i--)
                        {
                            if (_positionLaps[i] == _currentLapNumber)
                            {
                                _positionHistory.RemoveAt(i);
                                _positionTimes.RemoveAt(i);
                                _positionLaps.RemoveAt(i);
                            }
                        }
                    }
                    
                    // Detect when lap properly starts (low lap distance after restart)
                    if (_isLapRestarting && currentLapDistance < LapStartThreshold)
                    {
                        _isLapRestarting = false;
                        StatusText.Text = "Lap started - resuming trail recording";
                    }
                    
                    _lastLapDistance = currentLapDistance;
                }
                
                // Don't record positions during lap restart teleportation
                if (_isLapRestarting)
                {
                    return;
                }
                
                // Check if we should add this position to history
                bool shouldAddPosition = false;
                
                if (_positionHistory.Count == 0)
                {
                    shouldAddPosition = true;
                }
                else
                {
                    var lastPosition = _positionHistory[_positionHistory.Count - 1];
                    var distance = Math.Sqrt(Math.Pow(currentPosition.X - lastPosition.X, 2) + 
                                           Math.Pow(currentPosition.Y - lastPosition.Y, 2));
                    
                    // Only add if the car has moved enough distance
                    if (distance >= HistoryDistanceThreshold)
                    {
                        shouldAddPosition = true;
                    }
                }
                
                if (shouldAddPosition)
                {
                    _positionHistory.Add(currentPosition);
                    _positionTimes.Add(currentTime);
                    _positionLaps.Add(_currentLapNumber);
                    
                    // Remove old points if we have too many
                    while (_positionHistory.Count > MaxHistoryPoints)
                    {
                        var removedLap = _positionLaps[0];
                        _positionHistory.RemoveAt(0);
                        _positionTimes.RemoveAt(0);
                        _positionLaps.RemoveAt(0);
                        
                        // If this was the last point of a lap, remove that lap's trail
                        if (!_positionLaps.Contains(removedLap) && _lapTrails.ContainsKey(removedLap))
                        {
                            MiniTrackCanvas.Children.Remove(_lapTrails[removedLap]);
                            _lapTrails.Remove(removedLap);
                        }
                    }
                    
                    // Update the visual trail for current lap
                    UpdateLapTrail(_currentLapNumber);
                }
                
                // Clean up old positions (older than 5 minutes)
                CleanupOldPositions(currentTime);
            }
            catch (Exception ex)
            {
                // History update is non-critical
                StatusText.Text = $"History Update Error: {ex.Message}";
            }
        }
        
        private void UpdateLapTrail(byte lapNumber)
        {
            try
            {
                if (!_showHistory) return;
                
                // Get all points for this specific lap
                var lapPoints = new List<Point>();
                for (int i = 0; i < _positionHistory.Count; i++)
                {
                    if (_positionLaps[i] == lapNumber)
                    {
                        lapPoints.Add(_positionHistory[i]);
                    }
                }
                
                // Remove existing trail for this lap if present
                if (_lapTrails.ContainsKey(lapNumber))
                {
                    MiniTrackCanvas.Children.Remove(_lapTrails[lapNumber]);
                    _lapTrails.Remove(lapNumber);
                }
                
                // Only create new trail if we have enough points for this lap
                if (lapPoints.Count > 1)
                {
                    // Scale trail thickness inversely with zoom to maintain consistent appearance
                    var scaledThickness = 1.0 / Math.Max(_zoomFactor, 0.5);
                    
                    // Get color for this lap (cycle through available colors)
                    var colorIndex = (lapNumber - 1) % _lapColors.Length;
                    var lapColor = _lapColors[colorIndex];
                    
                    var lapTrail = new Polyline
                    {
                        Stroke = new SolidColorBrush(lapColor),
                        StrokeThickness = scaledThickness,
                        Points = new PointCollection(lapPoints)
                    };
                    
                    // Add the trail behind the car dot (so car appears on top)
                    MiniTrackCanvas.Children.Insert(Math.Max(0, MiniTrackCanvas.Children.Count - 1), lapTrail);
                    _lapTrails[lapNumber] = lapTrail;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Trail Update Error: {ex.Message}";
            }
        }
        
        private void CleanupOldPositions(DateTime currentTime)
        {
            try
            {
                var cutoffTime = currentTime.AddMinutes(-5); // Keep last 5 minutes of history
                int removeCount = 0;
                
                for (int i = 0; i < _positionTimes.Count; i++)
                {
                    if (_positionTimes[i] < cutoffTime)
                    {
                        removeCount++;
                    }
                    else
                    {
                        break;
                    }
                }
                
                if (removeCount > 0)
                {
                    // Track which laps will be removed
                    var removedLaps = new HashSet<byte>();
                    for (int i = 0; i < removeCount; i++)
                    {
                        removedLaps.Add(_positionLaps[i]);
                    }
                    
                    _positionHistory.RemoveRange(0, removeCount);
                    _positionTimes.RemoveRange(0, removeCount);
                    _positionLaps.RemoveRange(0, removeCount);
                    
                    // Remove trails for laps that no longer have any points
                    foreach (var lapNumber in removedLaps)
                    {
                        if (!_positionLaps.Contains(lapNumber) && _lapTrails.ContainsKey(lapNumber))
                        {
                            MiniTrackCanvas.Children.Remove(_lapTrails[lapNumber]);
                            _lapTrails.Remove(lapNumber);
                        }
                    }
                    
                    // Update remaining lap trails
                    var remainingLaps = _positionLaps.Distinct().ToArray();
                    foreach (var lapNumber in remainingLaps)
                    {
                        UpdateLapTrail(lapNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                // Cleanup is non-critical
            }
        }
        
        private void UpdateLapTimesDisplay()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    LapTimesPanel.Children.Clear();
                    
                    if (_completedLapTimes.Count == 0)
                    {
                        var placeholderText = new TextBlock
                        {
                            Text = "Complete a lap to see times",
                            Opacity = 0.6,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 20, 0, 0)
                        };
                        placeholderText.SetResourceReference(TextBlock.StyleProperty, "DataTextStyle");
                        LapTimesPanel.Children.Add(placeholderText);
                        return;
                    }
                    
                    // Find fastest lap for comparison
                    var fastestTime = _completedLapTimes.Values.Min();
                    
                    foreach (var lapTime in _completedLapTimes.OrderBy(x => x.Key))
                    {
                        var lapNumber = lapTime.Key;
                        var timeMs = lapTime.Value;
                        
                        // Get the color for this lap
                        var colorIndex = (lapNumber - 1) % _lapColors.Length;
                        var lapColor = _lapColors[colorIndex];
                        
                        // Create a clickable border for the entire lap entry
                        var lapBorder = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(30, lapColor.R, lapColor.G, lapColor.B)), // Semi-transparent background
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(5, 2, 5, 2),
                            Margin = new Thickness(0, 1, 0, 1),
                            Cursor = System.Windows.Input.Cursors.Hand
                        };
                        
                        // Create a grid for lap number and time
                        var lapGrid = new Grid();
                        lapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                        lapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        lapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                        
                        // Lap number
                        var lapNumberText = new TextBlock
                        {
                            Text = $"L{lapNumber}:",
                            Foreground = new SolidColorBrush(lapColor),
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(0, 0, 5, 0)
                        };
                        lapNumberText.SetResourceReference(TextBlock.StyleProperty, "DataTextStyle");
                        Grid.SetColumn(lapNumberText, 0);
                        
                        // Lap time with fastest indicator
                        var timeText = FormatTime(timeMs);
                        if (timeMs == fastestTime)
                        {
                            timeText += " ⭐"; // Star for fastest lap
                        }
                        
                        var lapTimeText = new TextBlock
                        {
                            Text = timeText,
                            Foreground = new SolidColorBrush(lapColor),
                            HorizontalAlignment = HorizontalAlignment.Right
                        };
                        lapTimeText.SetResourceReference(TextBlock.StyleProperty, "DataTextStyle");
                        Grid.SetColumn(lapTimeText, 1);
                        
                        // Delete indicator
                        var deleteText = new TextBlock
                        {
                            Text = "✖",
                            Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                            FontSize = 10,
                            Margin = new Thickness(5, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        Grid.SetColumn(deleteText, 2);
                        
                        lapGrid.Children.Add(lapNumberText);
                        lapGrid.Children.Add(lapTimeText);
                        lapGrid.Children.Add(deleteText);
                        
                        lapBorder.Child = lapGrid;
                        
                        // Add click handler to remove this lap
                        lapBorder.MouseLeftButtonUp += (s, e) => RemoveLap(lapNumber);
                        
                        // Add hover effects
                        lapBorder.MouseEnter += (s, e) =>
                        {
                            lapBorder.Background = new SolidColorBrush(Color.FromArgb(60, lapColor.R, lapColor.G, lapColor.B));
                            deleteText.Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
                        };
                        lapBorder.MouseLeave += (s, e) =>
                        {
                            lapBorder.Background = new SolidColorBrush(Color.FromArgb(30, lapColor.R, lapColor.G, lapColor.B));
                            deleteText.Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
                        };
                        
                        LapTimesPanel.Children.Add(lapBorder);
                    }
                    
                    // Auto-scroll to bottom to show latest lap
                    LapTimesScrollViewer.ScrollToBottom();
                });
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Lap Times Update Error: {ex.Message}";
            }
        }
        
        private void RemoveLap(byte lapNumber)
        {
            try
            {
                // Remove the lap time
                _completedLapTimes.Remove(lapNumber);
                
                // Remove the trail from canvas
                if (_lapTrails.ContainsKey(lapNumber))
                {
                    MiniTrackCanvas.Children.Remove(_lapTrails[lapNumber]);
                    _lapTrails.Remove(lapNumber);
                }
                
                // Remove all position points for this lap
                for (int i = _positionLaps.Count - 1; i >= 0; i--)
                {
                    if (_positionLaps[i] == lapNumber)
                    {
                        _positionHistory.RemoveAt(i);
                        _positionTimes.RemoveAt(i);
                        _positionLaps.RemoveAt(i);
                    }
                }
                
                // Update the display
                UpdateLapTimesDisplay();
                
                StatusText.Text = $"Removed lap {lapNumber} and its trail";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Remove Lap Error: {ex.Message}";
            }
        }
        
        private void ClearTrackHistory()
        {
            _positionHistory.Clear();
            _positionTimes.Clear();
            _positionLaps.Clear();
            _currentLapNumber = 1; // Reset lap counter
            _completedLapTimes.Clear();
            
            // Reset lap restart detection
            _lastLapDistance = 0f;
            _isLapRestarting = false;
            
            // Remove all lap trails from canvas
            foreach (var trail in _lapTrails.Values)
            {
                MiniTrackCanvas.Children.Remove(trail);
            }
            _lapTrails.Clear();
            
            // Update lap times display
            UpdateLapTimesDisplay();
        }
        


        private Point MapWorldPositionToCanvas(float worldX, float worldZ, double canvasWidth, double canvasHeight)
        {
            // Simple mapping - we'll need to calibrate this based on actual track data
            // For now, map world coordinates to canvas with some scaling and centering
            
            var centerX = canvasWidth / 2;
            var centerY = canvasHeight / 2;
            
            // Scale factor - increased to make the trail bigger and more visible
            var scale = 0.08; // Larger scale to make the car trail more prominent
            
            // Map world X,Z to canvas X,Y (top-down view)
            // Note: Z in F1 is forward/backward, X is left/right
            var canvasX = centerX + (worldX * scale);
            var canvasY = centerY - (worldZ * scale); // Negative Z because canvas Y increases downward
            
            return new Point(canvasX, canvasY);
        }

        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            ClearTrackHistory();
            StatusText.Text = "Track history cleared";
        }

        private void ResetViewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_scaleTransform != null && _translateTransform != null)
                {
                    _zoomFactor = 1.0;
                    _scaleTransform.ScaleX = 1.0;
                    _scaleTransform.ScaleY = 1.0;
                    _translateTransform.X = 0;
                    _translateTransform.Y = 0;
                    StatusText.Text = "View reset to default";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Reset View Error: {ex.Message}";
            }
        }
        
        private void ShowHistoryCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            _showHistory = ShowHistoryCheckBox.IsChecked ?? false;
            
            if (_showHistory)
            {
                // Redraw all lap trails
                var activeLaps = _positionLaps.Distinct().ToArray();
                foreach (var lapNumber in activeLaps)
                {
                    UpdateLapTrail(lapNumber);
                }
            }
            else
            {
                // Hide all trails
                foreach (var trail in _lapTrails.Values)
                {
                    MiniTrackCanvas.Children.Remove(trail);
                }
                _lapTrails.Clear();
            }
        }

        private void MiniTrackCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                var canvas = sender as Canvas;
                if (canvas == null || _scaleTransform == null || _translateTransform == null) return;

                var delta = e.Delta > 0 ? 1.2 : 0.8;
                var newZoom = _zoomFactor * delta;

                // Limit zoom range
                if (newZoom < 0.5) newZoom = 0.5;
                if (newZoom > 50.0) newZoom = 50.0;

                // Get mouse position relative to canvas
                var mousePos = e.GetPosition(canvas);
                var canvasCenter = new Point(canvas.ActualWidth / 2, canvas.ActualHeight / 2);
                
                // Calculate adjustment to keep zoom centered on mouse position
                var deltaZoom = newZoom / _zoomFactor;
                var offsetX = (mousePos.X - canvasCenter.X) * (deltaZoom - 1);
                var offsetY = (mousePos.Y - canvasCenter.Y) * (deltaZoom - 1);
                
                // Apply zoom and adjust translation
                _zoomFactor = newZoom;
                _scaleTransform.ScaleX = _zoomFactor;
                _scaleTransform.ScaleY = _zoomFactor;
                _translateTransform.X -= offsetX;
                _translateTransform.Y -= offsetY;
                
                // Update trail thickness for all laps
                var activeLaps = _lapTrails.Keys.ToArray();
                foreach (var lapNumber in activeLaps)
                {
                    UpdateLapTrail(lapNumber);
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Zoom Error: {ex.Message}";
            }
        }

        private void MiniTrackCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var canvas = sender as Canvas;
                if (canvas == null) return;

                _isPanning = true;
                _lastPanPoint = e.GetPosition(canvas);
                canvas.CaptureMouse();
                canvas.Cursor = System.Windows.Input.Cursors.Hand;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Pan Start Error: {ex.Message}";
            }
        }

        private void MiniTrackCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var canvas = sender as Canvas;
                if (canvas == null) return;

                _isPanning = false;
                canvas.ReleaseMouseCapture();
                canvas.Cursor = System.Windows.Input.Cursors.Arrow;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Pan End Error: {ex.Message}";
            }
        }

        private void MiniTrackCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (!_isPanning || _translateTransform == null) return;

                var canvas = sender as Canvas;
                if (canvas == null) return;

                var currentPoint = e.GetPosition(canvas);
                var deltaX = currentPoint.X - _lastPanPoint.X;
                var deltaY = currentPoint.Y - _lastPanPoint.Y;

                // Increase panning sensitivity for ultra-responsive movement
                var panSensitivity = 5.0;
                _translateTransform.X += deltaX * panSensitivity;
                _translateTransform.Y += deltaY * panSensitivity;

                _lastPanPoint = currentPoint;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Pan Move Error: {ex.Message}";
            }
        }

        private void MiniTrackCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            try
            {
                var canvas = sender as Canvas;
                if (canvas == null) return;

                _isPanning = false;
                canvas.ReleaseMouseCapture();
                canvas.Cursor = System.Windows.Input.Cursors.Arrow;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Pan Leave Error: {ex.Message}";
            }
        }

        protected override async void OnClosing(CancelEventArgs e)
        {
            if (_isRunning)
            {
                await StopTelemetry();
            }
            base.OnClosing(e);
        }
    }
} 