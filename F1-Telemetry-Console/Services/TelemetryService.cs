using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using F1_Telemetry_Console.Models;

namespace F1_Telemetry_Console.Services;

public class TelemetryService
{
    private UdpClient? _udpClient;
    private bool _isRunning;
    private readonly int _port;

    public event EventHandler<TelemetryDataEventArgs>? TelemetryDataReceived;

    public TelemetryService(int port = 20777)
    {
        _port = port;
    }

    public async Task StartAsync()
    {
        if (_isRunning) return;

        _isRunning = true;
        _udpClient = new UdpClient(_port);

        _ = Task.Run(ListenForPacketsAsync);
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        _udpClient?.Close();
        _udpClient = null;
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

                    // Only process F1 2024 packets
                    if (header.m_packetFormat == 2024)
                    {
                        ProcessPacket(header, data);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Handle error - could raise an event here for UI notification
                Console.WriteLine($"Error receiving packet: {ex.Message}");
            }
        }
    }

    private void ProcessPacket(PacketHeader header, byte[] data)
    {
        var telemetryData = new TelemetryData { Header = header };

        switch (header.m_packetId)
        {
            case 0: // Motion
                if (data.Length >= Marshal.SizeOf<PacketMotionData>())
                {
                    telemetryData.Motion = ByteArrayToStructure<PacketMotionData>(data);
                }
                break;
            case 1: // Session
                if (data.Length >= Marshal.SizeOf<PacketSessionData>())
                {
                    telemetryData.Session = ByteArrayToStructure<PacketSessionData>(data);
                }
                break;
            case 2: // Lap Data
                if (data.Length >= Marshal.SizeOf<PacketLapData>())
                {
                    telemetryData.LapData = ByteArrayToStructure<PacketLapData>(data);
                }
                break;
            case 6: // Car Telemetry
                if (data.Length >= Marshal.SizeOf<PacketCarTelemetryData>())
                {
                    telemetryData.CarTelemetry = ByteArrayToStructure<PacketCarTelemetryData>(data);
                }
                break;
            case 7: // Car Status
                if (data.Length >= Marshal.SizeOf<PacketCarStatusData>())
                {
                    telemetryData.CarStatus = ByteArrayToStructure<PacketCarStatusData>(data);
                }
                break;
        }

        TelemetryDataReceived?.Invoke(this, new TelemetryDataEventArgs(telemetryData));
    }

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
}

public class TelemetryDataEventArgs : EventArgs
{
    public TelemetryData Data { get; }

    public TelemetryDataEventArgs(TelemetryData data)
    {
        Data = data;
    }
}

public class TelemetryData
{
    public PacketHeader Header { get; set; }
    public PacketMotionData? Motion { get; set; }
    public PacketSessionData? Session { get; set; }
    public PacketLapData? LapData { get; set; }
    public PacketCarTelemetryData? CarTelemetry { get; set; }
    public PacketCarStatusData? CarStatus { get; set; }
} 