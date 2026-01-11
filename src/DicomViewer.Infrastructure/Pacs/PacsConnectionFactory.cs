namespace DicomViewer.Infrastructure.Pacs;

/// <summary>
/// PACS 연결 타입
/// PACS connection type
/// </summary>
public enum PacsConnectionType
{
    LocalFolder,
    Orthanc
}

/// <summary>
/// PACS 연결 팩토리 인터페이스
/// PACS connection factory interface
/// </summary>
public interface IPacsConnectionFactory
{
    IPacsConnectionService CreateService(PacsConnectionType type, string connectionString);
}

/// <summary>
/// PACS 연결 팩토리 구현
/// PACS connection factory implementation
/// </summary>
public sealed class PacsConnectionFactory(IHttpClientFactory httpClientFactory) : IPacsConnectionFactory
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public IPacsConnectionService CreateService(PacsConnectionType type, string connectionString)
    {
        return type switch
        {
            PacsConnectionType.LocalFolder => new LocalFolderPacsService(connectionString),
            PacsConnectionType.Orthanc => new OrthancPacsService(
                _httpClientFactory.CreateClient(),
                connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "지원하지 않는 PACS 타입입니다. / Unsupported PACS type.")
        };
    }
}
