﻿using Discord;
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
using System.Threading.Tasks;
using DColor = System.Drawing.Color;

namespace DisBot {
    public class DisBotQImgModule {

        public static Font Medium16 = new Font("Whitney Medium", 13);
        public static Font Medium10 = new Font("Whitney Semibold", 8);
        public static Font Medium12 = new Font("Whitney Medium", 10);
        public static Font Medium15 = new Font("Whitney Medium", 12);

        public static Brush BgBrush = new SolidBrush(DColor.FromArgb(unchecked((int) 0xFF36393E)));
        public static Brush BotBgBrush = new SolidBrush(DColor.FromArgb(114, 137, 218));
        public static Brush BotFgBrush = new SolidBrush(DColor.White);
        public static Brush FakeBgBrush = new SolidBrush(DColor.FromArgb(218, 137, 114));
        public static Brush FakeFgBrush = new SolidBrush(DColor.White);
        public static Brush DateBrush = new SolidBrush(DColor.FromArgb(51, 255, 255, 255));
        public static Brush TextBrush = new SolidBrush(DColor.FromArgb(179, 255, 255, 255));

        public void Init() {
            DisBotCore.Commands.Add(new DisBotDCommand() {
                Name = "qimg",
                Info = "Create a quote image. Does not get stored in the q database.",
                Help = "<how far back> | <[user] [text]>",
                OnRun = RunQImg
            });

            DisBotCore.Commands.Add(new DisBotDCommand() {
                Name = "q",
                Info = "Quote management command. Uses qimg as backend.",
                Help = "<how far back> | <[user] [text]>",
                OnRun = RunQImg
            });
        }

        public async void RunQImg(DisBotDCommand cmd, DisBotServerConfig server, Message msg, DisBotCommandArg[] args) {
            Message qmsg = msg;
            User user;
            string text;

            bool fake = false;
            if (args.Length <= 1) {
                int index;
                if (args.Length != 1 || !int.TryParse(args[0], out index)) {
                    index = 1;
                }

                index = server.MessageBuffer.CurrentSize - index - 1;
                if (index <= 0 || server.MessageBuffer.CurrentSize <= index) {
                    server.Send(msg.Channel, "disbot does not remember everything!");
                    return;
                }

                qmsg = server.MessageBuffer[index];
                user = qmsg.User;
                text = qmsg.Text;

            } else {
                fake = true;
                text = msg.Text.Substring(msg.Text.IndexOf(' ')).Trim();

                user = msg.MentionedUsers.FirstOrDefault();
                if (user == null) {
                    server.Send(msg.Channel, $"Stop abusing disbot. Ping those you want to message.");
                    return;
                }
                string userTag = "@" + (user.Nickname ?? user.Name);
                if (!text.StartsWithInvariant(userTag)) {
                    server.Send(msg.Channel, $"Stop abusing disbot. Ping those you want to message.");
                    return;
                }

                text = text.Substring(userTag.Length + 1).Trim();

            }

            using (Image img = await CreateQuoteImage(user, qmsg, text, fake)) {
                using (Stream s = img.ToStream(ImageFormat.Png)) {
                    server.Log("internal", $"Uploading qimg");
                    await msg.Channel.SendFile("qimg.png", s);
                    server.Log("internal", "Uploaded.");
                }
            }
        }

        public static async Task<Image> CreateQuoteImage(User user, Message qmsg, string text, bool fake = false) {
            string nick = user.Nickname ?? user.Name;

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

            SizeF textSize = DisBotCore.SharedGraphics.MeasureString(text, Medium15);
            SizeF nickSize = DisBotCore.SharedGraphics.MeasureString(nick, Medium16);

            Bitmap img = new Bitmap(
                (int) Math.Max(nickSize.Width + 256 + 46, textSize.Width + 46) + 40,
                (int) textSize.Height + 48
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

                int dateX = nickX + (int) g.MeasureString(nick, Medium16).Width + 1;
                if (user.IsBot) {
                    g.FillRoundRectangle(BotBgBrush, dateX, y + 3, 25, 17, 3);
                    g.DrawString("BOT", Medium10, BotFgBrush, dateX + 1, y + 5);
                    dateX += 24 + 5;
                }
                if (fake) {
                    g.FillRoundRectangle(FakeBgBrush, dateX, y + 3, 31, 17, 3);
                    g.DrawString("FAKE", Medium10, FakeFgBrush, dateX + 1, y + 5);
                    dateX += 31 + 5;
                }

                g.DrawString("Somewhen in the past", Medium12, DateBrush, dateX + 1, y + 5);

                y += 24;

                g.DrawString(text, Medium15, TextBrush, x, y);
            }

            return img;
        }

    }
}
