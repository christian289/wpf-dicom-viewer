using DicomViewer.DatasetTool;

using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
Console.CancelKeyPress += cancel;
try
{
    VerificationResult result;
    switch (args)
    {
        case ["collect", var directory]:
            result = await DatasetCollector.CollectAsync(directory, cancellation.Token);
            break;
        case ["verify", var manifestPath]:
            result = DatasetVerifier.Verify(manifestPath);
            break;
        default:
            Console.Error.WriteLine("Usage: collect <empty-dataset-directory> | verify <manifest.json>");
            return 2;
    }

    Console.WriteLine($"Verified {result.FileCount} DICOM files, {result.Bytes} bytes; source/stored pixels and geometry agree.");
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Dataset operation cancelled.");
    return 130;
}
catch (InvalidDataException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
catch (HttpRequestException exception)
{
    Console.Error.WriteLine($"Public source request failed (HTTP {exception.StatusCode?.ToString() ?? "transport error"}).");
    return 1;
}
catch (Exception exception)
{
    // Parser exceptions can include source metadata; never print their messages or datasets.
    Console.Error.WriteLine($"Dataset operation failed ({exception.GetType().Name}); no source metadata was logged.");
    return 1;
}
finally
{
    Console.CancelKeyPress -= cancel;
}