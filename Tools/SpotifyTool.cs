using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using AtlasAI.Voice;

namespace AtlasAI.Tools
{
    public static class SpotifyTool
    {
        private static readonly HttpClient httpClient;
        private static string? _accessToken;
        private static string? _clientId;
        private static string? _clientSecret;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;

        static SpotifyTool()
        {
            httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            LoadSpotifyCredentials();
        }

        private static void LoadSpotifyCredentials()
        {
            try
            {
                var keysPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AtlasAI", "ai_keys.json");
                if (File.Exists(keysPath))
                {
                    var json = File.ReadAllText(keysPath);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("spotify_client_id", out var id))
                        _clientId = id.GetString();
                    if (doc.RootElement.TryGetProperty("spotify_client_secret", out var secret))
                        _clientSecret = secret.GetString();
                }
            }
            catch { }
        }

        private static async Task<bool> EnsureAccessTokenAsync()
        {
            if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
                return false;
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiry)
                return true;

            try
            {
                var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
                var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
                request.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

                var response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(responseText);
                    _accessToken = doc.RootElement.GetProperty("access_token").GetString();
                    var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
                    _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static async Task<(string? uri, string? name, string? artist)> SearchTrackAsync(string query)
        {
            if (!await EnsureAccessTokenAsync())
                return (null, null, null);

            try
            {
                var url = $"https://api.spotify.com/v1/search?q={HttpUtility.UrlEncode(query)}&type=track&limit=1";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(responseText);
                    var tracks = doc.RootElement.GetProperty("tracks").GetProperty("items");
                    if (tracks.GetArrayLength() > 0)
                    {
                        var track = tracks[0];
                        return (
                            track.GetProperty("uri").GetString(),
                            track.GetProperty("name").GetString(),
                            track.GetProperty("artists")[0].GetProperty("name").GetString()
                        );
                    }
                }
            }
            catch { }
            return (null, null, null);
        }

        /// <summary>
        /// Play a song on Spotify desktop app
        /// </summary>
        public static async Task<string> PlayAsync(string query)
        {
            try
            {
                // Tell AudioDuckingManager we're starting music - don't restore/pause after
                AudioDuckingManager.SkipNextRestore();
                
                Debug.WriteLine($"[Spotify] PlayAsync called with query: '{query}'");
                
                // Make sure Spotify desktop is running
                var procs = Process.GetProcessesByName("Spotify");
                bool wasSpotifyRunning = procs.Length > 0;
                
                if (!wasSpotifyRunning)
                {
                    Debug.WriteLine("[Spotify] Spotify not running, launching...");
                    Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true });
                    await Task.Delay(4000); // Wait for Spotify to fully load
                }

                // Try API search first if configured
                var (trackUri, trackName, artistName) = await SearchTrackAsync(query);
                
                if (!string.IsNullOrEmpty(trackUri))
                {
                    Debug.WriteLine($"[Spotify] Found track: {trackName} by {artistName} - URI: {trackUri}");
                    
                    // Use spotify: protocol to play the track
                    // Format: spotify:track:TRACKID - this should auto-play
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = trackUri,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                    
                    // spotify: URI auto-plays, no need to send play key
                    await Task.Delay(2000);
                    
                    return $"🎵 Playing: {trackName} by {artistName}";
                }

                // Try searching for artist if no track found
                Debug.WriteLine($"[Spotify] No track found, trying artist search for: {query}");
                var (artistUri, foundArtistName) = await SearchArtistAsync(query);
                if (!string.IsNullOrEmpty(artistUri))
                {
                    Debug.WriteLine($"[Spotify] Found artist: {foundArtistName} - URI: {artistUri}");
                    Process.Start(new ProcessStartInfo(artistUri) { UseShellExecute = true });
                    // spotify: URI auto-plays
                    await Task.Delay(2000);
                    return $"🎵 Playing: {foundArtistName}";
                }

                // Last resort: Open Spotify search and try to play
                Debug.WriteLine($"[Spotify] No API results, using search URI for: {query}");
                var searchUri = $"spotify:search:{HttpUtility.UrlEncode(query)}";
                Process.Start(new ProcessStartInfo(searchUri) { UseShellExecute = true });
                
                // Wait for search to load, then select first result with Enter
                await Task.Delay(2500);
                
                // Send Enter key to select first result (this should auto-play)
                keybd_event(0x0D, 0, 0, UIntPtr.Zero);
                await Task.Delay(50);
                keybd_event(0x0D, 0, 2, UIntPtr.Zero);
                
                return $"🎵 Playing: {query} on Spotify";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Spotify] Error: {ex.Message}");
                return $"❌ Error: {ex.Message}";
            }
        }
        
        private static async Task<(string? uri, string? name)> SearchArtistAsync(string query)
        {
            if (!await EnsureAccessTokenAsync())
                return (null, null);

            try
            {
                var url = $"https://api.spotify.com/v1/search?q={HttpUtility.UrlEncode(query)}&type=artist&limit=1";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[Spotify] Artist search response: {responseText.Substring(0, Math.Min(200, responseText.Length))}...");
                    var doc = JsonDocument.Parse(responseText);
                    var artists = doc.RootElement.GetProperty("artists").GetProperty("items");
                    if (artists.GetArrayLength() > 0)
                    {
                        var artist = artists[0];
                        return (
                            artist.GetProperty("uri").GetString(),
                            artist.GetProperty("name").GetString()
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Spotify] Artist search error: {ex.Message}");
            }
            return (null, null);
        }
        
        private static void SendMediaKey(byte vk)
        {
            keybd_event(vk, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(100);
            keybd_event(vk, 0, 2, UIntPtr.Zero);
        }

        public static Task<string> ControlPlaybackAsync(string action)
        {
            var actionLower = action.ToLower();
            
            // If pausing/stopping, clear music protection so ducking works again
            if (actionLower == "pause" || actionLower == "stop")
            {
                AudioDuckingManager.ClearMusicProtection();
            }
            
            byte key = actionLower switch
            {
                "next" or "skip" => VK_MEDIA_NEXT_TRACK,
                "previous" or "prev" => VK_MEDIA_PREV_TRACK,
                _ => VK_MEDIA_PLAY_PAUSE
            };
            SendMediaKey(key);
            return Task.FromResult(actionLower switch
            {
                "next" or "skip" => "⏭️ Next track",
                "previous" or "prev" => "⏮️ Previous track",
                _ => "▶️ Play/Pause"
            });
        }

        public static Task<string> OpenSpotifyAsync()
        {
            try
            {
                Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true });
                return Task.FromResult("🎵 Opened Spotify");
            }
            catch
            {
                return Task.FromResult("❌ Could not open Spotify");
            }
        }

        public static bool IsApiConfigured => !string.IsNullOrEmpty(_clientId);
    }
}
