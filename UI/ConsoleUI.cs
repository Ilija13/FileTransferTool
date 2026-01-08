namespace FileTransferTool.UI
{
    public class ConsoleUI
    {
        public string SourcePath { get; private set; } = string.Empty;
        public string DestinationDirectory { get; private set; } = string.Empty;

        public void Run()
        {
            Console.WriteLine("File Transfer Tool\n");

            SourcePath = GetSourceFilePath();

            DestinationDirectory = GetDestinationDirectory();

            string destinationPath = Path.Combine(DestinationDirectory, Path.GetFileName(SourcePath));

            DisplayFileInfo(SourcePath, destinationPath);

            Console.WriteLine("\nSource and destination verified.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private string GetSourceFilePath()
        {
            while (true)
            {
                Console.WriteLine("Enter the file path: \n");
                string path = Console.ReadLine()?.Trim('"').Replace('/', '\\') ?? "";

                if (File.Exists(path))
                    return path;

                Console.WriteLine("The file does not exist. Please try again.");
            }
        }

        private string GetDestinationDirectory()
        {
            while (true)
            {
                Console.WriteLine("Enter the destination directory path: ");
                string path = Console.ReadLine()?.Trim('"').Replace('/', '\\') ?? "";

                if (Directory.Exists(path))
                    return path;

                Console.WriteLine("The destination directory does not exist. Please try again.");
            }
        }

        private void DisplayFileInfo(string source, string destination)
        {
            FileInfo fileInfo = new FileInfo(source);
            Console.WriteLine($"\nSource file: {source}");
            Console.WriteLine($"Destination file: {destination}");
            Console.WriteLine($"File size: {fileInfo.Length:N0} bytes ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)");
        }
    }
}