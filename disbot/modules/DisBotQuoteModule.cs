using Discord;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DColor = System.Drawing.Color;
using DRegion = System.Drawing.Region;

namespace DisBot {
    public class DisBotQImgModule : DisBotModule {

        public override string Name {
            get {
                return "DisBot Core Quote";
            }
        }
        public override Version Version {
            get {
                return DisBotCore.Version.Value;
            }
        }
        public override string Info {
            get {
                return "Provides basic quote management.";
            }
        }

        public Font Medium16;
        public Font Medium10;
        public Font Medium12;
        public Font Medium15;

        public Brush BgBrush;
        public Brush BotBgBrush;
        public Brush BotFgBrush;
        public Brush FakeBgBrush;
        public Brush FakeFgBrush;
        public Brush DateBrush;
        public Brush TextBrush;

        public StringFormat StringFormat;

        public List<string> Emojis = new List<string>();
        public List<string> EmojiPaths = new List<string>();
        public List<Image> EmojiCache = new List<Image>();
        public Dictionary<ulong, Image> CustomEmojiCache = new Dictionary<ulong, Image>();

        public Regex EmojiRegex = new Regex(
            @"\ud83d\udc68\u200d\u2764\ufe0f\u200d\ud83d\udc8b\u200d\ud83d\udc68|\ud83d\udc68\u200d\ud83d\udc68\u200d\ud83d\udc66\u200d\ud83d\udc66|\ud83d\udc68\u200d\ud83d\udc68\u200d\ud83d\udc67\u200d\ud83d[\udc66\udc67]|\ud83d\udc68\u200d\ud83d\udc69\u200d\ud83d\udc66\u200d\ud83d\udc66|\ud83d\udc68\u200d\ud83d\udc69\u200d\ud83d\udc67\u200d\ud83d[\udc66\udc67]|\ud83d\udc69\u200d\u2764\ufe0f\u200d\ud83d\udc8b\u200d\ud83d[\udc68\udc69]|\ud83d\udc69\u200d\ud83d\udc69\u200d\ud83d\udc66\u200d\ud83d\udc66|\ud83d\udc69\u200d\ud83d\udc69\u200d\ud83d\udc67\u200d\ud83d[\udc66\udc67]|\ud83d\udc68\u200d\u2764\ufe0f\u200d\ud83d\udc68|\ud83d\udc68\u200d\ud83d\udc68\u200d\ud83d[\udc66\udc67]|\ud83d\udc68\u200d\ud83d\udc69\u200d\ud83d[\udc66\udc67]|\ud83d\udc69\u200d\u2764\ufe0f\u200d\ud83d[\udc68\udc69]|\ud83d\udc69\u200d\ud83d\udc69\u200d\ud83d[\udc66\udc67]|\ud83c\udff3\ufe0f\u200d\ud83c\udf08|\ud83c\udff4\u200d\u2620\ufe0f|\ud83d\udc41\u200d\ud83d\udde8|(?:[\u0023\u002a\u0030-\u0039])\ufe0f?\u20e3|(?:(?:\ud83c\udfcb|\ud83d[\udd75\udd90]|[\u261d\u26f9\u270c\u270d])(?:\ufe0f|(?!\ufe0e))|\ud83c[\udf85\udfc2-\udfc4\udfc7\udfca]|\ud83d[\udc42\udc43\udc46-\udc50\udc66-\udc69\udc6e\udc70-\udc78\udc7c\udc81-\udc83\udc85-\udc87\udcaa\udd7a\udd95\udd96\ude45-\ude47\ude4b-\ude4f\udea3\udeb4-\udeb6\udec0]|\ud83e[\udd18-\udd1e\udd26\udd30\udd33-\udd39\udd3c-\udd3e]|[\u270a\u270b])(?:\ud83c[\udffb-\udfff]|)|\ud83c\udde6\ud83c[\udde8-\uddec\uddee\uddf1\uddf2\uddf4\uddf6-\uddfa\uddfc\uddfd\uddff]|\ud83c\udde7\ud83c[\udde6\udde7\udde9-\uddef\uddf1-\uddf4\uddf6-\uddf9\uddfb\uddfc\uddfe\uddff]|\ud83c\udde8\ud83c[\udde6\udde8\udde9\uddeb-\uddee\uddf0-\uddf5\uddf7\uddfa-\uddff]|\ud83c\udde9\ud83c[\uddea\uddec\uddef\uddf0\uddf2\uddf4\uddff]|\ud83c\uddea\ud83c[\udde6\udde8\uddea\uddec\udded\uddf7-\uddfa]|\ud83c\uddeb\ud83c[\uddee-\uddf0\uddf2\uddf4\uddf7]|\ud83c\uddec\ud83c[\udde6\udde7\udde9-\uddee\uddf1-\uddf3\uddf5-\uddfa\uddfc\uddfe]|\ud83c\udded\ud83c[\uddf0\uddf2\uddf3\uddf7\uddf9\uddfa]|\ud83c\uddee\ud83c[\udde8-\uddea\uddf1-\uddf4\uddf6-\uddf9]|\ud83c\uddef\ud83c[\uddea\uddf2\uddf4\uddf5]|\ud83c\uddf0\ud83c[\uddea\uddec-\uddee\uddf2\uddf3\uddf5\uddf7\uddfc\uddfe\uddff]|\ud83c\uddf1\ud83c[\udde6-\udde8\uddee\uddf0\uddf7-\uddfb\uddfe]|\ud83c\uddf2\ud83c[\udde6\udde8-\udded\uddf0-\uddff]|\ud83c\uddf3\ud83c[\udde6\udde8\uddea-\uddec\uddee\uddf1\uddf4\uddf5\uddf7\uddfa\uddff]|\ud83c\uddf4\ud83c\uddf2|\ud83c\uddf5\ud83c[\udde6\uddea-\udded\uddf0-\uddf3\uddf7-\uddf9\uddfc\uddfe]|\ud83c\uddf6\ud83c\udde6|\ud83c\uddf7\ud83c[\uddea\uddf4\uddf8\uddfa\uddfc]|\ud83c\uddf8\ud83c[\udde6-\uddea\uddec-\uddf4\uddf7-\uddf9\uddfb\uddfd-\uddff]|\ud83c\uddf9\ud83c[\udde6\udde8\udde9\uddeb-\udded\uddef-\uddf4\uddf7\uddf9\uddfb\uddfc\uddff]|\ud83c\uddfa\ud83c[\udde6\uddec\uddf2\uddf8\uddfe\uddff]|\ud83c\uddfb\ud83c[\udde6\udde8\uddea\uddec\uddee\uddf3\uddfa]|\ud83c\uddfc\ud83c[\uddeb\uddf8]|\ud83c\uddfd\ud83c\uddf0|\ud83c\uddfe\ud83c[\uddea\uddf9]|\ud83c\uddff\ud83c[\udde6\uddf2\uddfc]|\ud83c[\udccf\udd8e\udd91-\udd9a\udde6-\uddff\ude01\ude32-\ude36\ude38-\ude3a\ude50\ude51\udf00-\udf20\udf2d-\udf35\udf37-\udf7c\udf7e-\udf84\udf86-\udf93\udfa0-\udfc1\udfc5\udfc6\udfc8\udfc9\udfcf-\udfd3\udfe0-\udff0\udff4\udff8-\udfff]|\ud83d[\udc00-\udc3e\udc40\udc44\udc45\udc51-\udc65\udc6a-\udc6d\udc6f\udc79-\udc7b\udc7d-\udc80\udc84\udc88-\udca9\udcab-\udcfc\udcff-\udd3d\udd4b-\udd4e\udd50-\udd67\udda4\uddfb-\ude44\ude48-\ude4a\ude80-\udea2\udea4-\udeb3\udeb7-\udebf\udec1-\udec5\udecc\uded0-\uded2\udeeb\udeec\udef4-\udef6]|\ud83e[\udd10-\udd17\udd20-\udd25\udd27\udd3a\udd40-\udd45\udd47-\udd4b\udd50-\udd5e\udd80-\udd91\uddc0]|[\u23e9-\u23ec\u23f0\u23f3\u26ce\u2705\u2728\u274c\u274e\u2753-\u2755\u2795-\u2797\u27b0\u27bf\ue50a]|(?:\ud83c[\udc04\udd70\udd71\udd7e\udd7f\ude02\ude1a\ude2f\ude37\udf21\udf24-\udf2c\udf36\udf7d\udf96\udf97\udf99-\udf9b\udf9e\udf9f\udfcc-\udfce\udfd4-\udfdf\udff3\udff5\udff7]|\ud83d[\udc3f\udc41\udcfd\udd49\udd4a\udd6f\udd70\udd73\udd74\udd76-\udd79\udd87\udd8a-\udd8d\udda5\udda8\uddb1\uddb2\uddbc\uddc2-\uddc4\uddd1-\uddd3\udddc-\uddde\udde1\udde3\udde8\uddef\uddf3\uddfa\udecb\udecd-\udecf\udee0-\udee5\udee9\udef0\udef3]|[\u00a9\u00ae\u203c\u2049\u2122\u2139\u2194-\u2199\u21a9\u21aa\u231a\u231b\u2328\u23cf\u23ed-\u23ef\u23f1\u23f2\u23f8-\u23fa\u24c2\u25aa\u25ab\u25b6\u25c0\u25fb-\u25fe\u2600-\u2604\u260e\u2611\u2614\u2615\u2618\u2620\u2622\u2623\u2626\u262a\u262e\u262f\u2638-\u263a\u2648-\u2653\u2660\u2663\u2665\u2666\u2668\u267b\u267f\u2692-\u2694\u2696\u2697\u2699\u269b\u269c\u26a0\u26a1\u26aa\u26ab\u26b0\u26b1\u26bd\u26be\u26c4\u26c5\u26c8\u26cf\u26d1\u26d3\u26d4\u26e9\u26ea\u26f0-\u26f5\u26f7\u26f8\u26fa\u26fd\u2702\u2708\u2709\u270f\u2712\u2714\u2716\u271d\u2721\u2733\u2734\u2744\u2747\u2757\u2763\u2764\u27a1\u2934\u2935\u2b05-\u2b07\u2b1b\u2b1c\u2b50\u2b55\u3030\u303d\u3297\u3299])(?:\ufe0f|(?!\ufe0e))"
            ,
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ECMAScript
        );
        public Regex CustomEmojiRegex = new Regex(
            @"<:\w*:(\d*)>"
            ,
            RegexOptions.Compiled | RegexOptions.Multiline
        );
        public string VariationSelector16 = new string(new char[] { (char) 0xFE0F });

