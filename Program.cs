using FileTransferTool.UI;

var console = new ConsoleUI();
console.Run();

if (!string.IsNullOrWhiteSpace(console.DestinationDirectory))
{
    try
    {
        //// TODO service
        Console.WriteLine("NEsto");
    }
    catch (Exception ex)
    {
        //Console.WriteLine($"\n File transfer failed: {ex.Message}");
    }
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();