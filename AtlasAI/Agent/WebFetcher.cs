using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;

namespace AtlasAI.Agent
{
    /// <summary>
    /// Fetches and extracts content from web pages - gives Atlas the ability to read websites
    /// </summary>
    public static class WebFetcher
    {
        private static readonly HttpClient _client = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        static WebFetcher()
        {
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        /// <summary>
        /// Fetch and extract readable text from a URL
        /// </summary>
        public static async Task<string> FetchAsync(string url)
        {
            try
            {
                if (!url.StartsWith("http"))
                    url = "https://" + url;

                Debug.WriteLine($"[WebFetcher] Fetching: {url}");
                var response = await _client.GetStringAsync(url);
                var text = ExtractText(response);
                
                // Limit to reasonable size
                if (text.Length > 8000)
                    text = text.Substring(0, 8000) + "\n\n[Content truncated...]";

                return $"📄 Content from {url}:\n\n{text}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebFetcher] Error: {ex.Message}");
                return $"❌ Could not fetch {url}: {ex.Message}";
            }
        }

        /// <summary>
        /// Search the web and return results
        /// </summary>
        public static async Task<string> SearchAsync(string query)
        {
            try
            {
                // Use DuckDuckGo HTML for search (no API key needed)
                var searchUrl = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
                var html = await _client.GetStringAsync(searchUrl);
                
                var results = ExtractSearchResults(html);
                if (string.IsNullOrEmpty(results))
                    return $"🔍 No results found for: {query}";

                return $"🔍 Search results for \"{query}\":\n\n{results}";
            }
            catch (Exception ex)
            {
                return $"❌ Search failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Open a URL in the default browser
        /// </summary>
        public static string OpenUrl(string url)
        {
            try
            {
                if (!url.StartsWith("http"))
                    url = "https://" + url;

                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return $"🌐 Opened: {url}";
            }
            catch (Exception ex)
            {
                return $"❌ Could not open URL: {ex.Message}";
            }
        }

        /// <summary>
        /// Open a website by common name
        /// </summary>
        public static string OpenWebsite(string name)
        {
            var sites = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["youtube"] = "https://youtube.com",
                ["google"] = "https://google.com",
                ["gmail"] = "https://mail.google.com",
                ["github"] = "https://github.com",
                ["reddit"] = "https://reddit.com",
                ["twitter"] = "https://twitter.com",
                ["x"] = "https://x.com",
                ["facebook"] = "https://facebook.com",
                ["instagram"] = "https://instagram.com",
                ["linkedin"] = "https://linkedin.com",
                ["amazon"] = "https://amazon.com",
                ["netflix"] = "https://netflix.com",
                ["spotify"] = "https://open.spotify.com",
                ["twitch"] = "https://twitch.tv",
                ["discord"] = "https://discord.com/app",
                ["stackoverflow"] = "https://stackoverflow.com",
                ["wikipedia"] = "https://wikipedia.org",
                ["chatgpt"] = "https://chat.openai.com",
                ["claude"] = "https://claude.ai",
                ["bing"] = "https://bing.com",
                ["duckduckgo"] = "https://duckduckgo.com",
                ["news"] = "https://news.google.com",
                ["bbc"] = "https://bbc.com/news",
                ["cnn"] = "https://cnn.com",
                ["weather"] = "https://weather.com",
                ["maps"] = "https://maps.google.com",
                ["drive"] = "https://drive.google.com",
                ["docs"] = "https://docs.google.com",
                ["sheets"] = "https://sheets.google.com",
                ["outlook"] = "https://outlook.live.com",
                ["office"] = "https://office.com",
                ["notion"] = "https://notion.so",
                ["figma"] = "https://figma.com",
                ["canva"] = "https://canva.com",
                ["trello"] = "https://trello.com",
                ["slack"] = "https://slack.com",
                ["zoom"] = "https://zoom.us",
                ["teams"] = "https://teams.microsoft.com",
                ["whatsapp"] = "https://web.whatsapp.com",
                ["telegram"] = "https://web.telegram.org",
                ["pinterest"] = "https://pinterest.com",
                ["tiktok"] = "https://tiktok.com",
                ["hulu"] = "https://hulu.com",
                ["disney"] = "https://disneyplus.com",
                ["prime"] = "https://primevideo.com",
                ["ebay"] = "https://ebay.com",
                ["etsy"] = "https://etsy.com",
                ["paypal"] = "https://paypal.com",
                ["bank"] = "https://chase.com", // Default bank
            };

            if (sites.TryGetValue(name.Trim(), out var url))
                return OpenUrl(url);

            // Try as direct URL
            if (name.Contains("."))
                return OpenUrl(name);

            return $"❌ Unknown website: {name}. Try the full URL.";
        }

        private static string ExtractText(string html)
        {
            // Remove scripts and styles
            html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<nav[^>]*>[\s\S]*?</nav>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<footer[^>]*>[\s\S]*?</footer>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<header[^>]*>[\s\S]*?</header>", "", RegexOptions.IgnoreCase);
            
            // Convert common elements
            html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</p>", "\n\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</div>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</li>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<h[1-6][^>]*>", "\n## ", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</h[1-6]>", "\n", RegexOptions.IgnoreCase);
            
            // Remove all remaining tags
            html = Regex.Replace(html, @"<[^>]+>", "");
            
            // Decode entities
            html = System.Net.WebUtility.HtmlDecode(html);
            
            // Clean up whitespace
            html = Regex.Replace(html, @"[ \t]+", " ");
            html = Regex.Replace(html, @"\n{3,}", "\n\n");
            
            return html.Trim();
        }

        private static string ExtractSearchResults(string html)
        {
            var results = new System.Text.StringBuilder();
            var matches = Regex.Matches(html, @"<a[^>]+class=""result__a""[^>]*href=""([^""]+)""[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase);
            
            int count = 0;
            foreach (Match m in matches)
            {
                if (count >= 5) break;
                var url = m.Groups[1].Value;
                var title = System.Net.WebUtility.HtmlDecode(m.Groups[2].Value.Trim());
                
                // Clean up DuckDuckGo redirect URL
                if (url.Contains("uddg="))
                {
                    var match = Regex.Match(url, @"uddg=([^&]+)");
                    if (match.Success)
                        url = Uri.UnescapeDataString(match.Groups[1].Value);
                }
                
                results.AppendLine($"{count + 1}. {title}");
                results.AppendLine($"   {url}");
                results.AppendLine();
                count++;
            }
            
            return results.ToString();
        }
    }
}
