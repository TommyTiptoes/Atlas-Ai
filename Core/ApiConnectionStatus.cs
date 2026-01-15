using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasAI.Core
{
    /// <summary>
    /// Connection status for an API provider
    /// </summary>
    public enum ConnectionStatus
    {
        Unknown,        // Not yet tested
        Connected,      // Successfully connected
        Disconnected,   // Failed to connect
        NoApiKey,       // No API key configured
        InvalidKey,     // API key is invalid
        RateLimited,    // Hit rate limit
        Testing         // Currently testing connection
    }
    
    /// <summary>
    /// Manages API connection status and health checks for all providers.
    /// Provides retry logic, connection testing, and status notifications.
    /// </summary>
    public class ApiConnectionStatus
    {
        private static ApiConnectionStatus? _instance;
        public static ApiConnectionStatus Instance => _instance ??= new ApiConnectionStatus();
        
        private readonly Dictionary<string, ConnectionStatus> _statusCache = new();
        private readonly Dictionary<string, DateTime> _lastTestTime = new();
        private readonly Dictionary<string, int> _retryCount = new();
        private readonly Dictionary<string, string> _lastError = new();
        
        // Configuration
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 2000;
        private const int StatusCacheMinutes = 5;
        
        public event Action<string, ConnectionStatus>? StatusChanged;
        
        private ApiConnectionStatus() { }
        
        /// <summary>
        /// Get current connection status for a provider
        /// </summary>
        public ConnectionStatus GetStatus(string provider)
        {
            return _statusCache.TryGetValue(provider.ToLower(), out var status) ? status : ConnectionStatus.Unknown;
        }
        
        /// <summary>
        /// Get last error message for a provider
        /// </summary>
        public string GetLastError(string provider)
        {
            return _lastError.TryGetValue(provider.ToLower(), out var error) ? error : "";
        }
        
        /// <summary>
        /// Update connection status for a provider
        /// </summary>
        public void UpdateStatus(string provider, ConnectionStatus status, string? error = null)
        {
            var key = provider.ToLower();
            var previousStatus = GetStatus(provider);
            
            _statusCache[key] = status;
            _lastTestTime[key] = DateTime.Now;
            
            if (!string.IsNullOrEmpty(error))
                _lastError[key] = error;
            
            // Reset retry count on success
            if (status == ConnectionStatus.Connected)
                _retryCount[key] = 0;
            
            Debug.WriteLine($"[ApiConnectionStatus] {provider}: {status} {(error != null ? $"({error})" : "")}");
            
            // Notify if status changed
            if (previousStatus != status)
                StatusChanged?.Invoke(provider, status);
        }
        
        /// <summary>
        /// Test connection to a provider with retry logic
        /// </summary>
        public async Task<bool> TestConnectionAsync(string provider, Func<Task<bool>> testFunc, CancellationToken cancellationToken = default)
        {
            var key = provider.ToLower();
            
            // Check if we have a recent cached status
            if (_lastTestTime.TryGetValue(key, out var lastTest))
            {
                var cacheAge = DateTime.Now - lastTest;
                if (cacheAge.TotalMinutes < StatusCacheMinutes && 
                    _statusCache.TryGetValue(key, out var cachedStatus) && 
                    cachedStatus == ConnectionStatus.Connected)
                {
                    Debug.WriteLine($"[ApiConnectionStatus] Using cached status for {provider}");
                    return true;
                }
            }
            
            // Check if API key exists
            if (!ApiKeyManager.HasApiKey(provider))
            {
                UpdateStatus(provider, ConnectionStatus.NoApiKey, "No API key configured");
                return false;
            }
            
            UpdateStatus(provider, ConnectionStatus.Testing);
            
            var retryCount = _retryCount.TryGetValue(key, out var count) ? count : 0;
            const int MaxDelayMs = 30000; // Maximum 30 second delay
            
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;
                    
                    Debug.WriteLine($"[ApiConnectionStatus] Testing {provider} (attempt {attempt + 1}/{MaxRetries + 1})");
                    
                    var result = await testFunc();
                    
                    if (result)
                    {
                        UpdateStatus(provider, ConnectionStatus.Connected);
                        return true;
                    }
                    
                    // Test returned false, but no exception - might be invalid key
                    if (attempt == MaxRetries)
                    {
                        UpdateStatus(provider, ConnectionStatus.InvalidKey, "Connection test failed - check API key");
                        return false;
                    }
                }
                catch (TaskCanceledException)
                {
                    UpdateStatus(provider, ConnectionStatus.Disconnected, "Connection timeout");
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ApiConnectionStatus] Test failed for {provider}: {ex.Message}");
                    
                    if (attempt == MaxRetries)
                    {
                        // Determine status based on error
                        var status = ConnectionStatus.Disconnected;
                        var errorMsg = ex.Message;
                        
                        if (errorMsg.Contains("401") || errorMsg.Contains("Unauthorized") || errorMsg.Contains("Invalid"))
                        {
                            status = ConnectionStatus.InvalidKey;
                            errorMsg = "Invalid API key";
                        }
                        else if (errorMsg.Contains("429") || errorMsg.Contains("rate limit"))
                        {
                            status = ConnectionStatus.RateLimited;
                            errorMsg = "Rate limited - too many requests";
                        }
                        
                        UpdateStatus(provider, status, errorMsg);
                        _retryCount[key] = retryCount + 1;
                        return false;
                    }
                }
                
                // Wait before retry with exponential backoff and max delay cap
                if (attempt < MaxRetries)
                {
                    var delay = Math.Min(RetryDelayMs * (int)Math.Pow(2, attempt), MaxDelayMs);
                    await Task.Delay(delay, cancellationToken);
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Get a user-friendly status message
        /// </summary>
        public string GetStatusMessage(string provider)
        {
            var status = GetStatus(provider);
            var error = GetLastError(provider);
            
            switch (status)
            {
                case ConnectionStatus.Connected:
                    return $"✅ {provider} - Connected";
                    
                case ConnectionStatus.NoApiKey:
                    return $"🔑 {provider} - No API key configured";
                    
                case ConnectionStatus.InvalidKey:
                    return $"❌ {provider} - Invalid API key";
                    
                case ConnectionStatus.Disconnected:
                    return $"🔴 {provider} - Not connected{(string.IsNullOrEmpty(error) ? "" : $": {error}")}";
                    
                case ConnectionStatus.RateLimited:
                    return $"⏸️ {provider} - Rate limited, please wait";
                    
                case ConnectionStatus.Testing:
                    return $"🔄 {provider} - Testing connection...";
                    
                default:
                    return $"❓ {provider} - Unknown status";
            }
        }
        
        /// <summary>
        /// Check if any provider is connected
        /// </summary>
        public bool HasAnyConnection()
        {
            foreach (var status in _statusCache.Values)
            {
                if (status == ConnectionStatus.Connected)
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Get all configured providers and their statuses
        /// </summary>
        public Dictionary<string, ConnectionStatus> GetAllStatuses()
        {
            return new Dictionary<string, ConnectionStatus>(_statusCache);
        }
        
        /// <summary>
        /// Clear cached status for a provider (force retest)
        /// </summary>
        public void ClearStatus(string provider)
        {
            var key = provider.ToLower();
            _statusCache.Remove(key);
            _lastTestTime.Remove(key);
            _lastError.Remove(key);
            _retryCount.Remove(key);
        }
        
        /// <summary>
        /// Clear all cached statuses
        /// </summary>
        public void ClearAllStatuses()
        {
            _statusCache.Clear();
            _lastTestTime.Clear();
            _lastError.Clear();
            _retryCount.Clear();
        }
    }
}
