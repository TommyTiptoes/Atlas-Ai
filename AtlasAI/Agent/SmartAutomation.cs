using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

namespace AtlasAI.Agent
{
    /// <summary>
    /// Smart Automation - Create custom voice-triggered automations.
    /// "When I say X, do Y" - like IFTTT but for your PC.
    /// </summary>
    public class SmartAutomation
    {
        private static SmartAutomation? _instance;
        public static SmartAutomation Instance => _instance ??= new SmartAutomation();
        
        private readonly string _automationsFile;
        private List<Automation> _automations = new();
        private readonly Dictionary<string, Timer> _scheduledTimers = new();
        
        public event Action<string>? OnAutomationTriggered;
        
        private SmartAutomation()
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AtlasAI");
            Directory.CreateDirectory(appData);
            _automationsFile = Path.Combine(appData, "automations.json");
            LoadAutomations();
            SetupScheduledAutomations();
        }
        
        /// <summary>
        /// Create a new automation
        /// </summary>
        public string CreateAutomation(string trigger, List<string> actions, string? schedule = null)
        {
            var automation = new Automation
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Trigger = trigger.ToLowerInvariant(),
                Actions = actions,
                Schedule = schedule,
                CreatedAt = DateTime.Now,
                IsEnabled = true
            };
            
            _automations.Add(automation);
            SaveAutomations();
            
            if (!string.IsNullOrEmpty(schedule))
                SetupScheduledTimer(automation);
            
