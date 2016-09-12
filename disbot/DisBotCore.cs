using Discord;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DColor = System.Drawing.Color;

namespace DisBot {
    public static class DisBotCore {

        public static string NL;

        public static readonly Lazy<Version> Version = new Lazy<Version>(() => Assembly.GetExecutingAssembly().GetName().Version);
        public static readonly Lazy<string> PublicIP = new Lazy<string>(delegate () {
            using (WebClient wc = new WebClient()) {
                return wc.DownloadString("http://ipinfo.io/ip").Trim();
            }
        });
        public static readonly Lazy<string> Hostname = new Lazy<string>(delegate () {
            string hostname = Dns.GetHostName();
            if (hostname != "localhost") {
                return hostname;
            }
            return Environment.MachineName;
        });
        public static readonly Lazy<string> OS = new Lazy<string>(delegate () {
            if (Environment.OSVersion.Platform != PlatformID.Unix) {
                return Environment.OSVersion.ToString();
            }

            StringBuilder data = new StringBuilder();
            Process p;

            p = new Process();
            p.StartInfo.FileName = "which";
            p.StartInfo.Arguments = "lsb_release";
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.UseShellExecute = false;
            p.EnableRaisingEvents = true;
            
            p.OutputDataReceived += new DataReceivedEventHandler(
                delegate (object sender, DataReceivedEventArgs e) {
                    data.AppendLine(e.Data);
                }
            );
            p.Start();
            p.BeginOutputReadLine();
            p.StandardInput.WriteLine();
            p.WaitForExit();
            p.CancelOutputRead();
            if (data.Length == 0) {
                return Environment.OSVersion.ToString();
            }

            p = new Process();
            p.StartInfo.FileName = "lsb_release";
            p.StartInfo.Arguments = "-a";
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.UseShellExecute = false;
            p.EnableRaisingEvents = true;
            data.Clear();
            p.OutputDataReceived += new DataReceivedEventHandler(
                delegate (object sender, DataReceivedEventArgs e) {
                    data.AppendLine(e.Data);
                }
            );
            p.Start();
            p.BeginOutputReadLine();
            p.StandardInput.WriteLine();
            p.WaitForExit();
            p.CancelOutputRead();

            string desc = data.ToString();
            desc = desc.Substring(desc.IndexOf("Description")).Trim();
            desc = desc.Substring(desc.IndexOf(':') + 1).Trim();
            desc = desc.Substring(0, desc.IndexOf('\n')).Trim();

            data.Clear();
            data.Append(desc);
            data.Append(" (Linux ").Append(Environment.OSVersion.Version).Append(")");

            return data.ToString();
        });

        private static DiscordClient Client;

        public readonly static string RootDir = "disbot";

        public readonly static string GlobalLogFile = "globallog.txt";
        public readonly static string TokenFile = "token.txt";

        public static Random RNG;

        public static Dictionary<ulong, DisBotServerConfig> Servers = new Dictionary<ulong, DisBotServerConfig>();

        public static List<DisBotCommand> Commands = new List<DisBotCommand>();
        public static List<DisBotParser> Parsers = new List<DisBotParser>();

        public static Bitmap SharedBitmap;
        public static Graphics SharedGraphics;

        public readonly static bool IsMono = Type.GetType("Mono.Runtime") != null;

        public static ulong OverlordID = 93713629047697408UL;

        public static Regex PathVerifyRegex = new Regex("[" + Regex.Escape(new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars())) + "]");

        static DisBotCore() {
            try {
                CheckGDI();

                SharedBitmap = new Bitmap(1, 1);
                SharedGraphics = Graphics.FromImage(SharedBitmap);
            } catch (Exception) { /* GDI unsupported. */ }
        }

