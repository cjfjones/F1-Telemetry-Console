using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
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

            // Initialize UI
            UpdateConnectionStatus("Disconnected");
            StatusText.Text = "Ready - Click Start to begin receiving telemetry data";
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
                var canvasHeight = MiniTrackCanvas.ActualHeight > 0 ? MiniTrackCanvas.ActualHeight : 80;
                
                // Create a simple track outline based on current track
                var trackId = _currentSession?.m_trackId ?? -1;
                UpdateTrackOutline(trackId, canvasWidth, canvasHeight);
                
                // Use actual telemetry position data if available
                if (_currentMotion.HasValue)
                {
                    var motion = _currentMotion.Value;
                    var carPosition = MapWorldPositionToCanvas(motion.m_worldPositionX, motion.m_worldPositionZ, canvasWidth, canvasHeight);
                    
                    // Update car dot position
                    Canvas.SetLeft(CarDot, Math.Max(0, Math.Min(canvasWidth - 6, carPosition.X - 3)));
                    Canvas.SetTop(CarDot, Math.Max(0, Math.Min(canvasHeight - 6, carPosition.Y - 3)));
                }
                else
                {
                    // Fallback to lap progress if no motion data available
                    var angle = (lapProgress / 100) * 2 * Math.PI - Math.PI / 2;
                    var centerX = canvasWidth / 2;
                    var centerY = canvasHeight / 2;
                    var radius = Math.Min(centerX, centerY) * 0.7;
                    
                    var carX = centerX + radius * Math.Cos(angle);
                    var carY = centerY + radius * Math.Sin(angle);
                    
                    Canvas.SetLeft(CarDot, Math.Max(0, Math.Min(canvasWidth - 6, carX - 3)));
                    Canvas.SetTop(CarDot, Math.Max(0, Math.Min(canvasHeight - 6, carY - 3)));
                }
                
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
        
        private void UpdateTrackOutline(sbyte trackId, double canvasWidth, double canvasHeight)
        {
            try
            {
                var geometry = new PathGeometry();
                var figure = new PathFigure();
                
                var centerX = canvasWidth / 2;
                var centerY = canvasHeight / 2;
                var radius = Math.Min(centerX, centerY) * 0.7;
                
                // Create different track shapes based on the actual track
                switch (trackId)
                {
                    case 5: // Monaco - tight and twisty
                        CreateMonacoTrackOutline(figure, centerX, centerY, radius);
                        break;
                    case 10: // Spa - long with chicanes
                        CreateSpaTrackOutline(figure, centerX, centerY, radius);
                        break;
                    case 11: // Monza - oval-ish with chicanes
                        CreateMonzaTrackOutline(figure, centerX, centerY, radius);
                        break;
                    case 7: // Silverstone - flowing corners
                        CreateSilverstoneTrackOutline(figure, centerX, centerY, radius);
                        break;
                    default: // Generic oval/circular track
                        CreateGenericTrackOutline(figure, centerX, centerY, radius);
                        break;
                }
                
                geometry.Figures.Add(figure);
                TrackOutline.Data = geometry;
            }
            catch (Exception)
            {
                // If track outline update fails, use a simple circle
                var geometry = new EllipseGeometry(new Point(canvasWidth / 2, canvasHeight / 2), 
                    canvasWidth * 0.35, canvasHeight * 0.35);
                TrackOutline.Data = geometry;
            }
        }
        
        private void CreateGenericTrackOutline(PathFigure figure, double centerX, double centerY, double radius)
        {
            // Simple oval track
            figure.StartPoint = new Point(centerX, centerY - radius);
            figure.Segments.Add(new ArcSegment(
                new Point(centerX, centerY + radius), 
                new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new ArcSegment(
                new Point(centerX, centerY - radius), 
                new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            figure.IsClosed = true;
        }
        
        private void CreateMonacoTrackOutline(PathFigure figure, double centerX, double centerY, double radius)
        {
            // Monaco - rectangular with tight corners
            var width = radius * 1.2;
            var height = radius * 0.8;
            figure.StartPoint = new Point(centerX - width/2, centerY - height/2);
            figure.Segments.Add(new LineSegment(new Point(centerX + width/2, centerY - height/2), true));
            figure.Segments.Add(new LineSegment(new Point(centerX + width/2, centerY + height/2), true));
            figure.Segments.Add(new LineSegment(new Point(centerX - width/2, centerY + height/2), true));
            figure.IsClosed = true;
        }
        
        private void CreateSpaTrackOutline(PathFigure figure, double centerX, double centerY, double radius)
        {
            // Spa - elongated with chicanes
            figure.StartPoint = new Point(centerX, centerY - radius);
            figure.Segments.Add(new BezierSegment(
                new Point(centerX + radius * 1.5, centerY - radius * 0.5),
                new Point(centerX + radius * 1.5, centerY + radius * 0.5),
                new Point(centerX, centerY + radius), true));
            figure.Segments.Add(new BezierSegment(
                new Point(centerX - radius * 1.5, centerY + radius * 0.5),
                new Point(centerX - radius * 1.5, centerY - radius * 0.5),
                new Point(centerX, centerY - radius), true));
            figure.IsClosed = true;
        }
        
        private void CreateMonzaTrackOutline(PathFigure figure, double centerX, double centerY, double radius)
        {
            // Monza - oval with chicanes
            var width = radius * 1.4;
            var height = radius * 0.9;
            figure.StartPoint = new Point(centerX, centerY - height);
            figure.Segments.Add(new ArcSegment(
                new Point(centerX + width, centerY), 
                new Size(width, height), 0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new ArcSegment(
                new Point(centerX, centerY + height), 
                new Size(width, height), 0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new ArcSegment(
                new Point(centerX - width, centerY), 
                new Size(width, height), 0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new ArcSegment(
                new Point(centerX, centerY - height), 
                new Size(width, height), 0, false, SweepDirection.Clockwise, true));
            figure.IsClosed = true;
        }
        
        private void CreateSilverstoneTrackOutline(PathFigure figure, double centerX, double centerY, double radius)
        {
            // Silverstone - flowing S-curves
            figure.StartPoint = new Point(centerX, centerY - radius);
            figure.Segments.Add(new BezierSegment(
                new Point(centerX + radius * 0.8, centerY - radius * 0.3),
                new Point(centerX + radius * 0.8, centerY + radius * 0.3),
                new Point(centerX, centerY + radius), true));
            figure.Segments.Add(new BezierSegment(
                new Point(centerX - radius * 0.8, centerY + radius * 0.3),
                new Point(centerX - radius * 0.8, centerY - radius * 0.3),
                new Point(centerX, centerY - radius), true));
            figure.IsClosed = true;
        }

        private Point MapWorldPositionToCanvas(float worldX, float worldZ, double canvasWidth, double canvasHeight)
        {
            // Simple mapping - we'll need to calibrate this based on actual track data
            // For now, map world coordinates to canvas with some scaling and centering
            
            var centerX = canvasWidth / 2;
            var centerY = canvasHeight / 2;
            
            // Scale factor - this may need adjustment per track
            var scale = 0.02; // Start with a small scale since world coordinates can be large
            
            // Map world X,Z to canvas X,Y (top-down view)
            // Note: Z in F1 is forward/backward, X is left/right
            var canvasX = centerX + (worldX * scale);
            var canvasY = centerY - (worldZ * scale); // Negative Z because canvas Y increases downward
            
            return new Point(canvasX, canvasY);
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