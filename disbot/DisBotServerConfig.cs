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

        public Server Server;

        public string Prefix = "+";

        public string Dir;
        public string LogFile = "log.txt";
        public string ImageDir = "images";

        public LogBuffer LogBuffer = new LogBuffer();
        protected Dictionary<string, LogBuffer> _taggedLogBuffers = new Dictionary<string, LogBuffer>();

        public List<DisBotCommand> Commands = new List<DisBotCommand>();
        public List<DisBotParser> Parsers = new List<DisBotParser>();

        public List<string> Images = new List<string>();
        public string LastImage;

        public DisBotServerConfig(Server server) {
            Server = server;

            Dir = server != null ? server.Name + "-" + server.Id : "PM";
        }

        public void Init() {
            Directory.CreateDirectory(Path.Combine(DisBotCore.RootDir, Dir));

            RefreshImageCache();

            Commands.AddRange(DisBotCore.Commands);
            Parsers.AddRange(DisBotCore.Parsers);
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
            Log(e.Message.IsAuthor ? "self" : e.Message.User.IsBot ? "bot" : e.Message.Text.StartsWith(Prefix) ? "cmd" : "msg",
                e.Server?.Name ?? "PM", e.Channel.Name, e.User.ToString(), e.Message.Text, time: e.Message.Timestamp);
            if (e.Message.IsAuthor) return;

            foreach (DisBotParser parser in Parsers) await parser.Parse(this, e.Message);

            if (e.Message.User.IsBot) return;

            if (e.Message.Text.StartsWith(Prefix)) {
                await HandleCommand(e.Message);
            }
        }

        public virtual async void Send(Channel channel, string text) {
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
                Log("bot", $"Uploading {imguri}");
                await channel.SendFile(Path.GetFileName(imguri), stream);
                Log("bot", "Uploaded.");
            }
        }

        public virtual async Task HandleCommand(Message msg) {
            string cmdName = msg.Text.Split(' ')[0].Substring(Prefix.Length).Trim().ToLowerInvariant();
            if (cmdName.Length == 0) return;

            DisBotCommand cmd = GetCommand(cmdName);
            if (cmd == null) {
                Send(msg.Channel, "Command not found: \"" + cmdName + "\"");
                return;
            }

            await msg.Channel.SendIsTyping();
            await cmd.Parse(this, msg);
        }

        public virtual DisBotCommand GetCommand(string cmdName) {
            for (int i = 0; i < Commands.Count; i++) {
                DisBotCommand cmd = Commands[i];
                if (cmd.Name == cmdName) {
                    return cmd;
                }
            }
            return null;
        }

        public void Log(string tag, string server, string channel, string message, DateTime time = default(DateTime)) {
            if (message.Contains('\n')) message = "\n" + message;
            Log(tag, $"({time.DateString()})[{tag} @ {server}/{channel}] {message}");
        }
        public void Log(string tag, string server, string channel, string user, string message, DateTime time = default(DateTime)) {
            if (message.Contains('\n')) message = "\n" + message;
            Log(tag, $"({time.DateString()})[{tag} @ {server}/{channel}] {user}: {message}");
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

    }
}
