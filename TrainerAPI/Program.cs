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
        private static readonly Guid CYCLING_POWER_MEASUREMENT_CHARACTERISTIC_UUID = new Guid("00002a63-0000-1000-8000-00805f9b34fb");

        static async Task Main(string[] args)
        {
            Console.WriteLine("Searching for Suito-T trainer...");
            
            try
            {
                // Query for Bluetooth LE devices
                string selector = BluetoothLEDevice.GetDeviceSelector();
                DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(selector);

                BluetoothLEDevice trainerDevice = null;
                foreach (DeviceInformation device in devices)
                {
                    Console.WriteLine($"Found device: {device.Name}");
                    if (device.Name.Contains("Suito-T", StringComparison.OrdinalIgnoreCase))
                    {
                        trainerDevice = await BluetoothLEDevice.FromIdAsync(device.Id);
                        break;
                    }
                }

                if (trainerDevice == null)
                {
                    Console.WriteLine("Suito-T trainer not found. Please ensure it's powered on and in pairing mode.");
                    return;
                }

                Console.WriteLine("Connected to Suito-T trainer. Reading power data...");

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
