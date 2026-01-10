using FileTransferTool.Services;
using FileTransferTool.UI;

var console = new ConsoleUI();
console.Run();

if (!string.IsNullOrWhiteSpace(console.DestinationDirectory))
{
    try
    {
        var fileTransferService = new FileTransferService();
        await fileTransferService.TransferFileAsync(console.SourcePath, console.DestinationDirectory);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n File transfer failed: {ex.Message}");
    }
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();