        public string EmojiSubstitute = "    ";

        public override void Init() {
            try {
                DisBotCore.CheckGDI();

                Medium16 = new Font("Whitney Medium", 13);
                Medium10 = new Font("Whitney Semibold", 8);
                Medium12 = new Font("Whitney Medium", 10);
                Medium15 = new Font("Whitney Medium", 12);

                BgBrush = new SolidBrush(DColor.FromArgb(unchecked((int) 0xFF36393E)));
                BotBgBrush = new SolidBrush(DColor.FromArgb(114, 137, 218));
                BotFgBrush = new SolidBrush(DColor.White);
                FakeBgBrush = new SolidBrush(DColor.FromArgb(218, 137, 114));
                FakeFgBrush = new SolidBrush(DColor.White);
                DateBrush = new SolidBrush(DColor.FromArgb(51, 255, 255, 255));
                TextBrush = new SolidBrush(DColor.FromArgb(179, 255, 255, 255));
            } catch (Exception) { /* GDI unsupported. */ }

            string emojiPath = Path.Combine(DisBotCore.RootDir, "emoji");
            if (Directory.Exists(emojiPath)) {
                string[] emojiPaths = Directory.GetFiles(emojiPath);
                EmojiPaths.AddRange(emojiPaths);

                StringBuilder emojiSequenceBuilder = new StringBuilder();
                for (int i = 0; i < emojiPaths.Length; i++) {
                    EmojiCache.Add(null);

                    if (emojiPaths[i].StartsWithInvariant("_")) {
                        Emojis.Add(":" + emojiPaths[i].Substring(1) + ":");
                        continue;
                    }

                    string[] split = Path.GetFileNameWithoutExtension(emojiPaths[i]).Split('-');
                    for (int ci = 0; ci < split.Length; ci++) {
                        emojiSequenceBuilder.Append(char.ConvertFromUtf32(Convert.ToInt32(split[ci], 16)));
                    }
                    Emojis.Add(emojiSequenceBuilder.ToString());
                    emojiSequenceBuilder.Clear();
                }
            }

            DisBotCore.Commands.Add(new DisBotDCommand() {
                Name = "qimg",
                Info = "Create a quote image. Does not get stored in the q database.",
                Help = "<how far back> | [user] [text]",
                OnRun = RunQImg
            });

            DisBotCore.Commands.Add(new DisBotDCommand() {
                Name = "q",
                Info = "Quote management command. Uses qimg as backend.",
                Help = "<user> | + <how far back OR user> | + <user> <text> | -",
                OnRun = RunQ
            });
        }

