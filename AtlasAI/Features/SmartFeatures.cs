using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AtlasAI.Features
{
    /// <summary>
    /// Smart features that make Atlas feel more advanced and useful
    /// </summary>
    public class SmartFeatures
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string NotesFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
            "Atlas Notes");
        
        private static readonly Random _random = new Random();
        
        static SmartFeatures()
        {
            // Ensure notes folder exists
            if (!Directory.Exists(NotesFolder))
                Directory.CreateDirectory(NotesFolder);
        }
        
        #region Quick Notes
        
        /// <summary>
        /// Save a quick note to file
        /// </summary>
        public static async Task<string> TakeNoteAsync(string noteContent)
        {
            try
            {
                var timestamp = DateTime.Now;
                var filename = $"note_{timestamp:yyyyMMdd_HHmmss}.txt";
                var filepath = Path.Combine(NotesFolder, filename);
                
                var fullNote = $"📝 Note taken: {timestamp:MMMM dd, yyyy 'at' h:mm tt}\n\n{noteContent}";
                await File.WriteAllTextAsync(filepath, fullNote);
                
                return $"📝 Note saved!\n\n\"{noteContent}\"\n\n📁 Saved to: {filepath}";
            }
            catch (Exception ex)
            {
                return $"❌ Couldn't save note: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Get all saved notes
        /// </summary>
        public static async Task<string> GetNotesAsync()
        {
            try
            {
                if (!Directory.Exists(NotesFolder))
                    return "📝 No notes yet. Say \"take a note\" to create one!";
                
                var files = Directory.GetFiles(NotesFolder, "*.txt")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .Take(10)
                    .ToList();
                
                if (files.Count == 0)
                    return "📝 No notes yet. Say \"take a note\" to create one!";
                
                var result = "📝 YOUR RECENT NOTES\n━━━━━━━━━━━━━━━━━━━━\n\n";
                
                foreach (var file in files)
                {
                    var content = await File.ReadAllTextAsync(file);
                    var preview = content.Length > 100 ? content.Substring(0, 100) + "..." : content;
                    var date = File.GetCreationTime(file);
                    result += $"📄 {date:MMM dd, h:mm tt}\n{preview}\n\n";
                }
                
                result += $"📁 Notes folder: {NotesFolder}";
                return result;
            }
            catch (Exception ex)
            {
                return $"❌ Error reading notes: {ex.Message}";
            }
        }
        
        #endregion
        
        #region System Diagnostics
        
        /// <summary>
        /// Get comprehensive system diagnostics
        /// </summary>
        public static async Task<string> GetSystemDiagnosticsAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var result = "🖥️ SYSTEM DIAGNOSTICS\n━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";
                    
                    // CPU Info
                    result += GetCpuInfo();
                    
                    // Memory Info
                    result += GetMemoryInfo();
                    
                    // Disk Info
                    result += GetDiskInfo();
                    
                    // System Uptime
                    result += GetUptimeInfo();
                    
                    // Running Processes
                    result += GetTopProcesses();
                    
                    return result;
                }
                catch (Exception ex)
                {
                    return $"❌ Error getting diagnostics: {ex.Message}";
                }
            });
        }
        
        private static string GetCpuInfo()
        {
            try
            {
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // First call returns 0
                System.Threading.Thread.Sleep(100);
                var cpuUsage = cpuCounter.NextValue();
                
                string cpuName = "Unknown";
                using (var searcher = new ManagementObjectSearcher("select Name from Win32_Processor"))
                {
                    foreach (var item in searcher.Get())
                    {
                        cpuName = item["Name"]?.ToString() ?? "Unknown";
                        break;
                    }
                }
                
                var bar = GetProgressBar(cpuUsage, 100);
                var status = cpuUsage > 80 ? "🔴 HIGH" : cpuUsage > 50 ? "🟡 MODERATE" : "🟢 NORMAL";
                
                return $"⚡ CPU: {cpuUsage:F1}% {status}\n{bar}\n{cpuName}\n\n";
            }
            catch
            {
                return "⚡ CPU: Unable to read\n\n";
            }
        }
        
        private static string GetMemoryInfo()
        {
            try
            {
                var gcMemory = GC.GetTotalMemory(false) / 1024 / 1024;
                
                ulong totalMemory = 0;
                ulong freeMemory = 0;
                
                using (var searcher = new ManagementObjectSearcher("select TotalVisibleMemorySize, FreePhysicalMemory from Win32_OperatingSystem"))
                {
                    foreach (var item in searcher.Get())
                    {
                        totalMemory = Convert.ToUInt64(item["TotalVisibleMemorySize"]) / 1024; // MB
                        freeMemory = Convert.ToUInt64(item["FreePhysicalMemory"]) / 1024; // MB
                        break;
                    }
                }
                
                var usedMemory = totalMemory - freeMemory;
                var usagePercent = (double)usedMemory / totalMemory * 100;
                
                var bar = GetProgressBar(usagePercent, 100);
                var status = usagePercent > 85 ? "🔴 HIGH" : usagePercent > 60 ? "🟡 MODERATE" : "🟢 NORMAL";
                
                return $"🧠 RAM: {usedMemory:N0} MB / {totalMemory:N0} MB ({usagePercent:F1}%) {status}\n{bar}\n\n";
            }
            catch
            {
                return "🧠 RAM: Unable to read\n\n";
            }
        }
        
        private static string GetDiskInfo()
        {
            try
            {
                var result = "💾 STORAGE:\n";
                
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    var totalGB = drive.TotalSize / 1024 / 1024 / 1024;
                    var freeGB = drive.AvailableFreeSpace / 1024 / 1024 / 1024;
                    var usedGB = totalGB - freeGB;
                    var usagePercent = (double)usedGB / totalGB * 100;
                    
                    var bar = GetProgressBar(usagePercent, 100);
                    var status = usagePercent > 90 ? "🔴" : usagePercent > 70 ? "🟡" : "🟢";
                    
                    result += $"  {drive.Name} {status} {usedGB:N0} GB / {totalGB:N0} GB ({usagePercent:F0}%)\n  {bar}\n";
                }
                
                return result + "\n";
            }
            catch
            {
                return "💾 STORAGE: Unable to read\n\n";
            }
        }
        
        private static string GetUptimeInfo()
        {
            try
            {
                var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                return $"⏱️ UPTIME: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m\n\n";
            }
            catch
            {
                return "";
            }
        }
        
        private static string GetTopProcesses()
        {
            try
            {
                var processes = Process.GetProcesses()
                    .Where(p => p.WorkingSet64 > 0)
                    .OrderByDescending(p => p.WorkingSet64)
                    .Take(5)
                    .ToList();
                
                var result = "📊 TOP PROCESSES (by memory):\n";
                foreach (var p in processes)
                {
                    try
                    {
                        var memMB = p.WorkingSet64 / 1024 / 1024;
                        result += $"  • {p.ProcessName}: {memMB:N0} MB\n";
                    }
                    catch { }
                }
                
                return result;
            }
            catch
            {
                return "";
            }
        }
        
        private static string GetProgressBar(double value, double max)
        {
            var percent = Math.Min(value / max, 1.0);
            var filled = (int)(percent * 20);
            var empty = 20 - filled;
            return "[" + new string('█', filled) + new string('░', empty) + "]";
        }
        
        #endregion
        
        #region Website Shortcuts
        
        private static readonly Dictionary<string, string> WebsiteShortcuts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Video & Entertainment
            { "youtube", "https://www.youtube.com" },
            { "netflix", "https://www.netflix.com" },
            { "twitch", "https://www.twitch.tv" },
            { "disney", "https://www.disneyplus.com" },
            { "disney plus", "https://www.disneyplus.com" },
            { "prime video", "https://www.primevideo.com" },
            { "hulu", "https://www.hulu.com" },
            
            // Social Media
            { "twitter", "https://twitter.com" },
            { "x", "https://twitter.com" },
            { "facebook", "https://www.facebook.com" },
            { "instagram", "https://www.instagram.com" },
            { "tiktok", "https://www.tiktok.com" },
            { "reddit", "https://www.reddit.com" },
            { "linkedin", "https://www.linkedin.com" },
            { "discord", "https://discord.com/app" },
            
            // Productivity
            { "gmail", "https://mail.google.com" },
            { "email", "https://mail.google.com" },
            { "outlook", "https://outlook.live.com" },
            { "google drive", "https://drive.google.com" },
            { "dropbox", "https://www.dropbox.com" },
            { "notion", "https://www.notion.so" },
            { "trello", "https://trello.com" },
            { "slack", "https://slack.com" },
            
            // Shopping
            { "amazon", "https://www.amazon.co.uk" },
            { "ebay", "https://www.ebay.co.uk" },
            
            // News
            { "bbc", "https://www.bbc.co.uk/news" },
            { "bbc news", "https://www.bbc.co.uk/news" },
            { "cnn", "https://www.cnn.com" },
            { "news", "https://news.google.com" },
            
            // Dev & Tech
            { "github", "https://github.com" },
            { "stackoverflow", "https://stackoverflow.com" },
            { "stack overflow", "https://stackoverflow.com" },
            
            // Search
            { "google", "https://www.google.com" },
            { "bing", "https://www.bing.com" },
            
            // Music
            { "spotify", "https://open.spotify.com" },
            { "soundcloud", "https://soundcloud.com" },
            
            // Other
            { "chatgpt", "https://chat.openai.com" },
            { "claude", "https://claude.ai" },
            { "wikipedia", "https://www.wikipedia.org" }
        };
        
        /// <summary>
        /// Open a website by name
        /// </summary>
        public static string OpenWebsite(string siteName)
        {
            var name = siteName.ToLower().Trim();
            
            if (WebsiteShortcuts.TryGetValue(name, out var url))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return $"🌐 Opening {siteName}...";
            }
            
            // Try as direct URL
            if (name.Contains("."))
            {
                var fullUrl = name.StartsWith("http") ? name : $"https://{name}";
                Process.Start(new ProcessStartInfo(fullUrl) { UseShellExecute = true });
                return $"🌐 Opening {fullUrl}...";
            }
            
            return $"❓ I don't know that website. Try saying the full URL or one of these:\nYouTube, Netflix, Twitter, Reddit, Gmail, Amazon, GitHub, BBC News, etc.";
        }
        
        /// <summary>
        /// Get list of available website shortcuts
        /// </summary>
        public static string GetWebsiteList()
        {
            return @"🌐 QUICK WEBSITE ACCESS
━━━━━━━━━━━━━━━━━━━━━━━━

Say ""Open [site]"" to launch:

📺 ENTERTAINMENT
• YouTube, Netflix, Twitch, Disney Plus, Prime Video

💬 SOCIAL
• Twitter/X, Facebook, Instagram, TikTok, Reddit, Discord

📧 PRODUCTIVITY  
• Gmail, Outlook, Google Drive, Notion, Slack

🛒 SHOPPING
• Amazon, eBay

📰 NEWS
• BBC News, CNN, Google News

💻 DEV
• GitHub, Stack Overflow

🎵 MUSIC
• Spotify, SoundCloud

🔍 SEARCH
• Google, Bing, Wikipedia, ChatGPT, Claude";
        }
        
        #endregion
        
        #region System Files
        
        /// <summary>
        /// Open the Windows hosts file in Notepad with admin privileges
        /// </summary>
        public static string OpenHostsFile()
        {
            try
            {
                var hostsPath = @"C:\Windows\System32\drivers\etc\hosts";
                
                // Check if file exists
                if (!File.Exists(hostsPath))
                {
                    return "❌ Hosts file not found at expected location.";
                }
                
                // Launch Notepad as admin with the hosts file
                var startInfo = new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = hostsPath,
                    Verb = "runas", // Run as administrator
                    UseShellExecute = true
                };
                
                Process.Start(startInfo);
                
                return @"📝 Opening hosts file with admin privileges...

⚠️ IMPORTANT:
• The hosts file requires admin rights to edit
• A UAC prompt will appear - click 'Yes' to allow
• Be careful editing this file - incorrect entries can break network access

📍 Location: C:\Windows\System32\drivers\etc\hosts

💡 Common uses:
• Block websites: 127.0.0.1 facebook.com
• Redirect domains: 192.168.1.100 myserver.local
• Test local development sites";
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // User cancelled UAC prompt
                return "❌ Admin access was cancelled. The hosts file requires administrator privileges to edit.";
            }
            catch (Exception ex)
            {
                return $"❌ Error opening hosts file: {ex.Message}";
            }
        }
        
        #endregion
        
        #region Jokes & Fun
        
        private static readonly string[] Jokes = new[]
        {
            "Why do programmers prefer dark mode? Because light attracts bugs! 🐛",
            "Why did the developer go broke? Because he used up all his cache! 💸",
            "There are only 10 types of people in the world: those who understand binary and those who don't. 🤓",
            "A SQL query walks into a bar, walks up to two tables and asks... 'Can I join you?' 🍺",
            "Why do Java developers wear glasses? Because they can't C#! 👓",
            "What's a computer's favorite snack? Microchips! 🍟",
            "Why was the JavaScript developer sad? Because he didn't Node how to Express himself! 😢",
            "What do you call 8 hobbits? A hobbyte! 🧙",
            "Why did the computer go to the doctor? Because it had a virus! 🤒",
            "What's a robot's favorite type of music? Heavy metal! 🤖",
            "Why don't scientists trust atoms? Because they make up everything! ⚛️",
            "I told my computer I needed a break, and now it won't stop sending me Kit-Kat ads. 🍫",
            "Why did the PowerPoint presentation cross the road? To get to the other slide! 📊",
            "What do you call a computer that sings? A-Dell! 🎤",
            "Why was the computer cold? It left its Windows open! 🪟",
            "How does a computer get drunk? It takes screenshots! 📸",
            "Why did the computer keep freezing? It left too many windows open in winter! ❄️",
            "What's a computer's least favorite food? Spam! 📧",
            "Why do programmers always mix up Halloween and Christmas? Because Oct 31 = Dec 25! 🎃🎄",
            "I would tell you a UDP joke, but you might not get it. 📡"
        };
        
        private static readonly string[] FunFacts = new[]
        {
            "🧠 The human brain can store approximately 2.5 petabytes of data - that's about 3 million hours of TV!",
            "🌍 There are more possible iterations of a game of chess than there are atoms in the known universe.",
            "💻 The first computer bug was an actual bug - a moth found in a Harvard computer in 1947.",
            "🚀 NASA's computers in 1969 had less processing power than a modern calculator.",
            "📱 The average smartphone has more computing power than all of NASA had in 1969.",
            "🎮 The first video game ever made was 'Tennis for Two' created in 1958.",
            "🌐 The first website ever created is still online: info.cern.ch",
            "📧 The first email was sent by Ray Tomlinson to himself in 1971.",
            "🔋 If you charged your phone once a day, it would cost about £1 per year in electricity.",
            "🖥️ The QWERTY keyboard was designed to slow typists down to prevent typewriter jams.",
            "🎵 The 'Intel Inside' jingle was composed in just 3 days.",
            "📺 YouTube was originally designed as a video dating site called 'Tune In Hook Up'.",
            "🐦 Twitter's bird logo is named 'Larry' after basketball legend Larry Bird.",
            "📸 The first photo ever uploaded to the internet was of a comedy band called Les Horribles Cernettes.",
            "🔐 'password' and '123456' are still among the most common passwords used today.",
            "🌙 There's a website that tracks how many people are in space right now: howmanypeopleareinspacerightnow.com",
            "💾 A floppy disk could hold about 1.44 MB - that's less than one modern photo!",
            "🎯 Google's original name was 'BackRub' before it became Google in 1997.",
            "🤖 The word 'robot' comes from the Czech word 'robota' meaning forced labor.",
            "⌨️ The average person types at about 40 words per minute, but professional typists can exceed 100 WPM."
        };
        
        private static readonly string[] Compliments = new[]
        {
            "You're doing great today! Keep it up! 💪",
            "Your dedication is truly inspiring! ⭐",
            "You've got this! I believe in you! 🌟",
            "You're smarter than you think! 🧠",
            "Your potential is limitless! 🚀",
            "You make the world a better place! 🌍",
            "Your creativity knows no bounds! 🎨",
            "You're absolutely crushing it! 💥",
            "The world needs more people like you! ❤️",
            "You're a problem-solving machine! 🔧"
        };
        
        /// <summary>
        /// Tell a random joke
        /// </summary>
        public static string TellJoke()
        {
            return "😄 " + Jokes[_random.Next(Jokes.Length)];
        }
        
        /// <summary>
        /// Share a fun fact
        /// </summary>
        public static string TellFunFact()
        {
            return FunFacts[_random.Next(FunFacts.Length)];
        }
        
        /// <summary>
        /// Give a compliment
        /// </summary>
        public static string GiveCompliment()
        {
            return "💝 " + Compliments[_random.Next(Compliments.Length)];
        }
        
        /// <summary>
        /// Flip a coin
        /// </summary>
        public static string FlipCoin()
        {
            var result = _random.Next(2) == 0 ? "Heads" : "Tails";
            return $"🪙 *flips coin*\n\nIt's... {result}!";
        }
        
        /// <summary>
        /// Roll dice
        /// </summary>
        public static string RollDice(int sides = 6, int count = 1)
        {
            if (sides < 2) sides = 6;
            if (count < 1) count = 1;
            if (count > 10) count = 10;
            
            var rolls = new List<int>();
            for (int i = 0; i < count; i++)
            {
                rolls.Add(_random.Next(1, sides + 1));
            }
            
            var total = rolls.Sum();
            var rollsStr = string.Join(", ", rolls);
            
            if (count == 1)
                return $"🎲 *rolls d{sides}*\n\nYou rolled: {total}!";
            else
                return $"🎲 *rolls {count}d{sides}*\n\nRolls: {rollsStr}\nTotal: {total}";
        }
        
        /// <summary>
        /// Magic 8-ball
        /// </summary>
        public static string Magic8Ball()
        {
            var responses = new[]
            {
                "It is certain! ✨",
                "Without a doubt! 👍",
                "Yes, definitely! ✅",
                "You may rely on it! 🤝",
                "As I see it, yes! 👀",
                "Most likely! 📈",
                "Outlook good! 🌤️",
                "Signs point to yes! ➡️",
                "Reply hazy, try again... 🌫️",
                "Ask again later... ⏰",
                "Better not tell you now... 🤐",
                "Cannot predict now... 🔮",
                "Concentrate and ask again... 🧘",
                "Don't count on it... 👎",
                "My reply is no... ❌",
                "My sources say no... 📉",
                "Outlook not so good... 🌧️",
                "Very doubtful... 🤔"
            };
            
            return "🎱 *shakes magic 8-ball*\n\n" + responses[_random.Next(responses.Length)];
        }
        
        #endregion
        
        #region Daily Briefing
        
        /// <summary>
        /// Get a comprehensive daily briefing
        /// </summary>
        public static async Task<string> GetDailyBriefingAsync(string location = "Middlesbrough")
        {
            var result = $"☀️ GOOD {GetTimeOfDayGreeting().ToUpper()}, SIR!\n";
            result += $"📅 {DateTime.Now:dddd, MMMM d, yyyy}\n";
            result += $"🕐 {DateTime.Now:h:mm tt}\n";
            result += "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";
            
            // Weather
            result += await GetWeatherBriefAsync(location);
            result += "\n";
            
            // System status
            result += GetQuickSystemStatus();
            result += "\n";
            
            // Motivational quote
            result += "💭 " + GetMotivationalQuote();
            result += "\n\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";
            result += "Ready to assist you today! What would you like to do?";
            
            return result;
        }
        
        private static string GetTimeOfDayGreeting()
        {
            var hour = DateTime.Now.Hour;
            if (hour >= 5 && hour < 12) return "Morning";
            if (hour >= 12 && hour < 17) return "Afternoon";
            if (hour >= 17 && hour < 21) return "Evening";
            return "Night";
        }
        
        private static async Task<string> GetWeatherBriefAsync(string location)
        {
            try
            {
                // Use wttr.in for simple weather (no API key needed)
                var response = await _httpClient.GetStringAsync($"https://wttr.in/{location}?format=%c+%t+%h+%w");
                return $"🌤️ WEATHER ({location})\n{response.Trim()}\n";
            }
            catch
            {
                return $"🌤️ WEATHER: Unable to fetch (check internet)\n";
            }
        }
        
        private static string GetQuickSystemStatus()
        {
            try
            {
                var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                
                // Quick memory check
                ulong totalMemory = 0;
                ulong freeMemory = 0;
                using (var searcher = new ManagementObjectSearcher("select TotalVisibleMemorySize, FreePhysicalMemory from Win32_OperatingSystem"))
                {
                    foreach (var item in searcher.Get())
                    {
                        totalMemory = Convert.ToUInt64(item["TotalVisibleMemorySize"]) / 1024;
                        freeMemory = Convert.ToUInt64(item["FreePhysicalMemory"]) / 1024;
                        break;
                    }
                }
                var memUsage = (double)(totalMemory - freeMemory) / totalMemory * 100;
                var memStatus = memUsage > 80 ? "🔴" : memUsage > 60 ? "🟡" : "🟢";
                
                return $"💻 SYSTEM STATUS\n{memStatus} Memory: {memUsage:F0}% used | ⏱️ Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m\n";
            }
            catch
            {
                return "💻 SYSTEM: Running normally\n";
            }
        }
        
        private static readonly string[] MotivationalQuotes = new[]
        {
            "\"The only way to do great work is to love what you do.\" - Steve Jobs",
            "\"Innovation distinguishes between a leader and a follower.\" - Steve Jobs",
            "\"Stay hungry, stay foolish.\" - Steve Jobs",
            "\"The future belongs to those who believe in the beauty of their dreams.\" - Eleanor Roosevelt",
            "\"Success is not final, failure is not fatal: it is the courage to continue that counts.\" - Winston Churchill",
            "\"The best time to plant a tree was 20 years ago. The second best time is now.\" - Chinese Proverb",
            "\"Your time is limited, don't waste it living someone else's life.\" - Steve Jobs",
            "\"The only limit to our realization of tomorrow is our doubts of today.\" - Franklin D. Roosevelt",
            "\"Do what you can, with what you have, where you are.\" - Theodore Roosevelt",
            "\"Believe you can and you're halfway there.\" - Theodore Roosevelt"
        };
        
        private static string GetMotivationalQuote()
        {
            return MotivationalQuotes[_random.Next(MotivationalQuotes.Length)];
        }
        
        #endregion
    }
}
