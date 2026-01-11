using FileTransferTool.Services;
using FileTransferTool.Services.Implementations;
using FileTransferTool.UI;

var console = new ConsoleUI();
console.Run();

if (!string.IsNullOrWhiteSpace(console.DestinationDirectory))
{
    try
    {
        var fileSystem = new FileSystemService();
        var logger = new ConsoleLogger();
        var hashCalculator = new HashCalculator();

        var fileTransferService = new FileTransferService(fileSystem, logger, hashCalculator);

        await fileTransferService.TransferFileAsync(console.SourcePath, console.DestinationDirectory);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n File transfer failed: {ex.Message}");
    }
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();