            return $"✓ Created automation '{trigger}' with {actions.Count} action(s)";
        }
        
        /// <summary>
        /// Check if input matches an automation trigger
        /// </summary>
        public async Task<string?> TryTriggerAsync(string input)
        {
            var lower = input.ToLowerInvariant();
            
            var automation = _automations.FirstOrDefault(a => 
                a.IsEnabled && 
                string.IsNullOrEmpty(a.Schedule) && // Only manual triggers
                (lower == a.Trigger || lower.Contains(a.Trigger) || a.Trigger.Contains(lower)));
            
            if (automation == null) return null;
            
            return await ExecuteAutomationAsync(automation);
        }
        
        /// <summary>
        /// Execute an automation's actions
        /// </summary>
        public async Task<string> ExecuteAutomationAsync(Automation automation)
        {
            var results = new List<string>();
            automation.LastTriggered = DateTime.Now;
            automation.TriggerCount++;
            
            OnAutomationTriggered?.Invoke(automation.Trigger);
            
            foreach (var action in automation.Actions)
            {
                try
                {
                    var result = await Tools.DirectActionHandler.TryHandleAsync(action);
                    results.Add(result ?? $"Executed: {action}");
                    await Task.Delay(500); // Small delay between actions
                }
                catch (Exception ex)
                {
                    results.Add($"❌ Failed: {action} - {ex.Message}");
                }
            }
            
            SaveAutomations();
            return $"🤖 Automation '{automation.Trigger}':\n" + string.Join("\n", results);
        }
        
        /// <summary>
        /// List all automations
        /// </summary>
        public string ListAutomations()
        {
            if (!_automations.Any())
                return "No automations created yet. Say 'create automation' to get started!";
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🤖 **Your Automations:**\n");
            
            foreach (var a in _automations)
            {
                var status = a.IsEnabled ? "✓" : "○";
                var schedule = !string.IsNullOrEmpty(a.Schedule) ? $" ⏰ {a.Schedule}" : "";
                sb.AppendLine($"{status} **\"{a.Trigger}\"**{schedule}");
                sb.AppendLine($"   Actions: {string.Join(" → ", a.Actions)}");
                if (a.TriggerCount > 0)
                    sb.AppendLine($"   Used {a.TriggerCount}x, last: {a.LastTriggered:g}");
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Delete an automation
        /// </summary>
        public string DeleteAutomation(string trigger)
        {
            var automation = _automations.FirstOrDefault(a => 
                a.Trigger.Contains(trigger.ToLower()) || trigger.ToLower().Contains(a.Trigger));
            
            if (automation == null)
                return $"❌ Automation '{trigger}' not found";
            
            _automations.Remove(automation);
            if (_scheduledTimers.TryGetValue(automation.Id, out var timer))
            {
                timer.Stop();
                timer.Dispose();
                _scheduledTimers.Remove(automation.Id);
            }
            SaveAutomations();
            
            return $"✓ Deleted automation '{automation.Trigger}'";
        }
        
        /// <summary>
        /// Toggle automation enabled/disabled
        /// </summary>
        public string ToggleAutomation(string trigger)
        {
            var automation = _automations.FirstOrDefault(a => 
                a.Trigger.Contains(trigger.ToLower()) || trigger.ToLower().Contains(a.Trigger));
            
            if (automation == null)
                return $"❌ Automation '{trigger}' not found";
            
            automation.IsEnabled = !automation.IsEnabled;
            SaveAutomations();
            
            return automation.IsEnabled 
                ? $"✓ Enabled automation '{automation.Trigger}'"
                : $"○ Disabled automation '{automation.Trigger}'";
        }
        
        /// <summary>
        /// Parse natural language automation creation
        /// </summary>
        public (string? Trigger, List<string>? Actions, string? Schedule) ParseAutomationRequest(string input)
        {
            var lower = input.ToLowerInvariant();
            
            // "when I say X, do Y"
            var match = System.Text.RegularExpressions.Regex.Match(input, 
                @"when\s+(?:i\s+say\s+)?[""']?(.+?)[""']?\s*,?\s*(?:do|then|run|execute)\s+(.+)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                var trigger = match.Groups[1].Value.Trim().Trim('"', '\'');
                var actionsStr = match.Groups[2].Value.Trim();
                var actions = actionsStr.Split(new[] { " and ", " then ", ", " }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .ToList();
                return (trigger, actions, null);
            }
            
            // "every day at X, do Y"
            match = System.Text.RegularExpressions.Regex.Match(input,
                @"(?:every\s+)?(\w+)\s+at\s+(\d{1,2}(?::\d{2})?\s*(?:am|pm)?)\s*,?\s*(?:do|then|run)\s+(.+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                var frequency = match.Groups[1].Value.Trim();
                var time = match.Groups[2].Value.Trim();
                var actionsStr = match.Groups[3].Value.Trim();
                var actions = actionsStr.Split(new[] { " and ", " then ", ", " }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .ToList();
                var schedule = $"{frequency} at {time}";
                return ($"scheduled_{Guid.NewGuid():N}"[..12], actions, schedule);
            }
            
            return (null, null, null);
        }
        
        private void SetupScheduledAutomations()
        {
            foreach (var automation in _automations.Where(a => !string.IsNullOrEmpty(a.Schedule) && a.IsEnabled))
            {
                SetupScheduledTimer(automation);
            }
        }
        
        private void SetupScheduledTimer(Automation automation)
        {
            // Parse schedule and set up timer
            // For now, simple daily scheduling
            if (automation.Schedule?.Contains("at") == true)
            {
                var timeMatch = System.Text.RegularExpressions.Regex.Match(automation.Schedule, @"(\d{1,2})(?::(\d{2}))?\s*(am|pm)?");
                if (timeMatch.Success)
                {
                    var hour = int.Parse(timeMatch.Groups[1].Value);
                    var minute = timeMatch.Groups[2].Success ? int.Parse(timeMatch.Groups[2].Value) : 0;
                    var ampm = timeMatch.Groups[3].Value.ToLower();
                    
                    if (ampm == "pm" && hour < 12) hour += 12;
                    if (ampm == "am" && hour == 12) hour = 0;
                    
                    var now = DateTime.Now;
                    var scheduledTime = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
                    if (scheduledTime <= now)
                        scheduledTime = scheduledTime.AddDays(1);
                    
                    var delay = (scheduledTime - now).TotalMilliseconds;
                    
                    var timer = new Timer(delay);
                    timer.Elapsed += async (s, e) =>
                    {
                        timer.Interval = 24 * 60 * 60 * 1000; // Reset to daily
                        await ExecuteAutomationAsync(automation);
                    };
                    timer.AutoReset = true;
                    timer.Start();
                    
                    _scheduledTimers[automation.Id] = timer;
                }
            }
        }
        
        private void LoadAutomations()
        {
            try
            {
                if (File.Exists(_automationsFile))
                {
                    var json = File.ReadAllText(_automationsFile);
                    _automations = JsonSerializer.Deserialize<List<Automation>>(json) ?? new();
                }
            }
            catch { _automations = new(); }
        }
        
        private void SaveAutomations()
        {
            try
            {
                var json = JsonSerializer.Serialize(_automations, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_automationsFile, json);
            }
            catch { }
        }
    }
    
    public class Automation
    {
        public string Id { get; set; } = "";
        public string Trigger { get; set; } = "";
        public List<string> Actions { get; set; } = new();
        public string? Schedule { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastTriggered { get; set; }
        public int TriggerCount { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
