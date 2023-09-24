using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

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
        private static Image volUpImage = Image.FromFile(@"Images\VolumeHigh.png");
        private static Image volDnImage = Image.FromFile(@"Images\VolumeLow.png");
        private static Image muteImage = Image.FromFile(@"Images\VolumeMute.png");

        public static Image CreateIconImage(Bitmap image)
        {
            Bitmap icon = new Bitmap(image);
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

        public static Image CreateTextImage(string text)
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
                path.AddString(text, fontFamily, (int)FontStyle.Regular, emSize, new Rectangle(0, 0, 144, 144), stringFormat);

                Pen pen = new Pen(Brushes.Black, 10F);
                graph.DrawPath(pen, path);
                graph.FillPath(Brushes.White, path);

                return clone;
            };
        }

        public static Image CreateAppKey(Image iconImage, Image volumeImage, Boolean selected, Boolean muted, Boolean exists = true)
        {
            Bitmap clone = new Bitmap(144, 144, PixelFormat.Format32bppArgb);

            using (Graphics graph = Graphics.FromImage(clone))
            {
                graph.FillRectangle(Brushes.Black, new Rectangle(0, 0, 144, 144));

                // Scale application icon to fit inside selection box, if selected.
                Rectangle rect = selected ? new Rectangle(5, 5, 129, 129) : new Rectangle(0, 0, 144, 144);

                if (exists)
                {
                    graph.DrawImage(iconImage, rect);
                    graph.DrawImage(volumeImage, rect);
                } else
                {
                    graph.DrawImage(GrauwertBild(new Bitmap(iconImage)), rect);
                }
             
                if (exists && muted)
                {
                    Graphics line = Graphics.FromImage(clone);
                    GraphicsPath linePath = new GraphicsPath();
                    linePath.AddLines(new Point[] {
                        new Point(20, 134),
                        new Point(10, 124),
                        new Point(124, 10),
                        new Point(134, 20)
                    });
                    //line.DrawPath(new Pen(Brushes.Black, 15F), linePath);
                    line.FillPath(Brushes.Red, linePath);
                }

                // Apply selection box.
                if (selected)
                {
                    GraphicsPath rectPath = RoundedRect(new Rectangle(5, 5, 134, 134), 15);
                    graph.DrawPath(new Pen(Brushes.White, 5F), rectPath);
                }

                return clone;
            }
        }

        public static Image CreateDefaultAppKey()
        {
            Bitmap clone = new Bitmap(144, 144, PixelFormat.Format32bppArgb);

            Image muteImage = Image.FromFile(@"Images\Default@2x.png");
            using (Graphics graph = Graphics.FromImage(clone))
            {
                graph.DrawImage(muteImage, new Rectangle(0, 0, 144, 144));
            }

            return clone;
        }

        public static Image CreateMuteKey()
        {
            lock(muteImage) { 
                Bitmap clone = new Bitmap(144, 144, PixelFormat.Format32bppArgb);

                using (Graphics graph = Graphics.FromImage(clone))
                {
                    graph.DrawImage(muteImage, new Rectangle(0, 0, 144, 144));
                }

                return clone;
            }
        }

        public static Image CreateVolumeUpKey(float? volumeStep)
        {
            lock(volUpImage)
            {
                Bitmap clone = new Bitmap(144, 144, PixelFormat.Format32bppArgb);

                using (Graphics graph = Graphics.FromImage(clone))
                {
                    var rect = new Rectangle(0, 0, 144, 144);
                    graph.DrawImage(volUpImage, rect);
                    graph.DrawImage(CreateTextImage(volumeStep != null ? $"+{volumeStep}" : ""), rect);
                }

                return clone;
            }
        }

        public static Image CreateVolumeDownKey(float? volumeStep)
        {
            lock(volDnImage)
            {
                Bitmap clone = new Bitmap(144, 144, PixelFormat.Format32bppArgb);

                using (Graphics graph = Graphics.FromImage(clone))
                {
                    var rect = new Rectangle(0, 0, 144, 144);
                    graph.DrawImage(volDnImage, rect);
                    graph.DrawImage(CreateTextImage(volumeStep != null ? $"-{volumeStep}" : ""), rect);
                }

                return clone;
            }
        }

        private static Image ScaleImage(Image image, int maxWidth, int maxHeight)
        {
            var ratioX = (double)maxWidth / image.Width;
            var ratioY = (double)maxHeight / image.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(image.Width * ratio);
            var newHeight = (int)(image.Height * ratio);

            var newImage = new Bitmap(maxWidth, maxHeight);
            using (var graphics = Graphics.FromImage(newImage))
            {
                // Calculate x and y which center the image
                int y = (maxHeight / 2) - newHeight / 2;
                int x = (maxWidth / 2) - newWidth / 2;

                // Draw image on x and y with newWidth and newHeight
                graphics.DrawImage(image, x, y, newWidth, newHeight);
            }

            return newImage;
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            GraphicsPath path = new GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            // top left arc  
            path.AddArc(arc, 180, 90);

            // top right arc  
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // bottom right arc  
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // bottom left arc 
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        // Make a greyscale image
        public static Bitmap GrauwertBild(Bitmap input)
        {
            Bitmap image = new Bitmap(input.Width, input.Height);

            for (int x = 0; x < input.Width; x++)
            {
                for (int y = 0; y < input.Height; y++)
                {
                    Color pixelColor = input.GetPixel(x, y);
                    //  0.3 · r + 0.59 · g + 0.11 · b
                    int grey = (int)(pixelColor.R * 0.3 + pixelColor.G * 0.59 + pixelColor.B * 0.11);
                    image.SetPixel(x, y, Color.FromArgb(pixelColor.A, grey, grey, grey));
                }
            }
            return image;
        }

        public static string BitmapToBase64(Bitmap image)
        {
            // https://stackoverflow.com/questions/12709360/whats-the-difference-between-bitmap-clone-and-new-bitmapbitmap
            /* using (Image newImage = new Bitmap(image))
             {
                 System.IO.MemoryStream ms = new MemoryStream();
                 newImage.Save(ms, ImageFormat.Png);

                 byte[] byteImage = ms.ToArray();
                 var SigBase64 = Convert.ToBase64String(byteImage);

                 return SigBase64;
             }*/

            System.IO.MemoryStream ms = new MemoryStream();
            image.Save(ms, ImageFormat.Png);

            byte[] byteImage = ms.ToArray();
            var SigBase64 = Convert.ToBase64String(byteImage);

            return SigBase64;
        }

        public static Bitmap Base64ToBitmap(string base64String)
        {
            byte[] byteBuffer = Convert.FromBase64String(base64String);

            using (MemoryStream ms = new MemoryStream(byteBuffer))
            {
                return new Bitmap(ms);
            }
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("psapi.dll")]
        static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, [In][MarshalAs(UnmanagedType.U4)] int nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        // https://stackoverflow.com/a/34991822/9005679
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetProcessName(int pid)
        {
            var processHandle = OpenProcess(0x0400 | 0x0010, false, pid);

            if (processHandle == IntPtr.Zero)
            {
                return null;
            }

            const int lengthSb = 4000;

            var sb = new StringBuilder(lengthSb);

            string result = null;

            if (GetModuleFileNameEx(processHandle, IntPtr.Zero, sb, lengthSb) > 0)
            {
                result = Path.GetFullPath(sb.ToString());
            }

            CloseHandle(processHandle);

            return result;
        }

    }
}
