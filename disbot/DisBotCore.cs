using Discord;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DisBot {
    public static class DisBotCore {

        public static string NL;

        public static readonly Lazy<Version> Version = new Lazy<Version>(() => Assembly.GetExecutingAssembly().GetName().Version);
        public static readonly Lazy<string> PublicIP = new Lazy<string>(delegate () {
            using (WebClient wc = new WebClient()) {
                return wc.DownloadString("http://ipinfo.io/ip").Trim();
            }
        });

        private static DiscordClient Client;

        public static string RootDir = "disbot";

        public static string GlobalLogFile = "globallog.txt";
        public static string TokenFile = "token.txt";

        public static Random RNG;

        public static Dictionary<ulong, DisBotServerConfig> Servers = new Dictionary<ulong, DisBotServerConfig>();

        public static List<DisBotCommand> Commands = new List<DisBotCommand>();
        public static List<DisBotParser> Parsers = new List<DisBotParser>();

        public static void Main(string[] args) {
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
                        .Append("Running on *").Append(Environment.OSVersion)
                        .Append("* @ `").Append(Dns.GetHostName())
                        .Append(" (").Append(PublicIP.Value).AppendLine(")`");

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

                        if (GetWebStreamLength(url) > 8L * 1024L * 1024L) {
                            server.Send(msg.Channel, "Image too large!");
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
                Name = "log",
                Info = "Gives you a look at what happened in this server.",
                Help = "<tag (default: all) / list> <range (n, -n, a-b, a+n)>",
                OnRun = delegate (DisBotDCommand cmd, DisBotServerConfig server, Message msg, DisBotCommandArg[] args) {
                    if (server.Server == null) {
                        return;
                    }

                    StringBuilder builder = new StringBuilder();

                    LogBuffer buffer = args.Length == 0 ? server.LogBuffer : server.GetLogBuffer(args[0]);

                    if (buffer == null) {
                        // Try filter and size first
                        // TODO
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