        public async void RunQ(DisBotDCommand cmd, DisBotServerConfig server, Message msg, DisBotCommandArg[] args) {
            if (server.Server == null) {
                return;
            }

            if (DisBotCore.SharedBitmap == null) {
                server.Send(msg.Channel, "Sorry, but the current disbot host doesn't support GDI, required for qimg!");
                return;
            }

            CircularBuffer<Message> buffer = server.GetMessageBuffer(msg.Channel);
            if (buffer == null) {
                server.Send(msg.Channel, "disbot does not remember everything!");
                return;
            }

            Message qmsg = null;
            User user;
            string text;
            bool fake = false;
            string path;

            if (args.Length >= 1 && args[0] == "+") {
                int index = 1;
                if (args.Length >= 2) {
                    text = msg.Text.Substring(msg.Text.IndexOf(' ') + args[0].String.Length + 1).Trim();

                    if (int.TryParse(args[1], out index)) {
                        index = buffer.CurrentSize - index - 1;
                        if (index <= 0 || buffer.CurrentSize <= index) {
                            server.Send(msg.Channel, "disbot does not remember everything!");
                            return;
                        }
                        qmsg = buffer[index];
                        user = qmsg.User;
                        text = qmsg.Text;

                    } else {
                        user = msg.MentionedUsers.FirstOrDefault();
                        if (user == null) {
                            if (text.StartsWith("@")) {
                                text = text.Substring(1);
                            }
                            user = server.Server.FindUsers(text).FirstOrDefault();

                            if (user == null) {
                                server.Send(msg.Channel, "Could not find the requested user. Try mentioning the user instead.");
                                return;
                            }

                            text = string.Empty;

                        } else {
                            string userTag = "@" + (user.Nickname ?? user.Name);
                            if (!text.StartsWithInvariant(userTag)) {
                                server.Send(msg.Channel, "Stop abusing disbot. Ping those you want to create a message for.");
                                return;
                            }
                            text = text.Substring(userTag.Length).Trim();
                            fake = text.Length != 0;
                            if (fake) {
                                qmsg = msg;
                            }
                        }

                        if (!fake) {
                            for (int i = 1; i < buffer.CurrentSize; i++) {
                                Message bmsg = buffer[buffer.CurrentSize - i - 1];
                                if (bmsg.User == user) {
                                    qmsg = bmsg;
                                    user = qmsg.User;
                                    text = qmsg.Text;
                                    break;
                                }
                            }
                        }

                        if (qmsg == null) {
                            server.Send(msg.Channel, "disbot does not remember everything!");
                            return;
                        }
                    }

                } else {
                    if (buffer.CurrentSize <= 1) {
                        server.Send(msg.Channel, "disbot does not remember everything!");
                        return;
                    }

                    qmsg = buffer[buffer.CurrentSize - 2];
                    user = qmsg.User;
                    text = qmsg.Text;
                }

                path = Path.Combine(DisBotCore.RootDir, server.Dir, "qimages");
                path = Path.Combine(path, DisBotCore.PathVerifyRegex.Replace(user.Name, "_"));
                if (fake) {
                    path = Path.Combine(path, "__FAKE__");
                }
                path = Path.Combine(path, DisBotCore.PathVerifyRegex.Replace(text, "_") + ".png");
                Directory.GetParent(path).Create();
                if (File.Exists(path)) {
                    server.Send(msg.Channel, "Quote already stored!");
                    return;
                }
                using (Image img = await CreateQuoteImage(user, qmsg, text, fake)) {
                    using (Stream s = img.ToStream(ImageFormat.Png)) {
                        using (Stream fs = File.OpenWrite(path)) {
                            s.CopyTo(fs);
                        }
                    }
                    server.Data["LastQImg"] = path;
                    server.Send(msg.Channel, "Quote stored!");
                }

                return;
            }

            if (args.Length == 1 && args[0] == "-") {
                object lastqimgO;
                if (!server.Data.TryGetValue("LastQImg", out lastqimgO) || lastqimgO == null) {
                    server.Send(msg.Channel, "disbot no remembering quote!");
                    return;
                }

                File.Delete((string) lastqimgO);
                server.Send(msg.Channel, "Quote removed!");
                server.Data["LastQImg"] = null;
                return;
            }

            path = Path.Combine(DisBotCore.RootDir, server.Dir, "qimages");
            Directory.CreateDirectory(path);
            if (args.Length == 0) {
                string[] users = Directory.GetDirectories(path);
                if (users.Length == 0) {
                    server.Send(msg.Channel, "No quotes found! Like, at all!");
                    return;
                }

                path = users[DisBotCore.RNG.Next(users.Length)];

            } else {
                user = msg.MentionedUsers.FirstOrDefault();
                if (user == null && msg.Text.IndexOf(' ') > 0) {
                    text = msg.Text.Substring(msg.Text.IndexOf(' ')).Trim();

                    if (text.StartsWith("@")) {
                        text = text.Substring(1);
                    }
                    user = server.Server.FindUsers(text).FirstOrDefault();

                    if (user == null) {
                        server.Send(msg.Channel, "Could not find the requested user. Try mentioning the user instead.");
                        return;
                    }
                }

                path = Path.Combine(path, DisBotCore.PathVerifyRegex.Replace(user.Name, "_"));
            }

            if (!Directory.Exists(path)) {
                server.Send(msg.Channel, "No quotes found for that user!");
                return;
            }

            List<string> qimgs = new List<string>();
            qimgs.AddRange(Directory.GetFiles(path));

            path = Path.Combine(path, "__FAKE__");
            if (Directory.Exists(path)) {
                qimgs.AddRange(Directory.GetFiles(path));
            }

            if (qimgs.Count == 0) {
                server.Send(msg.Channel, "No quotes existing for that user!");
                return;
            }

            path = qimgs[DisBotCore.RNG.Next(qimgs.Count)];
            server.Data["LastQImg"] = path;
            server.SendImage(msg.Channel, path);
        }

