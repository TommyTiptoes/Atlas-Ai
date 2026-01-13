using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace AtlasAI.Agent
{
    /// <summary>
    /// URL Tools - Shorten URLs, encode/decode, parse query strings.
    /// </summary>
    public static class UrlTools
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
        
        /// <summary>
        /// Handle URL commands
        /// </summary>
        public static async Task<string?> TryHandleAsync(string input)
        {
            var lower = input.ToLowerInvariant();
            
            // URL encode
            if (lower.Contains("url encode") || lower.Contains("encode url") || lower.Contains("urlencode"))
            {
                return UrlEncode();
            }
            
            // URL decode
            if (lower.Contains("url decode") || lower.Contains("decode url") || lower.Contains("urldecode"))
            {
                return UrlDecode();
            }
            
            // Parse URL
            if (lower.Contains("parse url") || lower.Contains("url parse") || lower.Contains("url info"))
            {
                return ParseUrl();
            }
            
            // Extract query params
            if (lower.Contains("query") && (lower.Contains("param") || lower.Contains("string")))
            {
                return ParseQueryString();
            }
            
            // Shorten URL (using is.gd free service)
            if (lower.Contains("shorten") && lower.Contains("url"))
            {
                return await ShortenUrlAsync();
            }
            
            return null;
        }
        
        private static string UrlEncode()
        {
            string? text = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Clipboard.ContainsText())
                    text = Clipboard.GetText();
            });
            
            if (string.IsNullOrEmpty(text))
                return "📋 Copy some text to clipboard first!";
            
            var encoded = Uri.EscapeDataString(text);
            Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(encoded));
            
            var preview = encoded.Length > 200 ? encoded.Substring(0, 197) + "..." : encoded;
            
            return $"🔗 **URL Encoded:**\n```\n{preview}\n```\n✓ Copied to clipboard!";
        }
        
        private static string UrlDecode()
        {
            string? text = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Clipboard.ContainsText())
                    text = Clipboard.GetText();
            });
            
            if (string.IsNullOrEmpty(text))
                return "📋 Copy some text to clipboard first!";
            
            try
            {
                var decoded = Uri.UnescapeDataString(text);
                Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(decoded));
                
                var preview = decoded.Length > 200 ? decoded.Substring(0, 197) + "..." : decoded;
                
                return $"🔗 **URL Decoded:**\n```\n{preview}\n```\n✓ Copied to clipboard!";
            }
            catch
            {
                return "❌ Invalid URL-encoded string";
            }
        }
        
        private static string ParseUrl()
        {
            string? text = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Clipboard.ContainsText())
                    text = Clipboard.GetText()?.Trim();
            });
            
            if (string.IsNullOrEmpty(text))
                return "📋 Copy a URL to clipboard first!";
            
            try
            {
                var uri = new Uri(text);
                
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"🔗 **URL Parsed:**\n");
                sb.AppendLine($"**Scheme:** {uri.Scheme}");
                sb.AppendLine($"**Host:** {uri.Host}");
                if (uri.Port != 80 && uri.Port != 443)
                    sb.AppendLine($"**Port:** {uri.Port}");
                sb.AppendLine($"**Path:** {uri.AbsolutePath}");
                
                if (!string.IsNullOrEmpty(uri.Query))
                    sb.AppendLine($"**Query:** {uri.Query}");
                
                if (!string.IsNullOrEmpty(uri.Fragment))
                    sb.AppendLine($"**Fragment:** {uri.Fragment}");
                
                // Parse query params
                if (!string.IsNullOrEmpty(uri.Query))
                {
                    sb.AppendLine("\n**Query Parameters:**");
                    var query = uri.Query.TrimStart('?');
                    var pairs = query.Split('&');
                    foreach (var pair in pairs)
                    {
                        var kv = pair.Split('=', 2);
                        var key = Uri.UnescapeDataString(kv[0]);
                        var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
                        sb.AppendLine($"  • {key} = {value}");
                    }
                }
                
                return sb.ToString();
            }
            catch
            {
                return "❌ Invalid URL in clipboard";
            }
        }
        
        private static string ParseQueryString()
        {
            string? text = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Clipboard.ContainsText())
                    text = Clipboard.GetText()?.Trim();
            });
            
            if (string.IsNullOrEmpty(text))
                return "📋 Copy a query string to clipboard first!";
            
            // Remove leading ? if present
            text = text.TrimStart('?');
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🔍 **Query Parameters:**\n");
            
            var pairs = text.Split('&');
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(kv[0]);
                var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
                sb.AppendLine($"**{key}:** {value}");
            }
            
            return sb.ToString();
        }
        
        private static async Task<string> ShortenUrlAsync()
        {
            string? url = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Clipboard.ContainsText())
                    url = Clipboard.GetText()?.Trim();
            });
            
            if (string.IsNullOrEmpty(url))
                return "📋 Copy a URL to clipboard first!";
            
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "https://" + url;
            
            try
            {
                // Using is.gd free URL shortener
                var encoded = Uri.EscapeDataString(url);
                var response = await _httpClient.GetStringAsync($"https://is.gd/create.php?format=simple&url={encoded}");
                var shortUrl = response.Trim();
                
                Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(shortUrl));
                
                return $"🔗 **URL Shortened:**\n\n" +
                       $"**Original:** {(url.Length > 50 ? url.Substring(0, 47) + "..." : url)}\n" +
                       $"**Short:** `{shortUrl}`\n\n" +
                       $"✓ Copied to clipboard!";
            }
            catch (Exception ex)
            {
                return $"❌ Couldn't shorten URL: {ex.Message}";
            }
        }
    }
}
