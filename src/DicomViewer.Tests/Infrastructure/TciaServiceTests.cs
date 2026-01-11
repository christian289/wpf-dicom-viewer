using System.Net;
using System.Text;
using DicomViewer.Application.DTOs;
using DicomViewer.Application.Interfaces;
using DicomViewer.Infrastructure.Tcia;
using FluentAssertions;
using Moq;
using Moq.Protected;

namespace DicomViewer.Tests.Infrastructure;

/// <summary>
/// TciaService 유닛 테스트
/// TciaService unit tests
/// </summary>
public sealed class TciaServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly ITciaService _sut;

    public TciaServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _sut = new TciaService(_httpClient);
    }

    [Fact]
    public async Task GetCollectionsAsync_ReturnsCollections_WhenApiReturnsData()
    {
        // Arrange
        // 준비
        var jsonResponse = """[{"Collection":"TCGA-LUAD"},{"Collection":"LIDC-IDRI"}]""";
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        // Act
        // 실행
        var result = await _sut.GetCollectionsAsync();

        // Assert
        // 검증
        result.Should().HaveCount(2);
        result[0].Collection.Should().Be("TCGA-LUAD");
        result[1].Collection.Should().Be("LIDC-IDRI");
    }

    [Fact]
    public async Task GetCollectionsAsync_ReturnsEmptyList_WhenApiReturnsEmpty()
    {
        // Arrange
        // 준비
        SetupHttpResponse(HttpStatusCode.OK, "");

        // Act
        // 실행
        var result = await _sut.GetCollectionsAsync();

        // Assert
        // 검증
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPatientsAsync_ReturnsPatients_WhenApiReturnsData()
    {
        // Arrange
        // 준비
        var jsonResponse = """
            [
                {"PatientId":"P001","Collection":"TCGA-LUAD","PatientName":"John Doe"},
                {"PatientId":"P002","Collection":"TCGA-LUAD","PatientName":"Jane Doe"}
            ]
            """;
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        // Act
        // 실행
        var result = await _sut.GetPatientsAsync("TCGA-LUAD");

        // Assert
        // 검증
        result.Should().HaveCount(2);
        result[0].PatientId.Should().Be("P001");
        result[0].PatientName.Should().Be("John Doe");
    }

    [Fact]
    public async Task GetPatientsAsync_ReturnsEmptyList_WhenApiReturnsEmpty()
    {
        // Arrange
        // 준비
        SetupHttpResponse(HttpStatusCode.OK, "");

        // Act
        // 실행
        var result = await _sut.GetPatientsAsync("NonExistentCollection");

        // Assert
        // 검증
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPatientStudiesAsync_ReturnsStudies_WhenApiReturnsData()
    {
        // Arrange
        // 준비
        var jsonResponse = """
            [
                {
                    "StudyInstanceUID":"1.2.3.4",
                    "PatientID":"P001",
                    "PatientName":"John Doe",
                    "StudyDate":"2024-01-01",
                    "StudyDescription":"CT Chest",
                    "SeriesCount":3
                }
            ]
            """;
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        // Act
        // 실행
        var result = await _sut.GetPatientStudiesAsync("TCGA-LUAD", "P001");

        // Assert
        // 검증
        result.Should().HaveCount(1);
        result[0].StudyInstanceUID.Should().Be("1.2.3.4");
        result[0].StudyDescription.Should().Be("CT Chest");
        result[0].SeriesCount.Should().Be(3);
    }

    [Fact]
    public async Task GetSeriesAsync_ReturnsSeries_WhenApiReturnsData()
    {
        // Arrange
        // 준비
        var jsonResponse = """
            [
                {
                    "SeriesInstanceUID":"1.2.3.4.5",
                    "SeriesDescription":"Axial",
                    "Modality":"CT",
                    "BodyPartExamined":"CHEST",
                    "Manufacturer":"GE",
                    "ImageCount":150,
                    "FileSize":50000000
                }
            ]
            """;
        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        // Act
        // 실행
        var result = await _sut.GetSeriesAsync("1.2.3.4");

        // Assert
        // 검증
        result.Should().HaveCount(1);
        result[0].SeriesInstanceUID.Should().Be("1.2.3.4.5");
        result[0].Modality.Should().Be("CT");
        result[0].ImageCount.Should().Be(150);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsTrue_WhenApiIsReachable()
    {
        // Arrange
        // 준비
        SetupHttpResponse(HttpStatusCode.OK, "[]");

        // Act
        // 실행
        var result = await _sut.TestConnectionAsync();

        // Assert
        // 검증
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsFalse_WhenApiIsUnreachable()
    {
        // Arrange
        // 준비
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act
        // 실행
        var result = await _sut.TestConnectionAsync();

        // Assert
        // 검증
        result.Should().BeFalse();
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }
}
