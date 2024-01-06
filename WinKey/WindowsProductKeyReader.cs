using Microsoft.Win32;
using System.Collections;
using TextCopy;


class WindowsProductKeyReader
{
    static void Main()
    {
        string[] spinner = new string[] { "|", "/", "-", "\\" };
        int counter = 0;
        int delay = 200; // Verzögerung in Millisekunden
        int totalDuration = 2000; // Gesamtdauer in Millisekunden (2 Sekunden)
        int iterations = totalDuration / delay; // Anzahl der Iterationen

        string windowsProductKey = RetrieveWindowsProductKeyFromRegistry();
        Console.WriteLine(windowsProductKey);

        Console.WriteLine("Press Enter to copy the key, or any other key to exit the program.");

        var keyInfo = Console.ReadKey();
        if (keyInfo.Key == ConsoleKey.Enter)
        {
            ClipboardService.SetText(windowsProductKey);
            Console.WriteLine("Key copied!");
            
            for (int i = 0; i < iterations; i++)
            {
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(spinner[counter % spinner.Length]);
                Console.ResetColor();
                Console.Write("]");

                Thread.Sleep(delay);

                if (counter % spinner.Length == spinner.Length - 1)
                {
                    Console.Write("\r");
                }
                else
                {
                    Console.Write("\b\b\b");
                }

                counter++;
            }
        }
    }

    public static string RetrieveWindowsProductKeyFromRegistry()
    {
        var registryBaseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
        var digitalProductIdValue = registryBaseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")?.GetValue("DigitalProductId");

        if (digitalProductIdValue == null)
            return "Failed to get DigitalProductId from registry";

        var digitalProductId = (byte[])digitalProductIdValue;
        registryBaseKey.Close();

        var isWindows8OrNewer = Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 2 || Environment.OSVersion.Version.Major > 6;
        return DecodeWindowsProductKey(digitalProductId, isWindows8OrNewer ? ProductIdVersion.Windows8AndNewer : ProductIdVersion.UpToWindows7);
    }

    public static string DecodeWindowsProductKey(byte[] digitalProductId, ProductIdVersion productIdVersion)
    {
        return productIdVersion == ProductIdVersion.Windows8AndNewer
               ? DecodeProductKeyForWindows8AndNewer(digitalProductId)
               : DecodeProductKeyForOlderWindows(digitalProductId);
    }

    private static string DecodeProductKeyForOlderWindows(byte[] digitalProductId)
    {
        const int startIndex = 52;
        const int endIndex = startIndex + 15;
        var characterSet = new[] { 'B', 'C', 'D', 'F', 'G', 'H', 'J', 'K', 'M', 'P', 'Q', 'R', 'T', 'V', 'W', 'X', 'Y', '2', '3', '4', '6', '7', '8', '9', };
        const int decodeLength = 29;
        const int decodeStringLength = 15;
        var decodedCharacters = new char[decodeLength];
        var hexPid = new ArrayList();

        for (var i = startIndex; i <= endIndex; i++)
        {
            hexPid.Add(digitalProductId[i]);
        }

        for (var i = decodeLength - 1; i >= 0; i--)
        {
            if ((i + 1) % 6 == 0)
            {
                decodedCharacters[i] = '-';
            }
            else
            {
                var digitMapIndex = 0;
                for (var j = decodeStringLength - 1; j >= 0; j--)
                {
                    var byteValue = (digitMapIndex << 8) | (byte)hexPid[j];
                    hexPid[j] = (byte)(byteValue / 24);
                    digitMapIndex = byteValue % 24;
                    decodedCharacters[i] = characterSet[digitMapIndex];
                }
            }
        }

        return new string(decodedCharacters);
    }

    public static string DecodeProductKeyForWindows8AndNewer(byte[] digitalProductId)
    {
        var productKey = String.Empty;
        const int offset = 52;
        var isWindows8 = (byte)((digitalProductId[66] / 6) & 1);
        digitalProductId[66] = (byte)((digitalProductId[66] & 0xf7) | (isWindows8 & 2) * 4);
        const string characterSet = "BCDFGHJKMPQRTVWXY2346789";
        var lastCharacterIndex = 0;

        for (var i = 24; i >= 0; i--)
        {
            var current = 0;
            for (var j = 14; j >= 0; j--)
            {
                current = current * 256;
                current = digitalProductId[j + offset] + current;
                digitalProductId[j + offset] = (byte)(current / 24);
                current = current % 24;
                lastCharacterIndex = current;
            }
            productKey = characterSet[current] + productKey;
        }

        var keyPart1 = productKey.Substring(1, lastCharacterIndex);
        var keyPart2 = productKey.Substring(lastCharacterIndex + 1, productKey.Length - (lastCharacterIndex + 1));
        productKey = keyPart1 + "N" + keyPart2;

        for (var i = 5; i < productKey.Length; i += 6)
        {
            productKey = productKey.Insert(i, "-");
        }

        return productKey;
    }

    public enum ProductIdVersion
    {
        UpToWindows7,
        Windows8AndNewer
    }
}
