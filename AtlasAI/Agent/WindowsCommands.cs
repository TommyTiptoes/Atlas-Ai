using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MinimalApp.Agent
{
    /// <summary>
    /// Comprehensive Windows command reference and execution
    /// Based on Microsoft Windows Commands documentation
    /// </summary>
    public static class WindowsCommands
    {
        /// <summary>
        /// All Windows CMD commands organized by category
        /// </summary>
        public static readonly Dictionary<string, List<CommandInfo>> CommandsByCategory = new()
        {
            ["File Management"] = new()
            {
                new("attrib", "Displays or changes file attributes", "attrib +r file.txt"),
                new("copy", "Copies files to another location", "copy file.txt backup.txt"),
                new("del", "Deletes one or more files", "del file.txt"),
                new("dir", "Displays directory contents", "dir /s /b"),
                new("erase", "Deletes one or more files (same as del)", "erase file.txt"),
                new("fc", "Compares two files", "fc file1.txt file2.txt"),
                new("find", "Searches for text in files", "find \"text\" file.txt"),
                new("findstr", "Searches for strings in files (regex)", "findstr /r \"pattern\" file.txt"),
                new("forfiles", "Selects files for batch processing", "forfiles /p C:\\ /s /m *.txt /c \"cmd /c echo @file\""),
                new("move", "Moves files from one location to another", "move file.txt folder\\"),
                new("ren", "Renames a file or directory", "ren oldname.txt newname.txt"),
                new("rename", "Renames a file or directory", "rename oldname.txt newname.txt"),
                new("replace", "Replaces files", "replace source.txt dest\\"),
                new("robocopy", "Robust file copy utility", "robocopy source dest /mir"),
                new("tree", "Displays directory structure graphically", "tree /f"),
                new("type", "Displays contents of a text file", "type file.txt"),
                new("xcopy", "Copies files and directory trees", "xcopy source dest /s /e"),
                new("mklink", "Creates symbolic links", "mklink link target"),
                new("append", "Allows programs to open files in specified directories", "append path"),
            },
            ["Directory Management"] = new()
            {
                new("cd", "Changes the current directory", "cd \\Users\\Public"),
                new("chdir", "Changes the current directory", "chdir \\Users"),
                new("md", "Creates a directory", "md newfolder"),
                new("mkdir", "Creates a directory", "mkdir newfolder"),
                new("rd", "Removes a directory", "rd emptyfolder"),
                new("rmdir", "Removes a directory", "rmdir /s /q folder"),
                new("pushd", "Saves current directory and changes to new one", "pushd \\temp"),
                new("popd", "Returns to directory saved by pushd", "popd"),
            },
            ["Disk Management"] = new()
            {
                new("active", "Marks disk partition as active (diskpart)", "active"),
                new("add", "Adds mirror to simple volume (diskpart)", "add disk=n"),
                new("add alias", "Adds aliases to alias environment", "add alias name=value"),
                new("add volume", "Adds volume to shadow copy set", "add volume c:"),
                new("assign", "Assigns drive letter to volume (diskpart)", "assign letter=E"),
                new("attach-vdisk", "Attaches virtual hard disk", "attach vdisk"),
                new("attributes", "Displays/sets disk attributes (diskpart)", "attributes disk"),
                new("automount", "Enables/disables automount feature", "automount enable"),
                new("break", "Breaks mirrored volume (diskpart)", "break disk=n"),
                new("chkdsk", "Checks disk and displays status report", "chkdsk C: /f"),
                new("chkntfs", "Displays or modifies automatic disk checking", "chkntfs /d"),
                new("clean", "Removes all partitions from disk (diskpart)", "clean"),
                new("compact", "Displays or alters compression on NTFS", "compact /c file.txt"),
                new("compact-vdisk", "Compacts virtual hard disk", "compact vdisk"),
                new("convert", "Converts FAT volumes to NTFS", "convert D: /fs:ntfs"),
                new("create", "Creates partition/volume/vdisk (diskpart)", "create partition primary"),
                new("defrag", "Defragments hard drives", "defrag C: /o"),
                new("delete", "Deletes partition/volume (diskpart)", "delete partition"),
                new("detach-vdisk", "Detaches virtual hard disk", "detach vdisk"),
                new("detail", "Shows details about disk/partition (diskpart)", "detail disk"),
                new("diskpart", "Disk partitioning utility", "diskpart"),
                new("diskperf", "Enables/disables disk performance counters", "diskperf -y"),
                new("diskshadow", "Shadow copy management", "diskshadow"),
                new("expand-vdisk", "Expands virtual hard disk", "expand vdisk maximum=20000"),
                new("extend", "Extends volume (diskpart)", "extend size=1000"),
                new("filesystems", "Displays file system info (diskpart)", "filesystems"),
                new("format", "Formats a disk", "format D: /fs:ntfs /q"),
                new("fsutil", "File system utility", "fsutil volume diskfree C:"),
                new("gpt", "Assigns GPT attributes (diskpart)", "gpt attributes=0x0000000000000001"),
                new("import", "Imports disk group (diskpart)", "import"),
                new("label", "Creates/changes/deletes volume label", "label D: MyDrive"),
                new("list", "Lists disks/partitions/volumes (diskpart)", "list disk"),
                new("merge-vdisk", "Merges differencing VHD", "merge vdisk depth=1"),
                new("mountvol", "Creates/deletes/lists volume mount points", "mountvol"),
                new("offline", "Takes disk/volume offline (diskpart)", "offline disk"),
                new("online", "Brings disk/volume online (diskpart)", "online disk"),
                new("recover", "Refreshes disk state (diskpart)", "recover"),
                new("remove", "Removes drive letter (diskpart)", "remove letter=E"),
                new("repair", "Repairs RAID-5 volume (diskpart)", "repair disk=n"),
                new("rescan", "Rescans for new disks (diskpart)", "rescan"),
                new("retain", "Prepares volume for boot (diskpart)", "retain"),
                new("san", "Displays/sets SAN policy (diskpart)", "san"),
                new("select", "Selects disk/partition/volume (diskpart)", "select disk 0"),
                new("setid", "Changes partition type (diskpart)", "setid id=07"),
                new("shrink", "Shrinks volume (diskpart)", "shrink desired=1000"),
                new("uniqueid", "Displays/sets GPT identifier (diskpart)", "uniqueid disk"),
                new("vol", "Displays disk volume label and serial number", "vol C:"),
            },
            ["System Information"] = new()
            {
                new("date", "Displays or sets the date", "date /t"),
                new("hostname", "Displays computer name", "hostname"),
                new("systeminfo", "Displays detailed system configuration", "systeminfo"),
                new("time", "Displays or sets the system time", "time /t"),
                new("ver", "Displays Windows version", "ver"),
                new("whoami", "Displays current user information", "whoami /all"),
                new("wmic", "Windows Management Instrumentation", "wmic cpu get name"),
                new("driverquery", "Lists installed device drivers", "driverquery /v"),
                new("gpresult", "Displays Group Policy information", "gpresult /r"),
            },
            ["Process Management"] = new()
            {
                new("start", "Starts a program or command", "start notepad"),
                new("tasklist", "Lists running processes", "tasklist /v"),
                new("taskkill", "Terminates processes", "taskkill /im notepad.exe /f"),
                new("schtasks", "Schedules commands and programs", "schtasks /query"),
                new("at", "Schedules commands (deprecated)", "at 12:00 cmd"),
                new("shutdown", "Shuts down or restarts computer", "shutdown /s /t 60"),
                new("logoff", "Logs off current user", "logoff"),
            },
            ["Network Commands"] = new()
            {
                new("arp", "Displays and modifies ARP cache", "arp -a"),
                new("bitsadmin", "Background Intelligent Transfer Service", "bitsadmin /list"),
                new("dnscmd", "DNS server management", "dnscmd /info"),
                new("finger", "Displays user information on remote system", "finger user@host"),
                new("ftp", "FTP client", "ftp ftp.example.com"),
                new("getmac", "Displays MAC addresses", "getmac /v"),
                new("hostname", "Displays computer name", "hostname"),
                new("ipconfig", "Displays IP configuration", "ipconfig /all"),
                new("ipxroute", "Displays/modifies IPX routing table", "ipxroute config"),
                new("irftp", "Sends files over infrared link", "irftp file.txt"),
                new("jetpack", "Compacts WINS or DHCP database", "jetpack wins.mdb temp.mdb"),
                new("mrinfo", "Displays multicast router info", "mrinfo router"),
                new("nbtstat", "Displays NetBIOS statistics", "nbtstat -n"),
                new("net", "Network commands", "net user"),
                new("net accounts", "Sets password and logon requirements", "net accounts"),
                new("net computer", "Adds/removes computers from domain", "net computer \\\\pc /add"),
                new("net config", "Displays workstation/server config", "net config workstation"),
                new("net continue", "Continues paused service", "net continue spooler"),
                new("net file", "Displays open shared files", "net file"),
                new("net group", "Manages global groups", "net group"),
                new("net help", "Displays help for net commands", "net help user"),
                new("net helpmsg", "Explains Windows error messages", "net helpmsg 3534"),
                new("net localgroup", "Manages local groups", "net localgroup administrators"),
                new("net name", "Adds/deletes messaging name", "net name"),
                new("net pause", "Pauses a service", "net pause spooler"),
                new("net print", "Displays print jobs", "net print \\\\server\\printer"),
                new("net send", "Sends messages (deprecated)", "net send * message"),
                new("net session", "Lists/disconnects sessions", "net session"),
                new("net share", "Manages shared resources", "net share"),
                new("net start", "Starts a service", "net start spooler"),
                new("net statistics", "Displays workstation/server stats", "net statistics workstation"),
                new("net stop", "Stops a service", "net stop spooler"),
                new("net time", "Synchronizes time", "net time \\\\server"),
                new("net use", "Connects to shared resources", "net use Z: \\\\server\\share"),
                new("net user", "Manages user accounts", "net user username /add"),
                new("net view", "Displays shared resources", "net view \\\\server"),
                new("netcfg", "Network configuration", "netcfg -l"),
                new("netsh", "Network shell utility", "netsh wlan show profiles"),
                new("netstat", "Displays network statistics", "netstat -an"),
                new("nfsadmin", "NFS administration", "nfsadmin server"),
                new("nfsshare", "Controls NFS shares", "nfsshare"),
                new("nfsstat", "Displays NFS statistics", "nfsstat"),
                new("nltest", "Network diagnostics", "nltest /dclist:domain"),
                new("nslookup", "DNS lookup utility", "nslookup google.com"),
                new("ntfrsutl", "NTFRS utility", "ntfrsutl version"),
                new("pathping", "Traces route with latency info", "pathping google.com"),
                new("ping", "Tests network connectivity", "ping google.com"),
                new("pktmon", "Packet monitor", "pktmon start"),
                new("portqry", "Port query utility", "portqry -n server -e 80"),
                new("rcp", "Remote copy (deprecated)", "rcp file host:file"),
                new("rdpsign", "Signs RDP files", "rdpsign file.rdp"),
                new("rexec", "Remote execution (deprecated)", "rexec host command"),
                new("route", "Displays/modifies routing table", "route print"),
                new("rpcinfo", "RPC information", "rpcinfo -p"),
                new("rpcping", "Pings RPC server", "rpcping -s server"),
                new("rsh", "Remote shell (deprecated)", "rsh host command"),
                new("telnet", "Telnet client", "telnet host 80"),
                new("tftp", "TFTP client", "tftp -i host get file"),
                new("tracert", "Traces route to destination", "tracert google.com"),
                new("waitfor", "Sends/waits for signal", "waitfor signal"),
                new("winrs", "Windows Remote Shell", "winrs -r:server cmd"),
                new("wmic", "Windows Management Instrumentation", "wmic cpu get name"),
            },
            ["User Management"] = new()
            {
                new("net user", "Manages user accounts", "net user username /add"),
                new("net localgroup", "Manages local groups", "net localgroup administrators"),
                new("runas", "Runs program as different user", "runas /user:admin cmd"),
                new("cmdkey", "Manages stored credentials", "cmdkey /list"),
            },
            ["Security"] = new()
            {
                new("cipher", "Displays/alters encryption", "cipher /e folder"),
                new("icacls", "Displays/modifies file permissions", "icacls file.txt"),
                new("cacls", "Displays/modifies ACLs (deprecated)", "cacls file.txt"),
                new("takeown", "Takes ownership of files", "takeown /f file.txt"),
            },
            ["Batch/Scripting"] = new()
            {
                new("call", "Calls another batch file", "call script.bat"),
                new("choice", "Prompts user to make a choice", "choice /c YN /m \"Continue?\""),
                new("cls", "Clears the screen", "cls"),
                new("cmd", "Starts new command shell", "cmd /c dir"),
                new("color", "Sets console colors", "color 0a"),
                new("echo", "Displays messages or toggles echo", "echo Hello World"),
                new("endlocal", "Ends localization of environment", "endlocal"),
                new("exit", "Exits command shell", "exit /b 0"),
                new("for", "Runs command for each item", "for %i in (*.txt) do echo %i"),
                new("goto", "Directs to labeled line", "goto :label"),
                new("if", "Conditional processing", "if exist file.txt echo Found"),
                new("pause", "Suspends processing", "pause"),
                new("prompt", "Changes command prompt", "prompt $p$g"),
                new("rem", "Records comments in batch file", "rem This is a comment"),
                new("set", "Displays/sets environment variables", "set PATH"),
                new("setlocal", "Begins localization of environment", "setlocal enabledelayedexpansion"),
                new("shift", "Shifts batch parameters", "shift"),
                new("title", "Sets window title", "title My Window"),
                new("timeout", "Waits for specified time", "timeout /t 5"),
            },
            ["Recovery/Repair"] = new()
            {
                new("bcdedit", "Boot configuration editor", "bcdedit /enum"),
                new("bootrec", "Boot recovery tool", "bootrec /fixmbr"),
                new("reagentc", "Windows Recovery Environment", "reagentc /info"),
                new("sfc", "System File Checker", "sfc /scannow"),
                new("dism", "Deployment Image Servicing", "dism /online /cleanup-image /restorehealth"),
            },
            ["Power Management"] = new()
            {
                new("powercfg", "Power configuration utility", "powercfg /batteryreport"),
            },
            ["Printing"] = new()
            {
                new("print", "Prints a text file", "print file.txt"),
            },
            ["Other Utilities"] = new()
            {
                new("assoc", "Displays/modifies file associations", "assoc .txt"),
                new("clip", "Copies output to clipboard", "dir | clip"),
                new("comp", "Compares contents of two files", "comp file1 file2"),
                new("doskey", "Edits command lines and creates macros", "doskey /history"),
                new("expand", "Expands compressed files", "expand file.cab"),
                new("ftype", "Displays/modifies file type associations", "ftype txtfile"),
                new("help", "Provides help for commands", "help dir"),
                new("mode", "Configures system devices", "mode con cols=120 lines=50"),
                new("more", "Displays output one screen at a time", "type file.txt | more"),
                new("openfiles", "Displays files opened by remote users", "openfiles /query"),
                new("path", "Displays/sets search path", "path"),
                new("recover", "Recovers readable info from bad disk", "recover file.txt"),
                new("reg", "Registry command-line tool", "reg query HKLM\\SOFTWARE"),
                new("regsvr32", "Registers/unregisters DLLs", "regsvr32 file.dll"),
                new("sort", "Sorts input", "sort file.txt"),
                new("subst", "Associates path with drive letter", "subst X: C:\\folder"),
                new("where", "Locates files matching pattern", "where notepad"),
            },
        };

        /// <summary>
        /// Quick lookup of all commands
        /// </summary>
        public static readonly Dictionary<string, CommandInfo> AllCommands = BuildAllCommands();

        private static Dictionary<string, CommandInfo> BuildAllCommands()
        {
            var all = new Dictionary<string, CommandInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var category in CommandsByCategory.Values)
            {
                foreach (var cmd in category)
                {
                    all[cmd.Name] = cmd;
                }
            }
            return all;
        }

        /// <summary>
        /// Execute a Windows command and return the output
        /// </summary>
        public static async Task<string> ExecuteAsync(string command, int timeoutMs = 30000)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return "❌ Failed to start command";

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(timeoutMs))
                {
                    process.Kill();
                    return "❌ Command timed out";
                }

                var output = await outputTask;
                var error = await errorTask;

                if (!string.IsNullOrEmpty(error) && string.IsNullOrEmpty(output))
                    return $"❌ Error: {error}";

                return string.IsNullOrEmpty(output) ? "✓ Command completed (no output)" : output;
            }
            catch (Exception ex)
            {
                return $"❌ Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Get help for a specific command
        /// </summary>
        public static string GetHelp(string commandName)
        {
            if (AllCommands.TryGetValue(commandName, out var cmd))
            {
                return $"📘 **{cmd.Name}**\n\n" +
                       $"{cmd.Description}\n\n" +
                       $"Example: `{cmd.Example}`\n\n" +
                       $"💡 Run `{cmd.Name} /?` for full help";
            }
            return $"❓ Unknown command: {commandName}. Try 'list commands' to see all available commands.";
        }

        /// <summary>
        /// List all commands or commands in a category
        /// </summary>
        public static string ListCommands(string? category = null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("📋 **Windows Commands Reference**\n");

            if (string.IsNullOrEmpty(category))
            {
                // List categories
                sb.AppendLine("Categories:");
                foreach (var cat in CommandsByCategory.Keys)
                {
                    sb.AppendLine($"  • {cat} ({CommandsByCategory[cat].Count} commands)");
                }
                sb.AppendLine("\nSay 'list [category] commands' for details, or 'help [command]' for specific help.");
            }
            else
            {
                // Find matching category
                foreach (var kvp in CommandsByCategory)
                {
                    if (kvp.Key.Contains(category, StringComparison.OrdinalIgnoreCase))
                    {
                        sb.AppendLine($"**{kvp.Key}:**\n");
                        foreach (var cmd in kvp.Value)
                        {
                            sb.AppendLine($"  `{cmd.Name}` - {cmd.Description}");
                        }
                        return sb.ToString();
                    }
                }
                sb.AppendLine($"Category '{category}' not found. Available: {string.Join(", ", CommandsByCategory.Keys)}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Try to handle a command request
        /// </summary>
        public static async Task<string?> TryHandleAsync(string input)
        {
            var lower = input.ToLowerInvariant().Trim();

            // List commands
            if (lower == "list commands" || lower == "show commands" || lower == "windows commands")
            {
                return ListCommands();
            }

            // List category commands
            if (lower.StartsWith("list ") && lower.EndsWith(" commands"))
            {
                var category = lower.Replace("list ", "").Replace(" commands", "").Trim();
                return ListCommands(category);
            }

            // Help for specific command
            if (lower.StartsWith("help ") || lower.StartsWith("what is ") || lower.StartsWith("explain "))
            {
                var cmdName = lower.Replace("help ", "").Replace("what is ", "").Replace("explain ", "").Trim();
                if (AllCommands.ContainsKey(cmdName))
                {
                    return GetHelp(cmdName);
                }
            }

            // Direct command execution (if it starts with a known command)
            var firstWord = lower.Split(' ')[0];
            if (AllCommands.ContainsKey(firstWord))
            {
                // Execute the command
                return await ExecuteAsync(input);
            }

            return null;
        }
    }

    public class CommandInfo
    {
        public string Name { get; }
        public string Description { get; }
        public string Example { get; }

        public CommandInfo(string name, string description, string example)
        {
            Name = name;
            Description = description;
            Example = example;
        }
    }
}
