using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisBot {
    public class DisBotCommandArg {

        public readonly string String;

        public DisBotCommandArg(string @string) {
            String = @string;
        }

        public override string ToString() {
            return String;
        }

        public static implicit operator string(DisBotCommandArg d) {
            return d.String;
        }

    }

    public abstract class DisBotCommand {

        public abstract string Name { get; set; }
        public abstract string Info { get; set; }
        public abstract string Help { get; set; }

        public virtual async Task Parse(DisBotServerConfig server, Message msg) {
            if (msg.IsAuthor || msg.User.IsBot) return;

            // TODO
            string[] split = msg.Text.Split(' ');
            DisBotCommandArg[] args = new DisBotCommandArg[split.Length - 1];
            for (int i = 1; i < split.Length; i++) {
                args[i - 1] = new DisBotCommandArg(split[i]);
            }

            await Run(server, msg, args);
        }

        public virtual async Task Run(DisBotServerConfig server, Message msg, params DisBotCommandArg[] args) {

        }

    }

    public class DisBotDCommand : DisBotCommand {

        protected string _name;
        public override string Name { get { return _name; } set { _name = _name ?? value; } }
        protected string _info;
        public override string Info { get { return _info; } set { _info = _info ?? value; } }
        protected string _help;
        public override string Help { get { return _help; } set { _help = _help ?? value; } }

        public Action<DisBotDCommand, DisBotServerConfig, Message> OnParse;
        public override async Task Parse(DisBotServerConfig server, Message msg) {
            if (OnParse == null) {
                await base.Parse(server, msg);
                return;
            }
            await Task.Run(() => OnParse(this, server, msg));
        }

        public Action<DisBotDCommand, DisBotServerConfig, Message, DisBotCommandArg[]> OnRun;
        public override async Task Run(DisBotServerConfig server, Message msg, params DisBotCommandArg[] args) {
            await Task.Run(() => OnRun(this, server, msg, args));
        }

    }

    public abstract class DisBotParser : DisBotCommand {

        public override async Task Parse(DisBotServerConfig server, Message msg) {
            if (msg.IsAuthor || msg.User.IsBot) return;
            await Run(server, msg, new DisBotCommandArg(msg.Text));
            return;
        }

    }

    public class DisBotDParser : DisBotParser {

        protected string _name;
        public override string Name { get { return _name; } set { _name = _name ?? value; } }
        protected string _info;
        public override string Info { get { return _info; } set { _info = _info ?? value; } }
        protected string _help;
        public override string Help { get { return _help; } set { _help = _help ?? value; } }

        public Func<DisBotDParser, DisBotServerConfig, Message, bool> OnParse;
        public override async Task Parse(DisBotServerConfig server, Message msg) {
            if (OnParse == null) {
                await base.Parse(server, msg);
            }
            await Task.Run(delegate () {
                if (OnParse(this, server, msg)) OnRun(this, server, msg);
            });
        }

        public Action<DisBotDParser, DisBotServerConfig, Message> OnRun;
        public override async Task Run(DisBotServerConfig server, Message msg, params DisBotCommandArg[] args) {
            await Task.Run(() => OnRun(this, server, msg));
        }

    }
}
