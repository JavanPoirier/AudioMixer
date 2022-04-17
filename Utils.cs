using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading.Tasks;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using NAudio.CoreAudioApi.Interfaces;
using BarRaider.SdTools;
using System.Drawing.Text;
using System.Runtime.CompilerServices;
using System.IO;

namespace AudioMixer
{
    public static class Utils
    {
        public enum ControlType
        {
            Application,
            VolumeUp,
            VolumeDown,
            Mute
        }

        private static Font font = new Font("Arial", 28F, FontStyle.Regular);
        private static FontFamily fontFamily = new FontFamily("Arial");

        public static Image CreateIconImage(Bitmap icon)
        {
            Bitmap clone = new Bitmap(144, 144, PixelFormat.Format32bppArgb);

            using (Graphics graph = Graphics.FromImage(clone))
            {
                graph.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graph.SmoothingMode = SmoothingMode.HighQuality;
                graph.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graph.FillRectangle(Brushes.Black, new Rectangle(0, 0, 144, 144));

                var pos = (144 - 108) / 2;
                graph.DrawImage(icon, new Rectangle(pos, pos, 108, 108));

                return clone;
            };
        }

        public static Image CreateVolumeImage(float volume)
        {
            Bitmap clone = new Bitmap(144, 144, PixelFormat.Format32bppArgb);

            using (Graphics graph = Graphics.FromImage(clone))
            {
                var stringFormat = new StringFormat();
                stringFormat.Alignment = StringAlignment.Far;
                stringFormat.LineAlignment = StringAlignment.Far;

                graph.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graph.SmoothingMode = SmoothingMode.HighQuality;
                graph.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graph.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                graph.CompositingQuality = CompositingQuality.HighQuality;
                graph.FillRectangle(Brushes.Transparent, new Rectangle(0, 0, 144, 144));

                GraphicsPath path = new GraphicsPath();
                float emSize = graph.DpiY * font.Size / 72;
                path.AddString($"{Math.Round(volume, 2) * 100}%", fontFamily, (int)FontStyle.Regular, emSize, new Rectangle(0, 0, 144, 144), stringFormat);

                Pen pen = new Pen(Brushes.Black, 10F);
                graph.DrawPath(pen, path);
                graph.FillPath(Brushes.White, path);

                return clone;
            };
        }

        //public static Image CreateAppHighlight() { }

        public static Image CreateAppKey(Image iconImage, Image volumeImage, Boolean selected, Boolean muted)
        {
            Bitmap clone = new Bitmap(144, 144, PixelFormat.Format32bppArgb);

            using (Graphics graph = Graphics.FromImage(clone))
            {
                int contentSize = selected ? 120 : 144;
                graph.DrawImage(iconImage, new Rectangle(0, 0, 144, 144));
                graph.DrawImage(volumeImage, new Rectangle(0, 0, 144, 144));

                if (muted)
                {
                    Graphics line = Graphics.FromImage(clone);
                    GraphicsPath linePath = new GraphicsPath();
                    linePath.AddLines(new Point[] {
                    new Point(20, 134),
                    new Point(10, 124),
                    new Point(124, 10),
                    new Point(134, 20)
                });
                    line.DrawPath(new Pen(Brushes.Black, 12F), linePath);
                    line.FillPath(Brushes.Red, linePath);
                }

                if (selected)
                {
                    graph.DrawRectangle(new Pen(Brushes.White, 10F), new Rectangle(0, 0, 144, 144));
                }

                return clone;
            }
        }

        public static Image CreateMuteKey()
        {
            Bitmap clone = new Bitmap(144, 144, PixelFormat.Format32bppArgb);

            Image muteImage = Image.FromFile(@"Images\Mute.png");
            using (Graphics graph = Graphics.FromImage(clone))
            {
                graph.DrawImage(muteImage, new Rectangle(0, 0, 144, 144));
            }

            return clone;
        }

        public static Image CreateVolumeUpKey()
        {
            Bitmap clone = new Bitmap(144, 144, PixelFormat.Format32bppArgb);

            Image muteImage = Image.FromFile(@"Images\VolumeHigh.png");
            using (Graphics graph = Graphics.FromImage(clone))
            {
                graph.DrawImage(muteImage, new Rectangle(0, 0, 144, 144));
            }

            return clone;
        }
        public static Image CreateVolumeDownKey()
        {
            Bitmap clone = new Bitmap(144, 144, PixelFormat.Format32bppArgb);

            Image muteImage = Image.FromFile(@"Images\VolumeLow.png");
            using (Graphics graph = Graphics.FromImage(clone))
            {
                graph.DrawImage(muteImage, new Rectangle(0, 0, 144, 144));
            }

            return clone;
        }
    }
}
