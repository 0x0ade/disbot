using Discord;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DisBot {
    public class DisBotServerConfig {

        public const string ALIAS_SPLIT = "###;###";
        public readonly static string[] ALIAS_SPLIT_A = new string[] { ALIAS_SPLIT };
        public const string ALIAS_SEPARATE = "###;;###";
        public readonly static string[] ALIAS_SEPARATE_A = new string[] { ALIAS_SEPARATE };
        public const string ALIAS_NEWLINE = "###\n###";

        public Server Server;

        public string Prefix = "+";

        public string Dir;
        public string LogFile = "log.txt";
        public string ConfigFile = "config.txt";
        public string ImageDir = "images";

        public string BotCommander = "Bot Commander";
        public ulong BotOverlordID = 93713629047697408UL;

        public string CommandNotFound = "Command not found: `{0}`";

        public LogBuffer LogBuffer = new LogBuffer();
        protected Dictionary<string, LogBuffer> _taggedLogBuffers = new Dictionary<string, LogBuffer>();

        public List<DisBotCommand> Commands = new List<DisBotCommand>();
        public List<Tuple<string, string>> Aliases = new List<Tuple<string, string>>();
        public List<DisBotParser> Parsers = new List<DisBotParser>();

        public List<string> Images = new List<string>();
        public string LastImage;

        public Dictionary<string, Action<string>> OnLoad = new Dictionary<string, Action<string>>();
        public Dictionary<string, Func<string>> OnSave = new Dictionary<string, Func<string>>();

        public DisBotServerConfig(Server server) {
            Server = server;

            Dir = server != null ? server.Name + "-" + server.Id : "PM";

            OnLoad["id.overlord"] = (s) => BotOverlordID = ulong_Parse(s) ?? BotOverlordID;
            OnSave["id.overlord"] = () => BotOverlordID.ToString();

            OnLoad["role.commander"] = (s) => BotCommander = s;
            OnSave["role.commander"] = () => BotCommander;

            OnLoad["prefix"] = (s) => Prefix = s;
            OnSave["prefix"] = () => Prefix;

            OnLoad["cmd.notfound"] = (s) => CommandNotFound = s;
            OnSave["cmd.notfound"] = () => CommandNotFound;

            OnLoad["cmd.alias"] = (s) => SetAliases(s);
            OnSave["cmd.alias"] = () => GetAliases();


        }

        public void Init() {
            Directory.CreateDirectory(Path.Combine(DisBotCore.RootDir, Dir));

            RefreshImageCache();

            Commands.AddRange(DisBotCore.Commands);
            Parsers.AddRange(DisBotCore.Parsers);

            Load();
        }

        public void Load() {
            string path = Path.Combine(DisBotCore.RootDir, Dir, ConfigFile);
            if (!File.Exists(path)) {
                return;
            }
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];
                line = line.Trim();
                string[] data = line.Split(':');
                if (2 < data.Length) {
                    StringBuilder newData = new StringBuilder();
                    for (int ii = 1; ii < data.Length; ii++) {
                        newData.Append(data[ii]);
                        if (ii < data.Length - 1) {
                            newData.Append(':');
                        }
                    }
                    data = new string[] { data[0], newData.ToString() };
                }
                data[0] = data[0].Trim();
                data[1] = data[1].Trim();

                Action<string> d;
                if (OnLoad.TryGetValue(data[0], out d)) {
                    d(data[1]);
                }
            }
        }
        public void Save() {
            string path = Path.Combine(DisBotCore.RootDir, Dir, ConfigFile);
            if (File.Exists(path)) {
                File.Delete(path);
            }
            using (StreamWriter writer = new StreamWriter(path)) {
                foreach (KeyValuePair<string, Func<string>> nameGetPair in OnSave) {
                    string value = nameGetPair.Value();
                    writer.Write(nameGetPair.Key);
                    writer.Write(":");
                    writer.Write(value);
                    writer.WriteLine();
                }
            }
        }
        protected static ulong? ulong_Parse(string s) {
            ulong result;
            if (!ulong.TryParse(s, out result)) {
                return result;
            }
            return null;
        }
        public void SetAliases(string aliasdata) {
            Aliases.Clear();
            try {
                string[] aliaslines = aliasdata.Split(ALIAS_SEPARATE_A, StringSplitOptions.None);
                for (int i = 0; i < aliaslines.Length; i++) {
                    string aliasline = aliaslines[i];
                    string[] split = aliasline.Split(ALIAS_SPLIT_A, StringSplitOptions.None);
                    string name = split[0].Trim();
                    string cmd = split[1].Trim();
                    Aliases.Add(Tuple.Create(name, cmd));
                }
            } catch (Exception) {
            }
        }
        public string GetAliases() {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Aliases.Count; i++) {
                Tuple<string, string> alias = Aliases[i];
                string name = alias.Item1;
                string cmd = alias.Item2;

                sb.Append(name).Append(ALIAS_SPLIT).Append(cmd.Replace("\n", ALIAS_NEWLINE));

                if (i < Aliases.Count - 1) {
                    sb.Append(ALIAS_SEPARATE);
                }
            }
            return sb.ToString();
        }

        public void RefreshImageCache() {
            string dir = Path.Combine(DisBotCore.RootDir, Dir, ImageDir);
            Directory.CreateDirectory(dir);
            Images.Clear();
            Images.AddRange(Directory.GetFiles(dir));
        }
        public List<string> GetImages(string query) {
            query = query.Replace(' ', '_').ToLowerInvariant();
            return Images.Where((string img) => Path.GetFileName(img).ToLowerInvariant().Contains(query)).ToList();
        }


        public virtual async void MessageReceived(object sender, MessageEventArgs e) {
            Log(e.Message.IsAuthor ? "self" : e.Message.User.IsBot ? "bot" : e.Message.Text.StartsWithInvariant(Prefix) ? "cmd" : "msg",
                e.Channel.Name, e.User.ToString(), e.Message.Text, time: e.Message.Timestamp);
            if (e.Message.IsAuthor) return;

            foreach (DisBotParser parser in Parsers) await parser.Parse(this, e.Message);

            if (e.Message.User.IsBot) return;

            if (e.Message.Text.StartsWithInvariant(Prefix)) {
                await HandleCommand(e.Message);
            }
        }

        public virtual async void Send(Channel channel, string text) {
            text = text.Trim();
            if (text.Length == 0) return;
            if (text.Length > DiscordConfig.MaxMessageSize) {
                int lastnl = text.LastIndexOf("\n", DiscordConfig.MaxMessageSize, DiscordConfig.MaxMessageSize);
                if (lastnl < 0) lastnl = DiscordConfig.MaxMessageSize;
                await channel.SendMessage(text.Substring(0, lastnl));
                Send(channel, text.Substring(lastnl));
                return;
            }
            await channel.SendMessage(text);
        }
        public virtual async void SendImage(Channel channel, string imguri) {
            if (!File.Exists(imguri)) {
                Send(channel, "Image does not exist anymore!");
                return;
            }
            using (FileStream stream = File.OpenRead(imguri)) {
                Log("internal", $"Uploading {imguri}");
                await channel.SendFile(Path.GetFileName(imguri), stream);
                Log("internal", "Uploaded.");
            }
        }

        public virtual async Task HandleCommand(Message msg) {
            string cmdName = msg.Text.Split(' ')[0].Substring(Prefix.Length).Trim().ToLowerInvariant();
            if (cmdName.Length == 0) return;

            await msg.Channel.SendIsTyping();

            DisBotCommand cmd = GetCommand(cmdName);
            if (cmd != null) {
                try {
                    await cmd.Parse(this, msg);
                } catch (Exception e) {
                    Send(msg.Channel, $"Something went horribly wrong! Consult `{Prefix}log internal`");
                    Log("internal", e.ToString());
                }
                return;
            }

            string aliasCmd = GetAlias(cmdName);
            if (aliasCmd != null) {
                try {
                    await HandleCommand(msg.Clone(Prefix + aliasCmd));
                } catch (Exception e) {
                    Send(msg.Channel, $"Something went horribly, horribly wrong! Consult `{Prefix}log internal`");
                    Log("internal", e.ToString());
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(CommandNotFound)) {
                int splitIndex = msg.Text.IndexOf(' ');
                Send(msg.Channel, string.Format(CommandNotFound, cmdName, splitIndex < 0 || msg.Text.Length <= splitIndex ? "" : msg.Text.Substring(msg.Text.IndexOf(' '))));
            }
            return;
        }

        public DisBotCommand GetCommand(string cmdName, bool aliases = true) {
            for (int i = 0; i < Commands.Count; i++) {
                DisBotCommand cmd = Commands[i];
                if (cmd.Name == cmdName) {
                    return cmd;
                }
            }
            if (!aliases) {
                return null;
            }
            return null;
        }

        public string GetAlias(string cmdName) {
            return GetAliasTuple(cmdName)?.Item2;
        }
        public Tuple<string, string> GetAliasTuple(string cmdName) {
            for (int i = 0; i < Aliases.Count; i++) {
                Tuple<string, string> alias = Aliases[i];
                if (alias.Item1 == cmdName) {
                    return alias;
                }
            }
            return null;
        }

        public void Log(string tag, string channel, string message, DateTime time = default(DateTime)) {
            if (message.Contains('\n')) message = "\n" + message;
            Log(tag, $"({time.DateString()})[{tag} @ {Server?.Name ?? "PM"}/{channel}] {message}");
        }
        public void Log(string tag, string channel, string user, string message, DateTime time = default(DateTime)) {
            if (message.Contains('\n')) message = "\n" + message;
            Log(tag, $"({time.DateString()})[{tag} @ {Server?.Name ?? "PM"}/{channel}] {user}: {message}");
        }
        public void Log(string tag, string message, DateTime time = default(DateTime)) {
            if (message.Contains('\n')) message = "\n" + message;
            Log(tag, $"({time.DateString()})[{tag}] {message}");
        }

        private void Log(string tag, string message) {
            GetLogBuffer(tag, true).Add(message);

            Console.WriteLine(message);
            LogBuffer.Add(message);

            string log = Path.Combine(DisBotCore.RootDir, Dir, LogFile);

            if (string.IsNullOrEmpty(log)) return;
            // TODO better.
            try {
                File.AppendAllText(log, message.Replace("\n", DisBotCore.NL));
                File.AppendAllText(log, DisBotCore.NL);
            } catch (IOException) { /* Disk IO. */ }

            log = Path.Combine(DisBotCore.RootDir, DisBotCore.GlobalLogFile);
            if (string.IsNullOrEmpty(log)) return;
            // TODO better.
            try {
                File.AppendAllText(log, message.Replace("\n", DisBotCore.NL));
                File.AppendAllText(log, DisBotCore.NL);
            } catch (IOException) { /* Disk IO. */ }
        }

        public LogBuffer GetLogBuffer(string tag, bool create = false) {
            if (tag == "all") return LogBuffer;

            LogBuffer buffer;
            if (!_taggedLogBuffers.TryGetValue(tag, out buffer) && create) {
                _taggedLogBuffers[tag] = buffer = new LogBuffer();
            }
            return buffer;
        }
        public Dictionary<string, LogBuffer>.KeyCollection LogTags {
            get {
                return _taggedLogBuffers.Keys;
            }
        }

        public virtual bool IsBotCommander(User user, Message msg = null) {
            if (user.Id == BotOverlordID) {
                return true;
            }
            foreach (Role role in user.Roles) {
                if (role.Name == BotCommander || role.Name == "Bot Commander") {
                    return true;
                }
            }
            if (msg != null) {
                Send(msg.Channel, "You're not a bot commander.");
            }
            return false;
        }

    }
}
