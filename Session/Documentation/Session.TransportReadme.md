# Session Transport - Streaming and HTTP Operations

This document covers the transport layer components that handle HTTP request execution and streaming operations. The transport layer provides efficient handling of both regular HTTP requests and large file uploads/downloads.

## Table of Contents

- [Overview](#overview)
- [Transport Service](#transport-service)
- [Request Streaming](#request-streaming)
- [Response Streaming](#response-streaming)
- [Stream Analysis](#stream-analysis)
- [Error Handling](#error-handling)
- [Performance Considerations](#performance-considerations)
- [Use Cases](#use-cases)

## Overview

The transport layer consists of:

- **ExecuteTransportService**: Core service that handles HTTP operations
- **Streaming Operations**: Efficient handling of large request/response bodies
- **Stream Analysis**: Automatic analysis of stream sizes and content
- **Telemetry Integration**: Comprehensive logging and metrics for transport operations

### Key Features

- **🔄 Request Streaming**: Efficient upload of large files
- **📥 Response Streaming**: Memory-efficient download of large responses
- **📊 Stream Analysis**: Automatic size detection and warnings
- **⚡ Performance Optimized**: Optimized for large data transfers
- **🛡️ Error Handling**: Robust error handling with proper cleanup

## Transport Service

The `ExecuteTransportService` is the core component that handles all HTTP operations through the session.

### Service Structure

```csharp
public class ExecuteTransportService
{
    private readonly IHttpSession _session;
    private readonly ILogger? _logger;
    private readonly HttpClient _client;
    private readonly string _authType;

    public IAuthenticationProvider Auth { get; }
    public IDefensivePolicyRunner Policies { get; }
}
```

### Request Execution Flow

```csharp
public async Task<HttpResponseMessage> ExecuteRequestAsync(HttpRequestMessage request, CancellationToken ct = default)
{
    using var activity = SessionDiagnostics.StartSessionActivity(SessionTelemetryConstants.Operations.ExecuteRequest);

    try
    {
        // 1. Update session usage
        _session.UpdateLastUsed();
        
        // 2. Authenticate request
        await Auth.AuthenticateRequestAsync(request, ct);
        
        // 3. Execute with resilience policies
        HttpResponseMessage response;
        using (RequestContextScope.With(request))
        {
            response = await Policies.ExecuteAsync(
                token => _client.SendAsync(request, token),
                ct
            );
        }
        
        // 4. Record telemetry
        SessionDiagnostics.SetTag(activity, SessionTelemetryConstants.Tags.ResponseStatusCode, ((int)response.StatusCode).ToString());
        SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);
        
        return response;
    }
    catch (Exception ex)
    {
        SessionDiagnostics.RecordFailure(activity, ex);
        SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Failure);
        throw;
    }
}
```

## Request Streaming

The `StreamRequestAsync` method efficiently handles large request bodies by streaming them directly to the HTTP client.

### Method Signature

```csharp
public async Task<HttpResponseMessage> StreamRequestAsync(
    HttpRequestMessage request,
    Stream requestBody,
    ILogger? logger = null,
    CancellationToken cancellationToken = default)
```

### Implementation Details

```csharp
public async Task<HttpResponseMessage> StreamRequestAsync(
    HttpRequestMessage request,
    Stream requestBody,
    ILogger? logger = null,
    CancellationToken cancellationToken = default)
{
    using var activity = SessionDiagnostics.StartSessionActivity("StreamRequest");

    try
    {
        // 1. Analyze stream size
        var streamSize = GetStreamSizeIfPossible(requestBody);
        if (streamSize.HasValue)
        {
            SessionDiagnostics.SetTag(activity, "stream.size_bytes", streamSize.Value);
            
            if (streamSize.Value == 0)
            {
                logger?.LogWarning("Empty stream detected for request to {Uri}", request.RequestUri);
            }
            else if (streamSize.Value > 100 * 1024 * 1024) // 100MB
            {
                logger?.LogWarning("Large stream detected: {Size} bytes for request to {Uri}",
                    streamSize.Value, request.RequestUri);
            }
        }

        // 2. Reset stream position if needed
        if (requestBody.CanSeek && requestBody.Position != 0)
        {
            requestBody.Position = 0;
            logger?.LogDebug("Reset stream position to beginning");
        }

        // 3. Attach stream to request
        request.Content = new StreamContent(requestBody);

        // 4. Execute request
        var response = await ExecuteRequestAsync(request, cancellationToken);
        
        SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);
        return response;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Cancelled);
        throw;
    }
    catch (Exception ex)
    {
        SessionDiagnostics.RecordFailure(activity, ex);
        SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Failure);
        throw;
    }
}
```

### Usage Examples

#### File Upload

```csharp
// Upload a large file
await using var file = File.OpenRead("large-dataset.zip");
var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/upload");

var response = await session.StreamRequestAsync(uploadRequest, file);
Console.WriteLine($"Upload completed: {response.StatusCode}");
```

#### Memory Stream Upload

```csharp
// Upload data from memory stream
using var memoryStream = new MemoryStream();
await JsonSerializer.SerializeAsync(memoryStream, dataObject);
memoryStream.Position = 0;

var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/data");
var response = await session.StreamRequestAsync(request, memoryStream);
```

#### Network Stream Upload

```csharp
// Upload from network stream (e.g., from another service)
using var networkStream = await tcpClient.GetStreamAsync();
var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/forward");
var response = await session.StreamRequestAsync(request, networkStream);
```

## Response Streaming

The `StreamResponseAsync` method efficiently handles large response bodies by returning a streamed response object.

### Method Signature

```csharp
public async Task<StreamedResponse> StreamResponseAsync(
    HttpRequestMessage request,
    ILogger? logger = null,
    CancellationToken cancellationToken = default)
```

### StreamedResponse Model

```csharp
public sealed class StreamedResponse
{
    public required HttpResponseMessage RawResponse { get; init; }
    public required Stream ContentStream { get; init; }
    public long? ContentLength => RawResponse.Content.Headers.ContentLength;
    public bool IsSuccessStatusCode => RawResponse.IsSuccessStatusCode;
}
```

### Implementation Details

```csharp
public async Task<StreamedResponse> StreamResponseAsync(
    HttpRequestMessage request,
    ILogger? logger = null,
    CancellationToken cancellationToken = default)
{
    using var activity = SessionDiagnostics.StartSessionActivity("StreamResponse");

    try
    {
        // 1. Execute request
        var response = await ExecuteRequestAsync(request, cancellationToken);
        
        // 2. Analyze response content
        var contentLength = response.Content.Headers.ContentLength;
        var contentType = response.Content.Headers.ContentType?.ToString();

        if (contentLength.HasValue)
        {
            SessionDiagnostics.SetTag(activity, "response.content_length", contentLength.Value);
            
            if (contentLength.Value == 0)
            {
                logger?.LogWarning("Empty response content from {Uri}", request.RequestUri);
            }
            else if (contentLength.Value > 100 * 1024 * 1024) // 100MB
            {
                logger?.LogWarning("Large response detected: {ContentLength} bytes from {Uri}",
                    contentLength.Value, request.RequestUri);
            }
        }

        // 3. Get content stream
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        
        SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);
        
        return new StreamedResponse
        {
            RawResponse = response,
            ContentStream = stream
        };
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Cancelled);
        throw;
    }
    catch (Exception ex)
    {
        SessionDiagnostics.RecordFailure(activity, ex);
        SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Failure);
        throw;
    }
}
```

### Usage Examples

#### File Download

```csharp
// Download a large file
var downloadRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/download/large-file");
var streamed = await session.StreamResponseAsync(downloadRequest);

await using var targetFile = File.Create("downloaded-file.zip");
await streamed.ContentStream.CopyToAsync(targetFile);

Console.WriteLine($"Download completed: {streamed.RawResponse.StatusCode}");
```

#### Memory-Efficient Processing

```csharp
// Process large JSON response without loading into memory
var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/large-dataset");
var streamed = await session.StreamResponseAsync(request);

using var jsonReader = new JsonTextReader(new StreamReader(streamed.ContentStream));
using var jsonSerializer = new JsonSerializer();

// Process JSON stream without loading entire response into memory
while (jsonReader.Read())
{
    if (jsonReader.TokenType == JsonToken.StartObject)
    {
        var item = jsonSerializer.Deserialize<DataItem>(jsonReader);
        ProcessItem(item);
    }
}
```

#### Streaming to Multiple Destinations

```csharp
// Stream response to multiple destinations
var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
var streamed = await session.StreamResponseAsync(request);

// Create multiple streams from the same response
using var file1 = File.Create("backup1.dat");
using var file2 = File.Create("backup2.dat");

// Note: This requires buffering or sequential processing
var buffer = new byte[8192];
int bytesRead;
while ((bytesRead = await streamed.ContentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
{
    await file1.WriteAsync(buffer, 0, bytesRead);
    await file2.WriteAsync(buffer, 0, bytesRead);
}
```

## Stream Analysis

The transport service automatically analyzes streams to provide insights and warnings.

### Stream Size Detection

```csharp
private static long? GetStreamSizeIfPossible(Stream stream)
{
    if (!stream.CanSeek) return null;

    try 
    { 
        return stream.Length; 
    }
    catch 
    { 
        return null; 
    }
}
```

### Analysis Triggers

| Condition | Action | Log Level |
|-----------|--------|-----------|
| Empty stream (0 bytes) | Warning logged | Warning |
| Large stream (>100MB) | Warning logged | Warning |
| Unknown size | Debug info logged | Debug |
| Seekable stream reset | Position reset to 0 | Debug |

### Content Analysis

For response streaming, additional analysis includes:

- **Content Length**: Extracted from HTTP headers
- **Content Type**: MIME type analysis
- **Response Size**: Automatic size warnings
- **Empty Content**: Detection of empty responses

## Error Handling

### Request Streaming Errors

```csharp
try
{
    var response = await session.StreamRequestAsync(request, fileStream);
}
catch (OperationCanceledException ex)
{
    // Request was cancelled
    logger.LogWarning("Request streaming cancelled: {Message}", ex.Message);
}
catch (HttpRequestException ex)
{
    // Network or HTTP errors
    logger.LogError(ex, "Request streaming failed: {Message}", ex.Message);
}
catch (ObjectDisposedException ex)
{
    // Stream was disposed
    logger.LogError(ex, "Stream was disposed during upload");
}
```

### Response Streaming Errors

```csharp
try
{
    var streamed = await session.StreamResponseAsync(request);
}
catch (OperationCanceledException ex)
{
    // Response streaming was cancelled
    logger.LogWarning("Response streaming cancelled: {Message}", ex.Message);
}
catch (HttpRequestException ex)
{
    // Network or HTTP errors
    logger.LogError(ex, "Response streaming failed: {Message}", ex.Message);
}
catch (InvalidOperationException ex)
{
    // Content stream issues
    logger.LogError(ex, "Invalid content stream: {Message}", ex.Message);
}
```

### Resource Cleanup

```csharp
// Proper cleanup for streaming operations
await using var file = File.OpenRead("large-file.zip");
var request = new HttpRequestMessage(HttpMethod.Post, "/upload");

try
{
    var response = await session.StreamRequestAsync(request, file);
    // Process response
}
finally
{
    // File stream is automatically disposed by await using
    // Request and response are disposed by the session
}
```

## Performance Considerations

### Memory Efficiency

- **Streaming vs Buffering**: Always use streaming for large files
- **Memory Pressure**: Avoid loading large responses into memory
- **Buffer Sizes**: Use appropriate buffer sizes for copying operations

### Network Optimization

```csharp
// Optimize for large uploads
var request = new HttpRequestMessage(HttpMethod.Post, "/upload");
request.Headers.Add("Expect", "100-continue"); // For large uploads

// Use compression when appropriate
request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
```

### Concurrent Streaming

```csharp
// Handle multiple concurrent streams
var tasks = new List<Task<HttpResponseMessage>>();

foreach (var file in files)
{
    await using var fileStream = File.OpenRead(file);
    var request = new HttpRequestMessage(HttpMethod.Post, "/upload");
    tasks.Add(session.StreamRequestAsync(request, fileStream));
}

var responses = await Task.WhenAll(tasks);
```

## Use Cases

### 1. File Upload Service

```csharp
public class FileUploadService
{
    private readonly IHttpSession _session;
    
    public async Task<UploadResult> UploadFileAsync(string filePath, string uploadUrl)
    {
        await using var file = File.OpenRead(filePath);
        var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        
        var response = await _session.StreamRequestAsync(request, file);
        
        return new UploadResult
        {
            Success = response.IsSuccessStatusCode,
            StatusCode = response.StatusCode,
            Content = await response.Content.ReadAsStringAsync()
        };
    }
}
```

### 2. Data Pipeline

```csharp
public class DataPipeline
{
    private readonly IHttpSession _session;
    
    public async Task ProcessLargeDatasetAsync(string datasetUrl)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, datasetUrl);
        var streamed = await _session.StreamResponseAsync(request);
        
        using var reader = new StreamReader(streamed.ContentStream);
        string? line;
        
        while ((line = await reader.ReadLineAsync()) != null)
        {
            await ProcessDataLineAsync(line);
        }
    }
}
```

### 3. Backup Service

```csharp
public class BackupService
{
    private readonly IHttpSession _session;
    
    public async Task BackupDatabaseAsync(string backupUrl)
    {
        // Stream database backup to remote storage
        await using var backupStream = await CreateDatabaseBackupStreamAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, backupUrl);
        
        var response = await _session.StreamRequestAsync(request, backupStream);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new BackupFailedException($"Backup failed: {response.StatusCode}");
        }
    }
}
```

### 4. Media Processing

```csharp
public class MediaProcessor
{
    private readonly IHttpSession _session;
    
    public async Task ProcessVideoAsync(string videoUrl)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, videoUrl);
        var streamed = await _session.StreamResponseAsync(request);
        
        // Process video stream without loading into memory
        using var videoProcessor = new VideoProcessor();
        await videoProcessor.ProcessStreamAsync(streamed.ContentStream);
    }
}
```

### 5. Log Aggregation

```csharp
public class LogAggregator
{
    private readonly IHttpSession _session;
    
    public async Task AggregateLogsAsync(string logsUrl)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, logsUrl);
        var streamed = await _session.StreamResponseAsync(request);
        
        using var reader = new StreamReader(streamed.ContentStream);
        var logEntries = new List<LogEntry>();
        
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (LogEntry.TryParse(line, out var entry))
            {
                logEntries.Add(entry);
            }
        }
        
        await ProcessLogEntriesAsync(logEntries);
    }
}
``` 