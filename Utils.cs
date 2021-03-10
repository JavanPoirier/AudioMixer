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

namespace AudioMixer
{
    public static class Utils
    {
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
                path.AddString($"{volume * 100}%", fontFamily, (int)FontStyle.Regular, emSize, new Rectangle(0, 0, 144, 144), stringFormat);

                Pen pen = new Pen(Brushes.Black);
                pen.Width = 10F;
                graph.DrawPath(pen, path);
                graph.FillPath(Brushes.White, path);

                return clone;
            };
        }

        //public static Image CreateAppHighlight() { }

        public static Image CreateAppKey(Image iconImage, Image volumeImage) 
        {
            Bitmap clone = new Bitmap(144, 144, PixelFormat.Format32bppArgb);

            using (Graphics graph = Graphics.FromImage(clone))
            {
                graph.DrawImage(iconImage, new Rectangle(0, 0, 144, 144));
                graph.DrawImage(volumeImage, new Rectangle(0, 0, 144, 144));
                return clone;
            }
        }
    }
}
