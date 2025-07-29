# Cymulate.Http.Package.Authentication

A comprehensive authentication library for .NET 8.0 that provides multiple authentication strategies for HTTP requests. This package supports Basic Authentication, API Key Authentication, Bearer Token Authentication, JWT Authentication, OAuth2 Authentication, and No Authentication scenarios.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Authentication Strategies](#authentication-strategies)
- [Usage Examples](#usage-examples)
- [Configuration](#configuration)
- [Advanced Features](#advanced-features)
- [Dependencies](#dependencies)

## Overview

The Authentication package provides a flexible and extensible authentication system for HTTP requests. It follows the Strategy pattern to support multiple authentication methods and includes automatic token refresh capabilities for OAuth2 and JWT authentication.

### Key Features

- **Multiple Authentication Strategies**: Support for Basic, API Key, Bearer, JWT, OAuth2, and No Authentication
- **Automatic Token Refresh**: Built-in support for refreshing OAuth2 and JWT tokens
- **Thread-Safe Operations**: Concurrent token refresh with semaphore-based synchronization
- **Comprehensive Logging**: Detailed logging for debugging and monitoring
- **Validation**: Input validation and error handling
- **Dependency Injection Ready**: Designed to work with Microsoft.Extensions.DependencyInjection

## Architecture

### Core Interfaces

#### `IRequestAuthenticator`
The base interface for all authentication strategies:
```csharp
public interface IRequestAuthenticator
{
    Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
```

#### `IRefreshableAuthenticator`
Extends `IRequestAuthenticator` for strategies that support token refresh:
```csharp
public interface IRefreshableAuthenticator : IRequestAuthenticator
{
    Task RefreshAsync(CancellationToken cancellationToken = default);
    Task<bool> IsValidAsync(CancellationToken cancellationToken = default);
    DateTimeOffset ExpiresAt { get; }
}
```

#### `IAuthenticationProvider`
The main interface for authentication providers that wrap strategies:
```csharp
public interface IAuthenticationProvider : IDisposable
{
    Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
```

### Core Components

#### `AuthenticationProvider`
Wraps authentication strategies and provides:
- Automatic token validation and refresh
- Logging and error handling
- Resource disposal management

#### `AuthenticationOrchestrator`
Factory class that creates authentication providers based on configuration:
```csharp
public static IAuthenticationProvider Create(AuthSelection authSelection, ILoggerFactory loggerFactory, HttpClient? httpClient = null)
```

#### `AuthProviderCollection`
Contains factory methods for building specific authentication providers.

## Authentication Strategies

### 1. Basic Authentication

Authenticates using username and password with Base64 encoding.

**Configuration:**
```csharp
public sealed class BasicAuthenticationConfiguration
{
    public required string Username { get; init; }
    public required string Password { get; init; }
    public bool IgnoreIfAlreadyAuthenticated { get; init; } = true;
}
```

**Usage:**
```csharp
var config = new BasicAuthenticationConfiguration
{
    Username = "myuser",
    Password = "mypassword"
};

var authSelection = new AuthSelection.Basic(config);
var provider = AuthenticationOrchestrator.Create(authSelection, loggerFactory);
```

### 2. API Key Authentication

Supports API key authentication via headers or query parameters.

**Configuration:**
```csharp
public sealed class ApiKeyAuthenticationConfiguration
{
    public string? ApiKey { get; init; }
    public string? SecretKey { get; init; }
    public string? HeaderName { get; init; }
    public string? HeaderPrefix { get; init; }
    public bool UseQueryParameter { get; init; } = false;
    public string? QueryParameterName { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool CombineAsAccessSecretPair { get; init; } = false;
}
```

**Usage Examples:**

**Header-based API Key:**
```csharp
var config = new ApiKeyAuthenticationConfiguration
{
    ApiKey = "your-api-key",
    HeaderName = "X-API-Key"
};

var authSelection = new AuthSelection.ApiKey(config);
var provider = AuthenticationOrchestrator.Create(authSelection, loggerFactory);
```

**Query Parameter API Key:**
```csharp
var config = new ApiKeyAuthenticationConfiguration
{
    ApiKey = "your-api-key",
    UseQueryParameter = true,
    QueryParameterName = "api_key"
};

var authSelection = new AuthSelection.ApiKey(config);
var provider = AuthenticationOrchestrator.Create(authSelection, loggerFactory);
```

**Access/Secret Key Pair:**
```csharp
var config = new ApiKeyAuthenticationConfiguration
{
    ApiKey = "access-key",
    SecretKey = "secret-key",
    HeaderName = "Authorization",
    CombineAsAccessSecretPair = true
};

var authSelection = new AuthSelection.ApiKey(config);
var provider = AuthenticationOrchestrator.Create(authSelection, loggerFactory);
```

### 3. Bearer Token Authentication

Simple bearer token authentication.

**Configuration:**
```csharp
public sealed class BearerAuthenticationConfiguration
{
    public required string AccessToken { get; init; }
    public bool IsEnabled { get; init; } = true;
    public string StrategyType => "Bearer";
}
```

**Usage:**
```csharp
var config = new BearerAuthenticationConfiguration
{
    AccessToken = "your-bearer-token"
};

var authSelection = new AuthSelection.Bearer(config);
var provider = AuthenticationOrchestrator.Create(authSelection, loggerFactory);
```

### 4. JWT Authentication

JWT token authentication with automatic refresh support.

**Configuration:**
```csharp
public sealed class JwtAuthenticationConfiguration
{
    public required string JwtToken { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public Func<CancellationToken, Task<string>>? RefreshTokenAsync { get; set; }
    public TimeSpan DefaultLifetime { get; set; } = TimeSpan.FromHours(1);
}
```

**Usage:**
```csharp
var config = new JwtAuthenticationConfiguration
{
    JwtToken = "your-jwt-token",
    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
    RefreshTokenAsync = async (cancellationToken) =>
    {
        // Implement your token refresh logic here
        return await RefreshJwtTokenAsync(cancellationToken);
    }
};

var authSelection = new AuthSelection.Jwt(config);
var provider = AuthenticationOrchestrator.Create(authSelection, loggerFactory);
```

### 5. OAuth2 Authentication

OAuth2 client credentials flow with automatic token management.

**Configuration:**
```csharp
public sealed class OAuth2AuthenticationConfiguration
{
    public required string TokenEndpoint { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public string? Scope { get; init; }
    public string? CurrentAccessToken { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
```

**Usage:**
```csharp
var config = new OAuth2AuthenticationConfiguration
{
    TokenEndpoint = "https://auth.example.com/oauth/token",
    ClientId = "your-client-id",
    ClientSecret = "your-client-secret",
    Scope = "read write"
};

var authSelection = new AuthSelection.OAuth2(config);
var provider = AuthenticationOrchestrator.Create(authSelection, loggerFactory, httpClient);
```

### 6. No Authentication

For requests that don't require authentication.

**Usage:**
```csharp
var authSelection = new AuthSelection.None;
var provider = AuthenticationOrchestrator.Create(authSelection, loggerFactory);
```

## Usage Examples

### Basic Usage

```csharp
using Authentication.Contracts.Models;
using Authentication.Logic.Services;
using Microsoft.Extensions.Logging;

// Create logger factory
var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

// Configure authentication
var authConfig = new BasicAuthenticationConfiguration
{
    Username = "username",
    Password = "password"
};

var authSelection = new AuthSelection.Basic(authConfig);

// Create authentication provider
var authProvider = AuthenticationOrchestrator.Create(authSelection, loggerFactory);

// Use with HttpClient
using var httpClient = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");

// Authenticate the request
await authProvider.AuthenticateRequestAsync(request);

// Send the request
var response = await httpClient.SendAsync(request);
```

### Advanced Usage with Dependency Injection

```csharp
// Register services
services.AddSingleton<ILoggerFactory>(loggerFactory);
services.AddHttpClient();

// Create authentication provider
var authProvider = AuthenticationOrchestrator.Create(
    new AuthSelection.OAuth2(oauth2Config), 
    loggerFactory, 
    httpClient);

// Use in your application
public class ApiService
{
    private readonly IAuthenticationProvider _authProvider;
    private readonly HttpClient _httpClient;

    public ApiService(IAuthenticationProvider authProvider, HttpClient httpClient)
    {
        _authProvider = authProvider;
        _httpClient = httpClient;
    }

    public async Task<string> GetDataAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
        await _authProvider.AuthenticateRequestAsync(request);
        
        var response = await _httpClient.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }
}
```

### Token Refresh Example

```csharp
// JWT with refresh capability
var jwtConfig = new JwtAuthenticationConfiguration
{
    JwtToken = "initial-token",
    RefreshTokenAsync = async (cancellationToken) =>
    {
        // Call your token refresh endpoint
        var refreshResponse = await httpClient.PostAsync("https://auth.example.com/refresh", null, cancellationToken);
        var newToken = await refreshResponse.Content.ReadAsStringAsync(cancellationToken);
        return newToken;
    }
};

var authProvider = AuthenticationOrchestrator.Create(
    new AuthSelection.Jwt(jwtConfig), 
    loggerFactory);

// The provider will automatically refresh tokens when needed
await authProvider.AuthenticateRequestAsync(request);
```

## Configuration

### AuthSelection Pattern

The library uses a discriminated union pattern with `AuthSelection` to represent different authentication types:

```csharp
public abstract record AuthSelection
{
    public sealed record ApiKey(ApiKeyAuthenticationConfiguration Config) : AuthSelection;
    public sealed record Basic(BasicAuthenticationConfiguration Config) : AuthSelection;
    public sealed record Jwt(JwtAuthenticationConfiguration Config) : AuthSelection;
    public sealed record OAuth2(OAuth2AuthenticationConfiguration Config) : AuthSelection;
    public sealed record Bearer(BearerAuthenticationConfiguration Config) : AuthSelection;
    public sealed record None : AuthSelection;
}
```

### Validation

The library includes comprehensive validation through the `Validate` utility class:

```csharp
// Null checks
var config = Validate.NotNull(authenticationConfig);

// String validation
var token = Validate.NotNullOrWhiteSpace(accessToken);

// Custom validation
var endpoint = Validate.Valid(tokenEndpoint, 
    uri => Uri.IsWellFormedUriString(uri, UriKind.Absolute), 
    "Token endpoint must be a valid absolute URI");
```

## Advanced Features

### Thread-Safe Token Refresh

OAuth2 and JWT authentication strategies implement thread-safe token refresh using `SemaphoreSlim`:

```csharp
private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

public async Task RefreshAsync(CancellationToken cancellationToken = default)
{
    await _refreshSemaphore.WaitAsync(cancellationToken);
    try
    {
        // Token refresh logic
    }
    finally
    {
        _refreshSemaphore.Release();
    }
}
```

### Automatic Token Validation

The `AuthenticationProvider` automatically validates and refreshes tokens before each request:

```csharp
public async Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
{
    if (_refreshable is not null)
    {
        var isValid = await _refreshable.IsValidAsync(cancellationToken);
        if (!isValid)
        {
            await _refreshable.RefreshAsync(cancellationToken);
        }
    }
    
    await _authenticator.AuthenticateRequestAsync(request, cancellationToken);
}
```

### Resource Disposal

All authentication providers implement `IDisposable` to properly clean up resources:

```csharp
public void Dispose()
{
    if (_authenticator is IDisposable disposable)
    {
        disposable.Dispose();
    }
}
```

## Dependencies

- **.NET 8.0**: Target framework
- **Microsoft.Extensions.Logging**: For comprehensive logging support

## Package Information

- **Package ID**: `Cymulate.Http.Package.Authentication`
- **Version**: 1.0.3
- **Authors**: Uri Briskin
- **License**: MIT
- **Repository**: https://github.com/your-org/PowerHttp

## Contributing

This package is part of the Cymulate.Http.Package suite. For contributions, please follow the project's coding standards and ensure all tests pass.

## License

This project is licensed under the MIT License - see the LICENSE file for details. 