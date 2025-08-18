# Inductobot

A cross-platform .NET MAUI application for direct TCP/IP communication with industrial IoT devices, specifically designed for Wand V3 UAS (Universal Access Service) devices.

## Overview

Inductobot provides a native desktop and mobile application interface for device discovery, connection, and control without requiring a gRPC service backend. It offers direct TCP/IP communication with industrial measurement devices using the same command API as existing Wand V3 systems.

## Features

- **Cross-Platform**: Built with .NET MAUI for Windows, macOS, iOS, and Android
- **Direct TCP/IP Communication**: No intermediate gRPC service layer
- **Device Discovery**: Automatic network scanning and manual device addition
- **Real-time Control**: Device commands, measurements, and configuration
- **Compatible API**: Uses same command structure as existing Wand V3 systems
- **Modern UI**: Responsive interface similar to Wandv3TestBench

## Requirements

- .NET 9.0 SDK
- Visual Studio 2022 with MAUI workload
- Windows 10 version 19041 or higher (for Windows development)

## Getting Started

### Installation

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd Inductobot
   ```

2. Install .NET MAUI workload:
   ```bash
   dotnet workload install maui
   ```

3. Build the project:
   ```bash
   dotnet build --framework net9.0-windows10.0.19041.0
   ```

### Running the Application

#### Using Visual Studio
1. Open `Inductobot.sln` in Visual Studio 2022
2. Press F5 or click Run to build and launch

#### Using .NET CLI
```bash
dotnet run --framework net9.0-windows10.0.19041.0
```

## Usage

### UAS-WAND Connection
1. Launch the application
2. Navigate to the "UAS-WAND Connection" tab
3. Use "Scan Network" to discover UAS devices automatically
4. Or manually add devices using IP address and port
5. Click "Connect" to establish TCP/IP connection

### UAS-WAND Control
1. Switch to the "UAS-WAND Control" tab after connecting
2. Available operations:
   - **Device Information**: Get device details and status
   - **Measurements**: Start/stop scans, get live readings
   - **WiFi Configuration**: Configure device network settings
   - **Keep Alive**: Maintain connection with device

## Architecture

### Key Components

- **ByteSnapTcpClient**: Core TCP/IP communication client
- **DeviceDiscoveryService**: Network scanning and device management
- **UASDeviceInfo**: Device information and status model
- **UAS-WAND Connection Page**: UI for device discovery and connection
- **UAS-WAND Control Page**: UI for device operations and commands

### Communication Protocol

The application uses direct TCP/IP sockets to communicate with UAS devices using HTTP-style endpoints:

- `/info` - Device information
- `/scan` - Start/stop measurements
- `/measurement` - Get measurement data
- `/live` - Live reading data
- `/wifi` - WiFi configuration
- `/ping` - Keep-alive
- `/sleep` - Device sleep

## Development

### Project Structure

```
Inductobot/
├── Services/
│   ├── Communication/     # TCP/IP and device communication
│   ├── Device/           # Device discovery and management
│   └── Core/             # Core services and messaging
├── Models/
│   ├── Commands/         # Command request/response models
│   ├── Device/          # Device information models
│   └── Measurements/    # Measurement data models
├── Views/               # XAML UI pages
├── ViewModels/          # MVVM view models
└── Converters/          # XAML value converters
```

### Key Services

- `ITcpDeviceService` - Device connection management
- `IDeviceDiscoveryService` - Network device discovery
- `ByteSnapTcpClient` - Low-level TCP communication

## Troubleshooting

### Common Issues

1. **Build Errors**: Ensure .NET 9.0 SDK and MAUI workload are installed
2. **Device Discovery**: Check network connectivity and firewall settings
3. **Connection Failures**: Verify device IP addresses and ports are correct

### Logging

The application uses Microsoft.Extensions.Logging for debug output. Enable debug logging to troubleshoot connection issues.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make changes and add tests
4. Submit a pull request

## License

[Add your license information here]

## Related Projects

This application is inspired by and compatible with:
- **WandV3GrpcService**: Backend gRPC service for Wand V3 devices
- **Wandv3TestBench**: Blazor WebAssembly test application

The UI design and command API maintain compatibility with these existing systems while providing a native cross-platform experience.