        public async void RunQImg(DisBotDCommand cmd, DisBotServerConfig server, Message msg, DisBotCommandArg[] args) {
            if (server.Server == null) {
                return;
            }

            if (DisBotCore.SharedBitmap == null) {
                server.Send(msg.Channel, "Sorry, but the current disbot host doesn't support GDI, required for qimg!");
                return;
            }

            CircularBuffer<Message> buffer = server.GetMessageBuffer(msg.Channel);
            if (buffer == null) {
                server.Send(msg.Channel, "disbot does not remember everything!");
                return;
            }

            Message qmsg = msg;
            User user;
            string text;

            bool fake = false;
            if (args.Length <= 1) {
                int index;
                if (args.Length != 1 || !int.TryParse(args[0], out index)) {
                    index = 1;
                }

                index = buffer.CurrentSize - index - 1;
                if (index <= 0 || buffer.CurrentSize <= index) {
                    server.Send(msg.Channel, "disbot does not remember everything!");
                    return;
                }

                qmsg = buffer[index];
                user = qmsg.User;
                text = qmsg.Text;

            } else {
                fake = true;
                text = msg.Text.Substring(msg.Text.IndexOf(' ')).Trim();

                user = msg.MentionedUsers.FirstOrDefault();
                if (user == null) {
                    server.Send(msg.Channel, "Stop abusing disbot. Ping those you want to create a message for.");
                    return;
                }
                string userTag = "@" + (user.Nickname ?? user.Name);
                if (!text.StartsWithInvariant(userTag)) {
                    server.Send(msg.Channel, "Stop abusing disbot. Ping those you want to create a message for.");
                    return;
                }

                text = text.Substring(userTag.Length + 1).Trim();

            }

            using (Image img = await CreateQuoteImage(user, qmsg, text, fake)) {
                using (Stream s = img.ToStream(ImageFormat.Png)) {
                    server.Log("internal", "Uploading qimg");
                    try {
                        await msg.Channel.SendFile("qimg.png", s);
                        server.Log("internal", "Uploaded.");
                    } catch (Exception e) {
                        server.Send(msg.Channel, $"Disbot can't send the quote image! Consult `{server.Prefix}log internal`");
                        server.Log("internal", e.ToString());
                    }
                }
            }
        }

