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
        private static Font font = new Font("Arial", 22, FontStyle.Regular);

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
                // graph.DrawString($"{(volume * 100)}%", font, Brushes.White, new RectangleF(54, 108, 90, 36), new StringFormat());
                graph.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                graph.CompositingQuality = CompositingQuality.HighQuality;

                GraphicsPath path = new GraphicsPath();
                path.AddString($"{volume * 100}%", FontFamily.GenericSansSerif, (int)FontStyle.Regular, 16F, new Point(0, 0), new StringFormat());

                graph.DrawPath(Pens.Black, path);

                return clone;
            };
        }

        public static Image CreateAppKey(Image iconImage, Image volumeImage) 
        {
            Bitmap clone = new Bitmap(144, 144, PixelFormat.Format32bppArgb);

            using (Graphics graph = Graphics.FromImage(clone))
            {
                graph.DrawImage(iconImage, new Point(0, 0));
                graph.DrawImage(volumeImage, new Point(0, 0));
                return clone;
            }
        }
    }
}
