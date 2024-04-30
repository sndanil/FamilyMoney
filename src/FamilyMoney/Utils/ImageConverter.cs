using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyMoney.Utils
{
    public static class ImageConverter
    {
        public static Bitmap? ToImage(Stream? stream)
        {
            if (stream == null) 
                return null;

            return Bitmap.DecodeToWidth(stream, 400);
        }
    }
}