        public static void Main(string[] args) {
            if (args.Length == 1 && args[0] == "ouya") {
                ServicePointManager.ServerCertificateValidationCallback = delegate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
                    if (sslPolicyErrors != SslPolicyErrors.None) {
                        for (int i = 0; i < chain.ChainStatus.Length; i++) {
                            if (chain.ChainStatus[i].Status != X509ChainStatusFlags.RevocationStatusUnknown) {
                                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                                chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                                chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                                if (!chain.Build((X509Certificate2) certificate)) {
                                    return false;
                                }
                            }
                        }
                    }
                    return true;
                };
            }

            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs eargs) {
                Exception e = (Exception) eargs.ExceptionObject;
                
                if (string.IsNullOrEmpty(GlobalLogFile)) return;
                string log = Path.Combine(RootDir, GlobalLogFile);
                // TODO better.
                lock (GlobalLogFile)
                try {
                    File.AppendAllText(log, "----------------\nCRITICAL! CRITICAL!----------------\n" + e.ToString() + "\n----------------\n\n");
                } catch (IOException) { /* Disk IO. */ }
            };

            Init();
            Run();
        }

        public static void Init() {
            if (Client != null) return;
            NL = "\n";
            if (Environment.OSVersion.Platform != PlatformID.Unix && Environment.OSVersion.Platform != PlatformID.MacOSX) {
                NL = "\r\n";
            }

            RNG = new Random();

            Client = new DiscordClient(new DiscordConfigBuilder() {
                MessageCacheSize = 10,
                ConnectionTimeout = 180000
            });

            Client.MessageReceived += MessageReceived;
            Client.JoinedServer += (object s, ServerEventArgs e) => GetServer(e.Server, true);
            Client.ServerAvailable += (object s, ServerEventArgs e) => GetServer(e.Server, true);
            Client.LeftServer += (object s, ServerEventArgs e) => RemoveServer(e.Server);

            Commands.Add(new DisBotDCommand() {
                Name = "help",
                Info = "Yet another help command.",
                Help = "<command to get help for>",
                OnRun = delegate (DisBotDCommand cmd_, DisBotServerConfig server, Message msg, DisBotCommandArg[] args) {
                    StringBuilder builder = new StringBuilder();

                    if (args.Length == 0) {
                        builder.Append("**disbot ").Append(Version.Value.ToString()).AppendLine("**");
                        builder.Append("This server has got **").Append(server.Commands.Count).Append(" commands** and **").Append(server.Parsers.Count).AppendLine(" parsers.**");
                        builder.AppendLine();

                        builder.AppendLine("**Commands:**");
                        for (int i = 0; i < server.Commands.Count; i++) {
                            DisBotCommand cmd = server.Commands[i];
                            builder.Append(server.Prefix).Append(cmd.Name).AppendLine(": ");
                            builder.Append(" ").Append(cmd.Info).AppendLine();
                        }
                        builder.AppendLine($"*(Use a command with {server.Prefix}{server.Prefix} to make your message vanish!)*");
                        builder.AppendLine();

                        builder.AppendLine("**Parsers:**");
                        for (int i = 0; i < server.Parsers.Count; i++) {
                            DisBotParser parser = server.Parsers[i];
                            builder.Append(parser.Name).AppendLine(": ");
                            builder.Append(" ").Append(parser.Info).AppendLine();
                        }
                        builder.AppendLine();

                    } else {
                        for (int i = 0; i < args.Length; i++) {
                            string cmdName = args[i];
                            if (cmdName.StartsWithInvariant(server.Prefix)) {
                                cmdName = cmdName.Substring(server.Prefix.Length);
                            }

                            DisBotCommand cmd = server.GetCommand(cmdName);
                            if (cmd == null) {
                                builder.Append("Command not found: `").Append(cmdName).AppendLine("`");
                                builder.AppendLine();
                                continue;
                            }

                            builder.Append(cmd.Name).AppendLine(": ");
                            builder.Append(" ").Append(cmd.Info).AppendLine();
                            builder.Append(" `").Append(server.Prefix).Append(cmd.Name).Append(" ").AppendLine(cmd.Help).AppendLine("`");
                            builder.AppendLine();
                        }
                    }

                    server.Send(msg.Channel, builder.ToString());
                }
            });

            Commands.Add(new DisBotDCommand() {
                Name = "bot",
                Info = "Info about the current disbot instance.",
                Help = "",
                OnRun = delegate (DisBotDCommand cmd, DisBotServerConfig server, Message msg, DisBotCommandArg[] args) {
                    StringBuilder builder = new StringBuilder();
                    builder.Append("**disbot ").Append(Version.Value.ToString()).AppendLine("** by 0x0ade.");
                    builder
                        .Append("Running on *").Append(OS.Value)
                        .Append("* @ `").Append(Hostname.Value)
                        .Append(" (").Append(PublicIP.Value).AppendLine(")`");

                    if (IsMono) {
                        builder.AppendLine("Using *Mono.*");
                    } else {
                        builder.AppendLine("Using *.NET Framework.*");
                    }

                    builder.AppendLine();
                    builder.Append("disbot is being used on **").Append(Servers.Count).AppendLine("** servers!");

                    if (server.Server == null) {
                        server.Send(msg.Channel, builder.ToString());
                        return;
                    }

                    builder.AppendLine();
                    builder.AppendLine("Info for this server:");
                    builder.Append("**").Append(server.Commands.Count).AppendLine("** commands.");
                    builder.Append("**").Append(server.Aliases.Count).AppendLine("** aliases.");
                    builder.Append("**").Append(server.Parsers.Count).AppendLine("** parsers.");
                    builder.Append("**").Append(server.Images.Count).AppendLine("** images.");

                    server.Send(msg.Channel, builder.ToString());
                }
            });

            Commands.Add(new DisBotDCommand() {
                Name = "conf",
                Info = "Configuration management command.",
                Help = "export | import [data] | get [prop] | set [prop] [value]",
                OnRun = delegate (DisBotDCommand cmd_, DisBotServerConfig server, Message msg, DisBotCommandArg[] args) {
                    if (server.Server == null) {
                        return;
                    }

                    if (args.Length == 1 && args[0] == "export") {
                        server.Save();
                        server.Send(msg.Channel, $"```\n{File.ReadAllText(Path.Combine(RootDir, server.Dir, server.ConfigFile))}\n```");
                        return;
                    }

                    if (args.Length == 2 && args[0] == "get") {
                        Func<string> getter;
                        if (!server.OnSave.TryGetValue(args[1], out getter)) {
                            server.Send(msg.Channel, $"Property `{args[1]}` not found! Property names are case-sensitive!");
                            return;
                        }
                        server.Send(msg.Channel, $"```\n{getter()}\n```");
                        return;
                    }

                    if (!server.IsBotCommander(msg.User, msg)) {
                        return;
                    }

                    if (args.Length >= 2 && args[0] == "import") {
                        string data = msg.Text.Substring(msg.Text.IndexOf(' ') + 6 + 1);
                        data = data.Trim('`', ' ', '\n').Trim();
                        File.WriteAllText(Path.Combine(RootDir, server.Dir, server.ConfigFile), data);
                        server.Load();
                        server.Save();
                        server.Send(msg.Channel, "Data imported.");
                        return;
                    }

                    if (args.Length >= 2 && args[0] == "name") {
                        if (!server.IsBotOverlord(msg.User, msg)) {
                            return;
                        }

                        Task.Run(async delegate () {
                            try {
                                string data = msg.Text.Substring(msg.Text.IndexOf(' ') + 4 + 1);
                                data = data.Trim();
                                await Client.CurrentUser.Edit(username: data);
                                server.Send(msg.Channel, "Name changed.");
                            } catch (Exception e) {
                                server.Send(msg.Channel, $"Could not change name! Consult `{server.Prefix}log internal`");
                                server.Log("internal", e.ToString());
                            }
                        });
                        return;
                    }

                    if (args.Length >= 2 && args[0] == "avatar") {
                        if (!server.IsBotOverlord(msg.User, msg)) {
                            return;
                        }

                        Task.Run(async delegate () {
                            try {
                                string data = msg.Text.Substring(msg.Text.IndexOf(' ') + 6 + 1);
                                data = data.Trim();
                                using (MemoryStream ms = new MemoryStream()) {
                                    using (WebClient wc = new WebClient())
                                    using (Stream s = wc.OpenRead(data)) {
                                        s.CopyTo(ms);
                                    }

                                    ms.Position = 0;
                                    await Client.CurrentUser.Edit(avatar: ms);
                                }
                                server.Send(msg.Channel, "Avatar changed.");
                            } catch (Exception e) {
                                server.Send(msg.Channel, $"Could not change avatar! Consult `{server.Prefix}log internal`");
                                server.Log("internal", e.ToString());
                            }
                        });
                        return;
                    }

                    if (args.Length >= 2 && args[0] == "game") {
                        if (!server.IsBotOverlord(msg.User, msg)) {
                            return;
                        }

                        string data = msg.Text.Substring(msg.Text.IndexOf(' ') + 4 + 1);
                        data = data.Trim();
                        Client.SetGame(data);
                        server.Send(msg.Channel, "Game changed.");
                        return;
                    }

                    if (args.Length >= 3 && args[0] == "set") {
                        Action<string> setter;
                        if (!server.OnLoad.TryGetValue(args[1], out setter)) {
                            server.Send(msg.Channel, $"Property `{args[1]}` not found! Property names are case-sensitive!");
                            return;
                        }
                        string data = msg.Text.Substring(msg.Text.IndexOf(' ') + 3 + 1 + args[1].String.Length + 1);
                        data = data.Trim('`', ' ', '\n').Trim();
                        setter(data);
                        server.Save();
                        server.Send(msg.Channel, $"Property `{args[1]}` updated.");
                        return;
                    }

                    Task.Run(() => server.GetCommand("help").Run(server, msg, new DisBotCommandArg(cmd_.Name)));
                    return;
                }
            });

            Commands.Add(new DisBotDCommand() {
                Name = "alias",
                Info = "Alias management command.",
                Help = "+ [alias] [cmd] <args> | - [alias] | [alias]",
                OnRun = delegate (DisBotDCommand cmd_, DisBotServerConfig server, Message msg, DisBotCommandArg[] args) {
                    if (server.Server == null) {
                        return;
                    }

                    if (args.Length == 2 && args[0] == "-") {
                        Tuple<string, string> alias = server.GetAliasTuple(args[1]);
                        if (alias == null) {
                            server.Send(msg.Channel, $"Alias `{args[1]}` not found! Add a new one via `{server.Prefix}{cmd_.Name} + {args[1]} [cmd] <args>`");
                            return;
                        }

                        server.Aliases.Remove(alias);
                        server.Save();
                        server.Send(msg.Channel, $"Alias `{args[1]}` removed!");
                        return;
                    }

                    if (args.Length >= 3 && (args[0] == "+" || args[0] == "add")) {
                        DisBotCommand cmd = server.GetCommand(args[1]);
                        if (cmd != null) {
                            server.Send(msg.Channel, $"Command `{args[1]}` already existing!");
                            return;
                        }

                        string aliasName = args[1].String;
                        if (aliasName.StartsWithInvariant(server.Prefix)) {
                            aliasName = aliasName.Substring(server.Prefix.Length);
                        }

                        Tuple<string, string> alias = server.GetAliasTuple(aliasName);
                        if (alias != null) {
                            server.Send(msg.Channel, $"Alias `{aliasName}` already existing!");
                            return;
                        }

                        string aliasCmd = msg.Text.Substring(
                            msg.Text.IndexOf(' ') +
                            args[0].String.Length + 1 +
                            args[1].String.Length + 1
                        ).Trim();
                        if (aliasCmd.StartsWithInvariant(server.Prefix)) {
                            aliasCmd = aliasCmd.Substring(server.Prefix.Length);
                        }

                        server.Aliases.Add(Tuple.Create(aliasName, aliasCmd));
                        server.Save();
                        server.Send(msg.Channel, $"Alias `{aliasName}` added!");
                        return;
                    }

                    StringBuilder builder = new StringBuilder();

                    if (args.Length == 0) {
                        builder.Append("This server has got **").Append(server.Commands.Count).Append(" aliases.**");
                        builder.AppendLine();

                        for (int i = 0; i < server.Aliases.Count; i++) {
                            Tuple<string, string> alias = server.Aliases[i];
                            builder.Append("Alias `").Append(alias.Item1).AppendLine("`:");
                            builder.AppendLine("```");
                            builder.Append(server.Prefix).AppendLine(alias.Item2);
                            builder.AppendLine("```");
                        }
                        builder.AppendLine();
                    } else {
                        for (int i = 0; i < args.Length; i++) {
                            string aliasName = args[i];
                            if (aliasName.StartsWithInvariant(server.Prefix)) {
                                aliasName = aliasName.Substring(server.Prefix.Length);
                            }
                            string aliasCmd = server.GetAlias(aliasName);
                            if (aliasCmd == null) {
                                server.Send(msg.Channel, $"Alias `{aliasName}` not found! Add a new one via `{server.Prefix}{cmd_.Name} + {aliasName} [cmd] <args>`");
                                continue;
                            }

                            builder.Append("Alias `").Append(aliasName).AppendLine("`:");
                            builder.AppendLine("```");
                            builder.Append(server.Prefix).AppendLine(aliasCmd);
                            builder.AppendLine("```");
                        }
                    }

                    server.Send(msg.Channel, builder.ToString());
                    return;
                }
            });

            Commands.Add(new DisBotDCommand() {
                Name = "log",
                Info = "Gives you a look at what happened in this server.",
                Help = "<tag (default: all) / list> <range (n, -n, a-b, a+n)>",
                OnRun = delegate (DisBotDCommand cmd, DisBotServerConfig server, Message msg, DisBotCommandArg[] args) {
                    if (server.Server == null) {
                        return;
                    }

                    StringBuilder builder = new StringBuilder();

                    CircularBuffer<string> buffer = args.Length == 0 ? server.LogBuffer : server.GetLogBuffer(args[0]);

                    if (buffer == null) {
                        if (args[0] == "list") {
                            builder.AppendLine("Available tags:");
                            goto ListTags;
                        }
                    }

                    if (buffer == null) {
                        builder.AppendLine($"Log with tag `{args[0]}` not found! Available tags:");
                        goto ListTags;
                    }

                    int offset = 0;
                    int size = int.MaxValue;
                    if (args.Length != 0) {
                        buffer = buffer ?? server.LogBuffer;
                        string sizeStr = args[args.Length - 1];
                        string[] sizeStrSplit;
                        if (int.TryParse(sizeStr, out size)) { if (size < 0) { offset = buffer.CurrentSize + size; size = -size; } } else if ((sizeStrSplit = sizeStr.Split('+')).Length == 2 && int.TryParse(sizeStrSplit[0], out offset) && int.TryParse(sizeStrSplit[1], out size)) { } else if ((sizeStrSplit = sizeStr.Split('-')).Length == 2 && int.TryParse(sizeStrSplit[0], out offset) && int.TryParse(sizeStrSplit[1], out size)) { size -= offset; } else { size = int.MaxValue; }
                    }

                    if (buffer != null) {
                        builder.AppendLine("```");
                        offset = Math.Max(0, offset);
                        size = Math.Min(buffer.CurrentSize - offset, size);
                        for (int i = offset; i < offset + size; i++) {
                            builder.AppendLine(buffer[i].Replace("```", "---"));
                        }
                        builder.AppendLine("```");
                    }

                    if (builder.Length > DiscordConfig.MaxMessageSize) {
                        server.Send(msg.Channel, "The log is quite big. Sending it to you via PM.");

                        Task.Run(async delegate () {
                            Task<Channel> channelT = msg.User.CreatePMChannel();
                            List<string> texts = new List<string>();
                            string text = builder.ToString();
                            while (text.Length > DiscordConfig.MaxMessageSize) {
                                int lastnl = text.LastIndexOf("\n", DiscordConfig.MaxMessageSize, DiscordConfig.MaxMessageSize);
                                if (lastnl < 0) lastnl = DiscordConfig.MaxMessageSize;
                                texts.Add(text.Substring(0, lastnl));
                                text = text.Substring(lastnl);
                            }
                            texts.Add(text);
                            Channel channel = await channelT;
                            for (int i = 0; i < texts.Count; i++) {
                                text = texts[i];
                                if (i != 0) text = "```\n" + text;
                                if (i != texts.Count - 1) text = text + "\n```";
                                server.Send(channel, text);
                            }
                        });
                        return;
                    }
                    server.Send(msg.Channel, builder.ToString());
                    return;

                    ListTags:
                    builder.AppendLine($" `all`");
                    foreach (string tag in server.LogTags) {
                        builder.AppendLine($" `{tag}`");
                    }
                    server.Send(msg.Channel, builder.ToString());
                    return;
                }
            });

            Commands.Add(new DisBotDCommand() {
                Name = "echo",
                Info = "Repeats what you said.",
                Help = "[anything]",
                OnRun = (cmd, server, msg, args) => server.Send(msg.Channel, msg.Text.Substring(Math.Max(0, msg.Text.IndexOf(' '))).Trim())
            });

            Commands.Add(new DisBotDCommand() {
                Name = "mute",
                Info = "Mutes someone. Except it doesn't.",
                Help = "[anything]",
                OnRun = (cmd, server, msg, args) => server.Send(msg.Channel, msg.Text.Substring(msg.Text.IndexOf(' ')).Trim() + " muted.")
            });

            Commands.Add(new DisBotDCommand() {
                Name = "img",
                Info = "Your friendly image vault.",
                Help = "[any search query] | + [url] | + [attached image] | - [name] <number> | ~ <oldname> [newname]",
                OnRun = delegate (DisBotDCommand cmd, DisBotServerConfig server, Message msg, DisBotCommandArg[] args) {
                    if (server.Server == null) {
                        return;
                    }

                    string url, imguri;

                    if (args.Length == 2) {
                        if (args[0] == "++") {
                            if (!server.IsBotCommander(msg.User, msg)) {
                                return;
                            }
                            if (!server.ImageBlacklist.Contains(args[1])) {
                                server.Send(msg.Channel, $"`{args[1]}` not in blacklist.");
                                return;
                            }

                            server.ImageBlacklist.Remove(args[1]);
                            server.Save();
                            server.Send(msg.Channel, $"Removed `{args[1]}` from blacklist.");
                            return;
                        }
                        if (args[0] == "--") {
                            if (!server.IsBotCommander(msg.User, msg)) {
                                return;
                            }
                            if (server.ImageBlacklist.Contains(args[1])) {
                                server.Send(msg.Channel, $"`{args[1]}` already in blacklist.");
                                return;
                            }

                            server.ImageBlacklist.Add(args[1]);
                            server.Save();
                            server.Send(msg.Channel, $"Added `{args[1]}` to blacklist.");
                            return;
                        }
                    }

                    if (((1 <= args.Length && args.Length <= 2) && args[0].String.StartsWithInvariant("+")) || (args.Length == 2 && args[0] == "add")) {
                        if (msg.Attachments.Length == 0 && args.Length == 1 && args[0].String.Length == 1) {
                            server.Send(msg.Channel, "You didn't attach any image to your message!");
                            return;
                        }

                        if (args[0].String.Length == 1 && args.Length == 1) {
                            if (msg.Attachments[0].Width == null) {
                                server.Send(msg.Channel, "Not an image, you dirty liar!");
                                return;
                            }
                            url = msg.Attachments[0].Url;
                        } else {
                            if (args.Length == 2) url = args[1];
                            else url = args[0].String.Substring(1);
                            string urll = url.ToLowerInvariant();
                            if (
                                !urll.EndsWith(".png") &&
                                !urll.EndsWith(".jpg") &&
                                !urll.EndsWith(".jpeg") &&
                                !urll.EndsWith(".gif")
                            ) {
                                server.Send(msg.Channel, "Not an image, you dirty liar!");
                                return;
                            }
                        }

                        imguri = Path.Combine(RootDir, server.Dir, server.ImageDir, url.Substring(url.LastIndexOf("/") + 1).Replace(' ', '_').Replace("%20", "_"));

                        if (File.Exists(imguri)) {
                            server.Send(msg.Channel, "Image already exists!");
                            return;
                        }

                        if (GetWebStreamLength(url) > 3L * 1024L * 1024L) {
                            server.Send(msg.Channel, "Image too large!");
                            return;
                        }

                        if (server.IsImageBlacklisted(url)) {
                            server.Send(msg.Channel, "Image blacklisted!");
                            return;
                        }

                        Task.Run(async delegate () {
                            server.Log("bot", $"Downloading {url} to {imguri}");
                            Task<Message> replyT = msg.Channel.SendMessage("Downloading image...");
                            Message reply;

                            try {
                                using (WebClient wc = new WebClient()) {
                                    wc.DownloadFile(url, imguri);
                                }
                            } catch (Exception e) {
                                reply = await replyT;
                                server.Log("bot", "Download failed.");
                                server.Log("internal", msg.Server.Name, msg.Channel.Name, e.ToString());
                                await reply.Edit($"Something went wrong! Consult `{server.Prefix}log internal`");
                                return;
                            }

                            server.RefreshImageCache();
                            server.LastImage = imguri;

                            reply = await replyT;
                            await reply.Edit("Stored your image successfully!");
                        });
                        return;
                    }

                    if (args.Length == 1 && args[0] == "-") {
                        if (server.LastImage == null) {
                            server.Send(msg.Channel, "disbot no remembering image!");
                            return;
                        }

                        File.Delete(server.LastImage);
                        server.RefreshImageCache();
                        server.Send(msg.Channel, $"Image {Path.GetFileName(server.LastImage)} removed!");
                        server.LastImage = null;
                        return;
                    }

                    if (args.Length == 2 && args[0] == "-") {
                        List<string> matches = server.GetImages(args[1]);
                        if (matches.Count == 0) {
                            server.Send(msg.Channel, "Image does not exist!");
                            return;
                        }

                        if (matches.Count > 1) {
                            StringBuilder builder = new StringBuilder();
                            builder.AppendLine("Found following matches:");

                            for (int i = 0; i < matches.Count; i++) {
                                builder.Append(i).Append(": ").AppendLine(Path.GetFileName(matches[i]));
                            }

                            builder.AppendLine();
                            builder.AppendLine($"Use `{server.Prefix}img - {args[1]} NUMBER`, where number is one of the above.");

                            server.Send(msg.Channel, builder.ToString());
                            return;
                        }

                        File.Delete(matches[0]);
                        server.RefreshImageCache();
                        if (server.LastImage == matches[0]) server.LastImage = null;
                        server.Send(msg.Channel, "Image removed!");
                        return;
                    }

                    if (args.Length == 3 && args[0] == "-") {
                        List<string> matches = server.GetImages(args[1]);
                        if (matches.Count == 0) {
                            server.Send(msg.Channel, "Image does not exist!");
                            return;
                        }

                        if (matches.Count == 1) {
                            server.Send(msg.Channel, "Wrong syntax! Only use the number when multiple images exist for a given query!");
                            return;
                        }

                        int id;
                        if (!int.TryParse(args[2], out id)) {
                            server.Send(msg.Channel, $"`{args[2]}` not a valid number!");
                            return;
                        }
                        File.Delete(matches[id]);
                        server.RefreshImageCache();
                        if (server.LastImage == matches[id]) server.LastImage = null;
                        server.Send(msg.Channel, "Image removed!");
                        return;
                    }

                    if (args.Length == 2 && args[0] == "~") {
                        if (server.LastImage == null) {
                            server.Send(msg.Channel, "disbot no remembering image!");
                            return;
                        }

                        // Relative file name copy because extension shouldn't change when moving
                        string toR = Path.ChangeExtension(args[1], Path.GetExtension(server.LastImage));
                        string to = Path.Combine(RootDir, server.Dir, server.ImageDir, toR);
                        if (File.Exists(to)) {
                            server.Send(msg.Channel, "Destination image already existing!");
                            return;
                        }

                        if (server.IsImageBlacklisted(to)) {
                            server.Send(msg.Channel, "Image blacklisted!");
                            File.Delete(server.LastImage);
                            server.RefreshImageCache();
                            return;
                        }

                        File.Move(server.LastImage, to);
                        server.RefreshImageCache();
                        server.LastImage = to;
                        server.Send(msg.Channel, $"Image now known as {toR}!");
                        return;
                    }

                    if (args.Length == 3 && args[0] == "~") {
                        string from = Path.Combine(RootDir, server.Dir, server.ImageDir, args[1]);
                        if (!File.Exists(from)) {
                            List<string> matches = server.GetImages(args[1]);
                            if (matches.Count == 0) {
                                server.Send(msg.Channel, "Image does not exist!");
                                return;
                            }

                            if (matches.Count > 1) {
                                StringBuilder builder = new StringBuilder();
                                builder.AppendLine("Found following matches:");

                                for (int i = 0; i < matches.Count; i++) {
                                    builder.Append(" ").AppendLine(Path.GetFileName(matches[i]));
                                }

                                builder.AppendLine();
                                builder.AppendLine($"Use `{server.Prefix}img ~ FULLNAME {args[2]}`, where FULLNAME is one of the above.");

                                server.Send(msg.Channel, builder.ToString());
                                return;
                            }

                            from = matches[0];
                        }
                        // Relative file name copy because extension shouldn't change when moving
                        string toR = Path.ChangeExtension(args[2], Path.GetExtension(from));
                        string to = Path.Combine(RootDir, server.Dir, server.ImageDir, toR);
                        if (File.Exists(to)) {
                            server.Send(msg.Channel, "Destination image already existing!");
                            return;
                        }

                        if (server.IsImageBlacklisted(to)) {
                            server.Send(msg.Channel, "Image blacklisted!");
                            File.Delete(from);
                            server.RefreshImageCache();
                            return;
                        }

                        File.Move(from, to);
                        server.RefreshImageCache();
                        server.LastImage = to;
                        server.Send(msg.Channel, $"Image now known as {toR}!");
                        return;
                    }

                    if (args.Length == 1 && (args[0] == "list" || args[0] == "all")) {
                        StringBuilder builder = new StringBuilder();
                        builder.AppendLine("Found following images for this server:");

                        for (int i = 0; i < server.Images.Count; i++) {
                            builder.Append(" ").AppendLine(Path.GetFileName(server.Images[i]).Replace('_', ' '));
                        }

                        builder.AppendLine();
                        builder.AppendLine($"Use `{server.Prefix}img` for a random image or `{server.Prefix}img QUERY` to search for a specific image.");

                        if (server.Images.Count > 20) {
                            server.Send(msg.Channel, "The image list is quite big. Sending it to you via PM.");

                            Task.Run(async delegate () {
                                server.Send(await msg.User.CreatePMChannel(), builder.ToString());
                            });
                            return;
                        }
                        server.Send(msg.Channel, builder.ToString());
                        return;
                    }

                    if (args.Length == 1 && args[0] == "blacklist") {
                        StringBuilder builder = new StringBuilder();
                        builder.AppendLine("Following images blacklisted for this server:");

                        for (int i = 0; i < server.ImageBlacklist.Count; i++) {
                            builder.Append(" ").AppendLine(server.ImageBlacklist[i].Replace('_', ' '));
                        }

                        builder.AppendLine();
                        builder.AppendLine($"Use `{server.Prefix}img -- [img]` to blacklist and `{server.Prefix}img ++ [img]` to unblock an image.");

                        server.Send(msg.Channel, builder.ToString());
                        return;
                    }

                    if (args.Length == 1 && (args[0] == "info" || args[0] == "last")) {
                        if (server.LastImage == null) {
                            server.Send(msg.Channel, "No image was used. Or at least, that's what my memory says.");
                            return;
                        }

                        server.Send(msg.Channel, $"The last image used was: {Path.GetFileName(server.LastImage)}");
                        return;
                    }

                    if (args.Length >= 1) {
                        List<string> matches = server.GetImages(msg.Text.Substring(msg.Text.IndexOf(' ')).Trim());
                        if (matches.Count == 0) {
                            server.Send(msg.Channel, "Image does not exist!");
                            return;
                        }
                        imguri = matches[RNG.Next(matches.Count)];

                    } else {
                        if (server.Images.Count == 0) {
                            server.Send(msg.Channel, $"No images found! Try submitting an image by sending an image with `{server.Prefix}img +` or via `{server.Prefix}img +[URL]`.");
                            return;
                        }
                        imguri = server.Images[RNG.Next(server.Images.Count)];
                    }

                    server.LastImage = imguri;
                    server.SendImage(msg.Channel, imguri);
                }
            });

            Commands.Add(new DisBotDCommand() {
                Name = "emoji",
                Info = "Idea stolen from Zatherz. Sorry!",
                Help = "[anything]",
                OnRun = delegate (DisBotDCommand cmd, DisBotServerConfig server, Message msg, DisBotCommandArg[] args) {
                    if (args.Length == 0) {
                        return;
                    }
                    string text = msg.Text.Substring(msg.Text.IndexOf(' ')).Trim();
                    string textLower = text.ToLowerInvariant();

                    StringBuilder emoji = new StringBuilder();
                    for (int i = 0; i < textLower.Length; i++) {
                        char c = textLower[i];

                        if (c == ' ' || c == '\v') emoji.Append("        ");
                        else if ('a' <= c && c <= 'z') emoji.Append($":regional_indicator_{c}: ");
                        else if (c == '0') emoji.Append(":zero: ");
                        else if (c == '1') emoji.Append(":one: ");
                        else if (c == '2') emoji.Append(":two: ");
                        else if (c == '3') emoji.Append(":three: ");
                        else if (c == '4') emoji.Append(":four: ");
                        else if (c == '5') emoji.Append(":five: ");
                        else if (c == '6') emoji.Append(":six: ");
                        else if (c == '7') emoji.Append(":seven: ");
                        else if (c == '8') emoji.Append(":eight: ");
                        else if (c == '9') emoji.Append(":nine: ");
                        else if (c == ',') emoji.Append(":black_small_square: ");
                        else if (c == '.') emoji.Append(":black_circle: ");
                        else if (c == '+') emoji.Append(":heavy_plus_sign: ");
                        else if (c == '-') emoji.Append(":heavy_minus_sign: ");
                        else if (c == '\u2715') emoji.Append(":heavy_multiplication_x: ");
                        else if (c == '\u00f7') emoji.Append(":heavy_division_sign: ");
                        else if (c == '!') emoji.Append(":exclamation: ");
                        else if (c == '?') emoji.Append(":question: ");
                        else if (c == '\'') emoji.Append(":arrow_down_small: ");
                        else if (c == '*') emoji.Append(":asterisk: ");
                        else if (c == '#') emoji.Append(":hash: ");

                        else emoji.Append(text[i]);
                    }

                    server.Send(msg.Channel, emoji.ToString());
                }
            });

            Commands.Add(new DisBotDCommand() {
                Name = "pasta",
                Info = "Stolen from Zatherz, too. Sorry!",
                Help = "",
                OnRun = (cmd_, server, msg, args) => server.Send(msg.Channel,
@"Hey Zath. I'm now quitting Discord.
I want you to know that the Enter the Gungeon server was the first big modding job that I've done, and while I did do some bad, I'm not letting your power whoring stopping me from doing anything I deem fun.The fact that you took the community I helped run, put yourself in charge, and not put me back on staff is the best proof I can think of for the fact that you have always wanted to abuse power much more than I ever have.You could have simply taught me a lesson and put me back on staff, but you didn't.
Like I said, I genuinely enjoyed running the community, and I didn't do the best all the time. I actually wanted to be friends with you. Most of the time, at least, while you were arguing with me about how I ran the server, I actually tried to be at least a little bit understanding on your end. Other times I said some rude things, and I take those mean words back. Anger feeding into anger doesn't get anyone anywhere, and that's exactly what I did. So I apologize for that. But you wouldn't.
See, this is what separates me from you -I'm willing to admit my flaws. I messed up, and I admitted it. You have done wrong in the past (not what we deem wrong, but things that are generally considered wrong across the board. For example, you made fun of autism, a certain member of the community complained about the fact that he had it himself, and you continued to do it), but you won't admit your flaws.It takes guts to admit that someone does something wrong, and that's exactly what I did. Meanwhile, while we aren't arguing and I'm away, you're constantly putting yourself up on a pedestal saying that the coup went "+"\"so well\""+@", that your plan to sabotage us was practically the best thing you've ever done. If your life amounts to bringing down a small group of people just because they did some wrong for a small internet community, then you seriously need to rethink your life.
This account will never be used again, and I genuinely don't care what you do to the community any more.  I don't care what you think about me.I don't even care if you ban me. All I know is that the community is now more toxic for the drama you helped spread (on the server and on the subreddit too) and you singlehandedly ruined it for not wanting to move on. I don't think the future is bright for the community right now, but all I can say is that I hope for the best, even with you leading it. Goodbye."
                    )
            });

            Parsers.Add(new DisBotDParser() {
                Name = "tableunflip",
                Info = "Your friendly tableunflipper bot.",
                OnParse = (parser, server, msg) => msg.Text.Contains("(╯°□°）╯︵ ┻━┻") || msg.Text.StartsWithInvariant("/tableflip"),
                OnRun = (parser, server, msg) => server.Send(msg.Channel, "┬──┬◡ﾉ(° -°ﾉ)")
            });

            Parsers.Add(new DisBotDParser() {
                Name = "hohono",
                Info = "hoho no.",
                OnParse = delegate (DisBotDParser parser, DisBotServerConfig server, Message msg) {
                    string text = msg.Text.ToLowerInvariant();
                    return
                        (text.Contains("haha") && text.Contains("yes")) ||
                        (text.Contains("fun") && text.Contains("central"));
                },
                OnRun = delegate (DisBotDParser parser, DisBotServerConfig server, Message msg) {
                    if (RNG.Next(6) > 1) return;
                    server.Send(msg.Channel, RNG.Next(4) <= 1 ? "hoho **NO.**" : RNG.Next(4) <= 1 ? "haha yes" : "hoho no");
                }
            });

            Parsers.Add(new DisBotDParser() {
                Name = "prefix",
                Info = "Set the disbot prefix (not a command): @disbot prefix [prefix]",
                OnParse = delegate (DisBotDParser parser, DisBotServerConfig server, Message msg) {
                    string[] split = msg.Text.Split(' ');
                    return msg.IsMentioningMe() && split.Length == 3 && split[1] == "prefix";
                },
                OnRun = delegate (DisBotDParser parser, DisBotServerConfig server, Message msg) {
                    if (server.Server == null) {
                        return;
                    }
                    if (!server.IsBotCommander(msg.User, msg)) {
                        return;
                    }
                    string[] split = msg.Text.Split(' ');
                    string prefix = split[2].Trim();
                    server.Prefix = prefix;
                    server.Save();
                    server.Send(msg.Channel, $"Prefix set to `{prefix}`.");
                }
            });

            // TODO
            new DisBotQImgModule().Init();
        }

        public static Task Start() {
            return Client.Connect(File.ReadAllText(Path.Combine(RootDir, TokenFile)).Trim(), TokenType.Bot);
        }

        public static void Run() {
            Client.ExecuteAndWait(
                async () => await Client.Connect(File.ReadAllText(Path.Combine(RootDir, TokenFile)).Trim(), TokenType.Bot)
            );
        }

        public static async void MessageReceived(object sender, MessageEventArgs e) {
            await Task.Run(() => GetServer(e.Server, true).MessageReceived(sender, e));
        }

        private static Dictionary<string, Image> _avatarCache = new Dictionary<string, Image>();
        private readonly static DColor _roundAvatarBG = DColor.FromArgb(unchecked((int) 0xFF36393E));
        public static async Task<Image> GetAvatarRound(User user, int size = 40) {
            Bitmap img = new Bitmap(size, size);
            Rectangle bounds = new Rectangle(0, 0, size, size);

            using (Graphics g = Graphics.FromImage(img)) {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                Image avatar = await GetAvatar(user);
                g.DrawImage(avatar, bounds, 0, 0, avatar.Width, avatar.Height, GraphicsUnit.Pixel);
            }

            using (Bitmap mask = new Bitmap(size, size)) {
                using (Graphics g = Graphics.FromImage(mask))
                using (SolidBrush brush = new SolidBrush(DColor.White))
                using (GraphicsPath ellipse = new GraphicsPath()) {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    ellipse.AddEllipse(0, 0, size - 1, size - 1);
                    g.FillPath(brush, ellipse);
                }

                img.ApplyMask(mask);
            }

            return img;
        }
        public static async Task<Image> GetAvatar(User user) {
            return await Task.Run(delegate () {
                Image img;
                if (!_avatarCache.TryGetValue(user.AvatarId, out img)) {
                    using (WebClient wc = new WebClient())
                    using (Stream s = wc.OpenRead(user.AvatarUrl)) {
                        img = Image.FromStream(s);
                    }
                    _avatarCache[user.AvatarId] = img;
                }
                return img;
            });
        }

        public static void ApplyMask(this Bitmap img, Bitmap mask) {
            Rectangle bounds = new Rectangle(0, 0, img.Width, img.Height);

            if (!IsMono) {
                BitmapData imgData = img.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                BitmapData maskData = mask.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                unsafe
                {
                    byte* imgP = (byte*) imgData.Scan0.ToPointer();
                    byte* maskP = (byte*) maskData.Scan0.ToPointer();
                    imgP += 3; // a
                    maskP += 3; // a
                    for (int i = bounds.Width * bounds.Height; i > 0; i--) {
                        *imgP = *maskP;
                        imgP += 4;
                        maskP += 4;
                    }
                }
                img.UnlockBits(imgData);
                mask.UnlockBits(maskData);

            } else {
                for (int y = bounds.Height - 1; y >= 0; y--) {
                    for (int x = bounds.Width - 1; x >= 0; x--) {
                        byte a = mask.GetPixel(x, y).A;
                        float f = a / 255f;
                        float ff = 1f - f;

                        if (f < 0.007f) {
                            img.SetPixel(x, y, _roundAvatarBG);
                            continue;
                        }

                        DColor cA = img.GetPixel(x, y);
                        DColor cB = _roundAvatarBG;
                        float r = cA.R * f + cB.R * ff;
                        float g = cA.G * f + cB.G * ff;
                        float b = cA.B * f + cB.B * ff;

                        img.SetPixel(x, y, DColor.FromArgb((byte) r, (byte) g, (byte) b));
                    }
                }
            }
        }


        public static string DateString(this DateTime time) {
            if (time == default(DateTime)) time = DateTime.UtcNow;
            return time.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static DisBotServerConfig GetServer(Server server, bool create = false) {
            DisBotServerConfig config;
            ulong id = server?.Id ?? 0UL;
            if (!Servers.TryGetValue(id, out config) && create) {
                Servers[id] = config = new DisBotServerConfig(server);
                config.Init();
            }
            return config;
        }
        public static void RemoveServer(Server server) {
            if (Servers.ContainsKey(server.Id)) {
                Servers.Remove(server.Id);
            }
        }

        public static long GetWebStreamLength(string url) {
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
            request.UserAgent = "disbot";
            request.Method = "HEAD";

            using (HttpWebResponse response = (HttpWebResponse) request.GetResponse()) {
                return response.ContentLength;
            }
        }

        public static bool StartsWithInvariant(this string a, string b) {
            return a.StartsWith(b, StringComparison.InvariantCulture);
        }

        public static Stream ToStream(this Image img, ImageFormat format = null) {
            format = format ?? ImageFormat.Png;
            MemoryStream ms = new MemoryStream();
            img.Save(ms, format);
            ms.Position = 0;
            return ms;
        }

        public static void CheckGDI() {
            // throw new Exception("GDI not supported.");
        }

        private readonly static object[] a_object_0 = new object[0];

        private readonly static MethodInfo m_Message_Clone =
            typeof(Message).GetMethod("Clone", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly static MethodInfo m_Message_set_Text =
            typeof(Message).GetProperty("Text", BindingFlags.Public | BindingFlags.Instance).GetSetMethod(true);
        public static Message Clone(this Message msg, string text = null) {
            Message clone = (Message) m_Message_Clone.Invoke(msg, a_object_0);
            if (text != null) {
                m_Message_set_Text.Invoke(clone, new object[] { text });
            }
            return clone;
        }

    }
}
