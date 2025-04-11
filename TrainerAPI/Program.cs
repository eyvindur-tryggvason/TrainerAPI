using System;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace TrainerAPI
{
    class Program
    {
        // Cycling Power Service UUIDs
        private static readonly Guid CYCLING_POWER_SERVICE_UUID = new Guid("00001818-0000-1000-8000-00805f9b34fb");
        private static readonly Guid CYCLING_POWER_MEASUREMENT_CHARACTERISTIC_UUID = new Guid("00002A63-0000-1000-8000-00805f9b34fb");
        private static readonly Guid CYCLING_POWER_CONTROL_POINT_CHARACTERISTIC_UUID = new Guid("00002A66-0000-1000-8000-00805f9b34fb");

        static async Task Main(string[] args)
        {
            Console.WriteLine("Searching for Suito-T trainer...");
            
            try
            {
                // Query for all Bluetooth LE devices
                string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };
                string aqsFilter = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";
                
                DeviceInformationCollection devices = null;
                int retryCount = 0;
                const int maxRetries = 5;  // Increased retries

                while (devices == null || devices.Count == 0 && retryCount < maxRetries)
                {
                    Console.WriteLine($"Scanning for devices (attempt {retryCount + 1}/{maxRetries})...");
                    devices = await DeviceInformation.FindAllAsync(
                        aqsFilter,
                        requestedProperties,
                        DeviceInformationKind.AssociationEndpoint);
                    
                    if (devices.Count == 0)
                    {
                        retryCount++;
                        Console.WriteLine("No devices found, waiting 3 seconds before retry...");
                        await Task.Delay(3000); // Increased delay to 3 seconds
                    }
                    else
                    {
                        Console.WriteLine($"Found {devices.Count} Bluetooth LE devices:");
                        foreach (var device in devices)
                        {
                            Console.WriteLine($"- {device.Name}");
                        }
                    }
                }

                BluetoothLEDevice trainerDevice = null;
                foreach (DeviceInformation device in devices)
                {
                    // Elite trainers might show up as "ELITE_" or just "Suito"
                    if (device.Name?.Contains("ELITE_", StringComparison.OrdinalIgnoreCase) == true ||
                        device.Name?.Contains("Suito", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        Console.WriteLine($"\nFound potential trainer: {device.Name}");
                        Console.WriteLine($"Device ID: {device.Id}");
                        Console.WriteLine("Attempting to connect...");
                        
                        try
                        {
                            trainerDevice = await BluetoothLEDevice.FromIdAsync(device.Id);
                            if (trainerDevice != null)
                            {
                                Console.WriteLine($"Connection status: {trainerDevice.ConnectionStatus}");
                                
                                // Try to get services to verify it's really connected
                                var initialServicesResult = await trainerDevice.GetGattServicesAsync();
                                if (initialServicesResult.Status == GattCommunicationStatus.Success)
                                {
                                    Console.WriteLine("Successfully connected to device!");
                                    break;
                                }
                                else
                                {
                                    Console.WriteLine($"Failed to get services: {initialServicesResult.Status}");
                                    trainerDevice.Dispose();
                                    trainerDevice = null;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error connecting to device: {ex.Message}");
                            if (trainerDevice != null)
                            {
                                trainerDevice.Dispose();
                                trainerDevice = null;
                            }
                        }
                    }
                }

                if (trainerDevice == null)
                {
                    Console.WriteLine("Suito-T trainer not found. Please ensure it's powered on and in pairing mode.");
                    return;
                }

                Console.WriteLine("Connected to Suito-T trainer. Reading power data...");

                if (trainerDevice != null)
                {
                    Console.WriteLine($"Device Information:");
                    Console.WriteLine($"- Name: {trainerDevice.Name}");
                    Console.WriteLine($"- Connection Status: {trainerDevice.ConnectionStatus}");
                    Console.WriteLine($"- Device Id: {trainerDevice.DeviceId}");
                    Console.WriteLine($"- Bluetooth Address: {trainerDevice.BluetoothAddress}");
                }

                // Get the Cycling Power Service
                GattDeviceServicesResult servicesResult = await trainerDevice.GetGattServicesAsync();
                if (servicesResult.Status != GattCommunicationStatus.Success)
                {
                    Console.WriteLine("Failed to get services.");
                    return;
                }

                GattDeviceService powerService = null;
                foreach (GattDeviceService service in servicesResult.Services)
                {
                    if (service.Uuid == CYCLING_POWER_SERVICE_UUID)
                    {
                        powerService = service;
                        break;
                    }
                }

                if (powerService == null)
                {
                    Console.WriteLine("Cycling Power Service not found.");
                    return;
                }

                // Get the Power Measurement Characteristic
                GattCharacteristicsResult characteristicsResult = await powerService.GetCharacteristicsAsync();
                if (characteristicsResult.Status != GattCommunicationStatus.Success)
                {
                    Console.WriteLine("Failed to get characteristics.");
                    return;
                }

                GattCharacteristic powerMeasurementCharacteristic = null;
                foreach (GattCharacteristic characteristic in characteristicsResult.Characteristics)
                {
                    if (characteristic.Uuid == CYCLING_POWER_MEASUREMENT_CHARACTERISTIC_UUID)
                    {
                        powerMeasurementCharacteristic = characteristic;
                        break;
                    }
                }

                if (powerMeasurementCharacteristic == null)
                {
                    Console.WriteLine("Power Measurement Characteristic not found.");
                    return;
                }

                // Subscribe to power measurement notifications
                GattCommunicationStatus status = await powerMeasurementCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);

                if (status != GattCommunicationStatus.Success)
                {
                    Console.WriteLine("Failed to subscribe to power measurements.");
                    return;
                }

                powerMeasurementCharacteristic.ValueChanged += PowerMeasurementCharacteristic_ValueChanged;

                Console.WriteLine("Successfully subscribed to power measurements. Press any key to exit...");
                Console.ReadKey();

                // Cleanup
                powerMeasurementCharacteristic.ValueChanged -= PowerMeasurementCharacteristic_ValueChanged;
                trainerDevice.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static void PowerMeasurementCharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // Read the power data
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            reader.ByteOrder = ByteOrder.LittleEndian;

            // The first two bytes contain flags, we'll skip them
            reader.ReadUInt16();

            // The next two bytes contain the power value in watts
            ushort power = reader.ReadUInt16();

            Console.WriteLine($"Current Power: {power} watts");

            // TODO: Send power data to your API endpoint
            // You would implement your API call here
        }
    }
}
