using System.Drawing;
using System.Drawing.Imaging;

namespace D4HUD.Extensions
{
    public static class BitmapExtensions
    {
        public static byte[] ToByteArray(this Bitmap image, ImageFormat format)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, format);
                return ms.ToArray();
            }
        }
    }
}