        public async Task<Image> CreateQuoteImage(User user, Message qmsg, string text, bool fake = false) {
            const int maxWidth = 800;

            string nick = user.Nickname ?? user.Name;
            nick = nick.Replace(VariationSelector16, "");
            nick = EmojiRegex.Replace(nick, EmojiSubstitute);

            uint color = 0xFFFFFFFF;
            List<Role> roles = user.Roles.ToList();
            roles.Sort(delegate (Role a, Role b) {
                return b.Position - a.Position;
            });
            for (int i = 0; i < roles.Count; i++) {
                Role role = roles[i];
                if (role.Color != null && role.Color.RawValue != 0) {
                    color = 0xFF000000 | role.Color.RawValue;
                    break;
                }
            }


            Message.File thumbFile =
                (qmsg.Embeds.Length != 0 ? qmsg.Embeds[0].Thumbnail : null) ??
                (qmsg.Attachments.Length != 0 && qmsg.Attachments[0].Width != null ? qmsg.Attachments[0] : null);
            int thumbWidth = 0;
            int thumbHeight = 0;
            if (thumbFile != null) {
                thumbWidth = thumbFile.Width.Value;
                thumbHeight = thumbFile.Height.Value;
                if (thumbWidth > 520) {
                    float ratio = thumbWidth / (float) thumbHeight;
                    thumbWidth = 520;
                    thumbHeight = (int) (thumbWidth / ratio);
                }
            }

            Task<Image> thumbT = Task.Run(delegate () {
                try {
                    if (thumbFile != null) {
                        using (WebClient wc = new WebClient())
                        using (Stream s = wc.OpenRead(thumbFile.Url)) {
                            Image thumb = Image.FromStream(s);
                            if (thumb.Width != thumbWidth) {
                                Image thumbO = thumb;
                                thumb = new Bitmap(thumb, thumbWidth, thumbHeight);
                                thumbO.Dispose();
                            }
                            return thumb;
                        }
                    }
                } catch (Exception) { }
                return null;
            });


            List<Image> emojis = new List<Image>();
            List<CharacterRange> emojiRanges = new List<CharacterRange>();

            StringBuilder textBuilder = new StringBuilder(text);

            MatchCollection twemojiMatches = EmojiRegex.Matches(text);
            MatchCollection cemojiMatches = CustomEmojiRegex.Matches(text);
            IEnumerable<Match> emojiMatches =
                twemojiMatches.OfType<Match>()
                .Concat(cemojiMatches.OfType<Match>())
                .OrderBy(m => m.Index);
            int emojiOffset = 0;
            foreach (Match m in emojiMatches) {
                string emoji = m.Value;
                int emojiID = Emojis.IndexOf(m.Value);
                int index = m.Index - emojiOffset;

                if (emojiID != -1) {
                    emojis.Add(GetEmoji(emojiID));
                } else if (m.Groups.Count > 1) {
                    ulong parsed;
                    if (!ulong.TryParse(m.Groups[1].Value, out parsed)) {
                        continue;
                    }
                    emojis.Add(GetCustomEmoji(parsed));
                } else {
                    continue;
                }
                emojiRanges.Add(new CharacterRange(index, 1));

                emojiOffset += emoji.Length - EmojiSubstitute.Length;

                textBuilder.Remove(index, emoji.Length);
                textBuilder.Insert(index, EmojiSubstitute);
            }

            text = textBuilder.ToString();
            text = text.Replace(VariationSelector16, "");

            SizeF textSize = DisBotCore.SharedGraphics.MeasureString(text, Medium15, maxWidth);
            SizeF textThumbSize = textSize;
            SizeF nickSize = DisBotCore.SharedGraphics.MeasureString(nick, Medium16);

            if (thumbFile != null) {
                textThumbSize = new SizeF(
                    Math.Min(Math.Max(textSize.Width, thumbWidth), maxWidth),
                    textSize.Height + 4 + thumbHeight
                );
            }


            Bitmap img = new Bitmap(
                (int) Math.Max(nickSize.Width + 256 + 46, textThumbSize.Width + 46) + 40,
                (int) textThumbSize.Height + 48
            );
            using (Graphics g = Graphics.FromImage(img)) {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                g.FillRectangle(BgBrush, 0, 0, img.Width, img.Height);

                int x = 8;
                int y = 8;

                using (Image avatar = await DisBotCore.GetAvatarRound(user))
                    g.DrawImage(avatar, x, y);

                x += 40 + 16;

                int nickX = x;
                using (Brush userBrush = new SolidBrush(DColor.FromArgb((int) color)))
                    g.DrawString(nick, Medium16, userBrush, nickX, y);

                int dateX = nickX + (int) g.MeasureString(nick, Medium16).Width + 1 + (DisBotCore.IsMono ? 5 : 0);
                if (user.IsBot) {
                    g.FillRoundRectangle(BotBgBrush, dateX, y + 3 + (DisBotCore.IsMono ? 2 : 0), 25 + (DisBotCore.IsMono ? -1 : 0), 17 + (DisBotCore.IsMono ? -4 : 0), 3);
                    g.DrawString("BOT", Medium10, BotFgBrush, dateX + 1, y + 5 + (DisBotCore.IsMono ? -1 : 0));
                    dateX += 24 + 5;
                }
                if (fake) {
                    g.FillRoundRectangle(FakeBgBrush, dateX, y + 3 + (DisBotCore.IsMono ? 2 : 0), 31 + (DisBotCore.IsMono ? -1 : 0), 17 + (DisBotCore.IsMono ? -4 : 0), 3);
                    g.DrawString("FAKE", Medium10, FakeFgBrush, dateX + 1, y + 5 + (DisBotCore.IsMono ? -1 : 0));
                    dateX += 31 + 5;
                }

                g.DrawString("Somewhen in the past", Medium12, DateBrush, dateX + 1, y + 5 + (DisBotCore.IsMono ? -2 : 0));

                y += 24;

                RectangleF textBounds = new RectangleF(x, y, img.Width - x, img.Height - y);
                g.DrawString(EmojiRegex.Replace(text, EmojiSubstitute), Medium15, TextBrush, textBounds);

                if (emojis.Count != 0) {
                    using (StringFormat format = new StringFormat()) {
                        format.SetMeasurableCharacterRanges(emojiRanges.ToArray());
                        DRegion[] emojiRegions = g.MeasureCharacterRanges(text, Medium15, textBounds, format);

                        for (int i = 0; i < emojis.Count; i++) {
                            Image emoji = emojis[i];
                            Rectangle emojiCharRect = Rectangle.Round(emojiRegions[i].GetBounds(g));

                            g.FillRectangle(BgBrush, x + emojiCharRect.X, y + emojiCharRect.Y, 28, 22);

                            if (emoji == null) continue;
                            g.DrawImage(emoji, x + emojiCharRect.X + 4, y + emojiCharRect.Y, 22, 22);
                        }
                    }
                }

                if (thumbFile != null) {
                    using (Image thumb = await thumbT) {
                        if (thumb != null) {
                            using (TextureBrush thumbBrush = new TextureBrush(thumb)) {
                                y += (int) textSize.Height + 4;
                                thumbBrush.TranslateTransform(x, y);
                                g.FillRoundRectangle(thumbBrush, x, y, thumbWidth - 1, thumbHeight - 1, 3);
                            }
                        }
                    }
                }
            }

            return img;
        }

        public Image GetEmoji(int id) {
            if (id < 0 || EmojiCache.Count <= id) {
                return null;
            }
            Image img = EmojiCache[id];
            if (img == null) {
                img = Image.FromFile(EmojiPaths[id]);
                if (DisBotCore.IsMono) {
                    img = new Bitmap(img);
                    ((Bitmap) img).ApplyMask((Bitmap) img);
                }
                EmojiCache[id] = img;
            }
            return img;
        }

        public Image GetCustomEmoji(ulong id) {
            Image img;
            if (!CustomEmojiCache.TryGetValue(id, out img)) {
                using (WebClient wc = new WebClient())
                using (Stream s = wc.OpenRead("https://cdn.discordapp.com/emojis/" + id + ".png")) {
                    img = Image.FromStream(s);
                }
                if (DisBotCore.IsMono) {
                    img = new Bitmap(img);
                    ((Bitmap) img).ApplyMask((Bitmap) img);
                }
                CustomEmojiCache[id] = img;
            }
            return img;
        }

    }
}
