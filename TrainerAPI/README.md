# TrainerAPI

A .NET Core application that connects to a Suito-T bike trainer via Bluetooth and reads power data in real-time.

## Features

- Bluetooth LE connection to Suito-T trainer
- Real-time power data reading
- Cycling Power Service (CPS) implementation
- Windows 10+ compatible

## Requirements

- Windows 10 or later
- .NET 6.0 SDK
- Suito-T trainer
- Bluetooth enabled computer

## Setup

1. Clone the repository:
```bash
git clone https://github.com/yourusername/TrainerAPI.git
```

2. Navigate to the project directory:
```bash
cd TrainerAPI
```

3. Build the project:
```bash
dotnet build
```

4. Run the application:
```bash
dotnet run
```

## Usage

1. Ensure your Suito-T trainer is powered on and in pairing mode
2. Run the application
3. The program will automatically:
   - Search for the Suito-T trainer
   - Connect to the trainer
   - Subscribe to power measurements
   - Display real-time power data

## Project Structure

- `Program.cs` - Main application entry point
- `TrainerAPI.csproj` - Project configuration file

## License

This project is licensed under the MIT License - see the LICENSE file for details. 