namespace FileTransferTool.UI
{
    public class ConsoleUI
    {
        public string SourcePath { get; private set; } = string.Empty;

        public string DestinationDirectory { get; private set; } = string.Empty;

        public void Run()
        {
            Console.WriteLine("File Transfer Tool");

            SourcePath = GetSourceFilePath();

            while (true)
            {
                string? destination = GetDestinationDirectory();

                if (string.IsNullOrWhiteSpace(destination))
                {
                    Console.WriteLine("Please enter a valid destination directory");
                    continue;
                }

                DestinationValidationResult result = ValidateDestination(destination, out string validatedDestination);

                if (result == DestinationValidationResult.Cancelled)
                {
                    Console.WriteLine("File transfer cancelled.");
                    return;
                }

                if (result == DestinationValidationResult.Valid)
                {
                    DestinationDirectory = validatedDestination;
                    break;
                }
            }

            string destinationPath = Path.Combine(DestinationDirectory!, Path.GetFileName(SourcePath));

            DisplayFileInfo(SourcePath, destinationPath);

            Console.WriteLine("Source and destination verified.");
        }


        private string GetSourceFilePath()
        {
            while (true)
            {
                Console.WriteLine("Enter the file path:");

                string path = Console.ReadLine()?.Trim('"').Replace('/', '\\') ?? string.Empty;

                if (File.Exists(path))
                {
                    return path;
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    Console.WriteLine("Please enter the right path!");
                }
                else
                {
                    Console.WriteLine("The file does not exist. Please try again.");
                }
            }
        }

        private string? GetDestinationDirectory()
        {
            Console.WriteLine("Enter the destination directory path:");
            return Console.ReadLine()?.Trim('"').Replace('/', '\\');
        }

        private void DisplayFileInfo(string source, string destination)
        {
            FileInfo fileInfo = new FileInfo(source);

            Console.WriteLine($"Source file: {source}");
            Console.WriteLine($"Destination file: {destination}");
            Console.WriteLine($"File size: {fileInfo.Length:N0} bytes ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)");
        }

        private DestinationValidationResult ValidateDestination(string path, out string validatedPath)
        {
            validatedPath = string.Empty;

            if (!Directory.Exists(path))
            {
                Console.WriteLine("The destination directory does not exist. Please try again.");
                return DestinationValidationResult.Invalid;
            }

            string intendedDestPath = Path.Combine(Path.GetFullPath(path), Path.GetFileName(SourcePath));
            string normalizedSourcePath = Path.GetFullPath(SourcePath);

            if (string.Equals(normalizedSourcePath, intendedDestPath, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Destination file cannot be the same as the source file. Please choose another folder.");
                return DestinationValidationResult.Invalid;
            }

            if (File.Exists(intendedDestPath) && !ConfirmOverwrite(intendedDestPath))
            {
                return DestinationValidationResult.Cancelled;
            }

            validatedPath = path;
            return DestinationValidationResult.Valid;
        }


        private bool ConfirmOverwrite(string destinationPath)
        {
            while (true)
            {
                Console.WriteLine($"File already exists at destination:\n{destinationPath}\nDo you want to overwrite it? (YES/NO)");

                string? input = Console.ReadLine()?.Trim().ToUpperInvariant();

                bool? result = input switch
                {
                    "Y" or "YES" => true,
                    "N" or "NO" => false,
                    _ => null
                };

                if (result.HasValue)
                {
                    return result.Value;
                }

                Console.WriteLine("Please enter YES or NO");
            }
        }
    }
}