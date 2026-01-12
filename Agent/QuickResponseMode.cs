using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MinimalApp.Agent
{
    /// <summary>
    /// Quick response mode - generates fast, concise responses without AI.
    /// For common questions and confirmations that don't need full AI processing.
    /// </summary>
    public static class QuickResponseMode
    {
        // Canned responses for common patterns
        private static readonly Dictionary<string, string[]> QuickResponses = new(StringComparer.OrdinalIgnoreCase)
        {
            // Greetings
            { "hi", new[] { "Hey!", "Hi there!", "What's up?" } },
            { "hello", new[] { "Hello!", "Hey!", "Hi!" } },
            { "hey", new[] { "Hey!", "What's up?", "Hi!" } },
            { "good morning", new[] { "Good morning! ☀️", "Morning!", "Good morning! Ready to help." } },
            { "good afternoon", new[] { "Good afternoon!", "Afternoon!", "Hey there!" } },
            { "good evening", new[] { "Good evening!", "Evening!", "Hey!" } },
            { "good night", new[] { "Good night! 🌙", "Night!", "Sleep well!" } },
            
            // Thanks
            { "thanks", new[] { "No problem!", "You got it!", "Anytime!" } },
            { "thank you", new[] { "You're welcome!", "Happy to help!", "No worries!" } },
            { "cheers", new[] { "Cheers! 🍻", "No problem!", "Anytime!" } },
            { "ta", new[] { "No worries!", "You got it!", "👍" } },
            
            // Confirmations
            { "ok", new[] { "👍", "Cool.", "Got it." } },
            { "okay", new[] { "👍", "Alright!", "Cool." } },
            { "cool", new[] { "😎", "Nice!", "👍" } },
            { "nice", new[] { "😊", "Thanks!", "👍" } },
            { "great", new[] { "Awesome!", "😊", "Glad to hear!" } },
            { "perfect", new[] { "Excellent! ✨", "Great!", "👌" } },
            { "awesome", new[] { "🎉", "Thanks!", "😊" } },
            
            // Status checks
            { "you there", new[] { "I'm here!", "Yep!", "Always here." } },
            { "are you there", new[] { "I'm here!", "Yes!", "Right here." } },
            { "you awake", new[] { "Wide awake!", "Yep!", "Always on." } },
            
            // How are you
            { "how are you", new[] { "I'm good! Ready to help.", "Doing great!", "All systems go! 🚀" } },
            { "how's it going", new[] { "Going well!", "All good here!", "Can't complain!" } },
            { "what's up", new[] { "Not much, what do you need?", "Ready to help!", "Just chilling, what's up?" } },
            { "sup", new[] { "Sup!", "Hey!", "What's good?" } },
            
            // Bye
            { "bye", new[] { "Later! 👋", "Bye!", "See ya!" } },
            { "goodbye", new[] { "Goodbye!", "Take care!", "See you!" } },
            { "see you", new[] { "See ya!", "Later!", "👋" } },
            { "later", new[] { "Later! 👋", "Catch you later!", "✌️" } },
            { "cya", new[] { "Cya! 👋", "Later!", "✌️" } },
            
            // Affirmations
            { "yes", new[] { "👍", "Got it!", "Alright!" } },
            { "yeah", new[] { "👍", "Cool!", "Alright!" } },
            { "yep", new[] { "👍", "Cool!", "Got it!" } },
            { "no", new[] { "Okay, no problem.", "Alright.", "Got it." } },
            { "nope", new[] { "Okay.", "No worries.", "Alright." } },
            { "nah", new[] { "Okay.", "No problem.", "Got it." } },
            
            // Misc
            { "never mind", new[] { "No worries!", "Okay!", "All good." } },
            { "nevermind", new[] { "No worries!", "Okay!", "All good." } },
            { "forget it", new[] { "Forgotten! 🧠", "Done.", "Okay!" } },
            { "stop", new[] { "Stopped.", "Okay.", "Done." } },
            { "cancel", new[] { "Cancelled.", "Done.", "Okay." } },
        };
        
        // Pattern-based responses
        private static readonly List<(Regex Pattern, Func<Match, string> Response)> PatternResponses = new()
        {
            // Time
            (new Regex(@"^what('s| is) the time\??$", RegexOptions.IgnoreCase), 
                _ => $"It's {DateTime.Now:h:mm tt}"),
            (new Regex(@"^what time is it\??$", RegexOptions.IgnoreCase), 
                _ => $"{DateTime.Now:h:mm tt}"),
            (new Regex(@"^time\??$", RegexOptions.IgnoreCase), 
                _ => $"{DateTime.Now:h:mm tt}"),
            
            // Date
            (new Regex(@"^what('s| is) the date\??$", RegexOptions.IgnoreCase), 
                _ => $"{DateTime.Now:dddd, MMMM d, yyyy}"),
            (new Regex(@"^what('s| is) today('s date)?\??$", RegexOptions.IgnoreCase), 
                _ => $"{DateTime.Now:dddd, MMMM d}"),
            (new Regex(@"^what day is it\??$", RegexOptions.IgnoreCase), 
                _ => $"{DateTime.Now:dddd}"),
            
            // Math (simple)
            (new Regex(@"^what('s| is) (\d+)\s*[\+]\s*(\d+)\??$", RegexOptions.IgnoreCase), 
                m => $"{int.Parse(m.Groups[2].Value) + int.Parse(m.Groups[3].Value)}"),
            (new Regex(@"^what('s| is) (\d+)\s*[\-]\s*(\d+)\??$", RegexOptions.IgnoreCase), 
                m => $"{int.Parse(m.Groups[2].Value) - int.Parse(m.Groups[3].Value)}"),
            (new Regex(@"^what('s| is) (\d+)\s*[\*x]\s*(\d+)\??$", RegexOptions.IgnoreCase), 
                m => $"{int.Parse(m.Groups[2].Value) * int.Parse(m.Groups[3].Value)}"),
            (new Regex(@"^what('s| is) (\d+)\s*[\/]\s*(\d+)\??$", RegexOptions.IgnoreCase), 
                m => int.Parse(m.Groups[3].Value) != 0 ? $"{int.Parse(m.Groups[2].Value) / int.Parse(m.Groups[3].Value)}" : "Can't divide by zero!"),
            (new Regex(@"^(\d+)\s*[\+]\s*(\d+)\s*=?\??$", RegexOptions.IgnoreCase), 
                m => $"{int.Parse(m.Groups[1].Value) + int.Parse(m.Groups[2].Value)}"),
            (new Regex(@"^(\d+)\s*[\-]\s*(\d+)\s*=?\??$", RegexOptions.IgnoreCase), 
                m => $"{int.Parse(m.Groups[1].Value) - int.Parse(m.Groups[2].Value)}"),
            (new Regex(@"^(\d+)\s*[\*x]\s*(\d+)\s*=?\??$", RegexOptions.IgnoreCase), 
                m => $"{int.Parse(m.Groups[1].Value) * int.Parse(m.Groups[2].Value)}"),
        };
        
        private static readonly Random _random = new();
        
        /// <summary>
        /// Try to get a quick response without AI. Returns null if AI is needed.
        /// </summary>
        public static string? TryGetQuickResponse(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            
            var text = input.Trim();
            var lower = text.ToLowerInvariant();
            
            // Remove trailing punctuation for matching
            var normalized = lower.TrimEnd('.', '!', '?', ' ');
            
            // Check exact matches first
            if (QuickResponses.TryGetValue(normalized, out var responses))
            {
                return responses[_random.Next(responses.Length)];
            }
            
            // Check pattern matches
            foreach (var (pattern, responseFunc) in PatternResponses)
            {
                var match = pattern.Match(text);
                if (match.Success)
                {
                    try
                    {
                        return responseFunc(match);
                    }
                    catch { }
                }
            }
            
            // Check partial matches for greetings
            if (normalized.StartsWith("hi ") || normalized.StartsWith("hey ") || normalized.StartsWith("hello "))
            {
                return QuickResponses["hi"][_random.Next(QuickResponses["hi"].Length)];
            }
            
            // Check for "thanks for X" patterns
            if (normalized.StartsWith("thanks for") || normalized.StartsWith("thank you for"))
            {
                return QuickResponses["thanks"][_random.Next(QuickResponses["thanks"].Length)];
            }
            
            return null; // Need AI
        }
        
        /// <summary>
        /// Shorten an AI response for voice output
        /// </summary>
        public static string ShortenForVoice(string response, int maxLength = 100)
        {
            if (string.IsNullOrEmpty(response)) return response;
            if (response.Length <= maxLength) return response;
            
            // Try to find a good break point
            var shortened = response;
            
            // Remove markdown formatting
            shortened = Regex.Replace(shortened, @"\*\*([^*]+)\*\*", "$1");
            shortened = Regex.Replace(shortened, @"\*([^*]+)\*", "$1");
            shortened = Regex.Replace(shortened, @"```[\s\S]*?```", "[code]");
            shortened = Regex.Replace(shortened, @"`([^`]+)`", "$1");
            
            // Remove bullet points and lists
            shortened = Regex.Replace(shortened, @"^[\-\*•]\s*", "", RegexOptions.Multiline);
            shortened = Regex.Replace(shortened, @"^\d+\.\s*", "", RegexOptions.Multiline);
            
            // Get first sentence or two
            var sentences = Regex.Split(shortened, @"(?<=[.!?])\s+");
            var result = "";
            foreach (var sentence in sentences)
            {
                if (result.Length + sentence.Length > maxLength) break;
                result += sentence + " ";
            }
            
            if (string.IsNullOrWhiteSpace(result))
            {
                // Just truncate
                result = shortened.Substring(0, Math.Min(maxLength, shortened.Length));
                var lastSpace = result.LastIndexOf(' ');
                if (lastSpace > maxLength / 2)
                    result = result.Substring(0, lastSpace);
            }
            
            return result.Trim();
        }
        
        /// <summary>
        /// Check if input is just a simple acknowledgment that doesn't need a response
        /// </summary>
        public static bool IsAcknowledgment(string input)
        {
            var lower = input.Trim().ToLowerInvariant().TrimEnd('.', '!', '?');
            var acks = new[] { "ok", "okay", "k", "kk", "cool", "nice", "great", "awesome", 
                              "perfect", "got it", "understood", "alright", "right", "yep", 
                              "yeah", "yes", "no", "nope", "nah", "👍", "👌", "✓" };
            return acks.Contains(lower);
        }
    }
}
