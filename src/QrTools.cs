using System.Drawing;
using QRCoder;

namespace MiyouDaily
{
    public static class QrTools
    {
        public static Bitmap Render(string text)
        {
            using (var generator = new QRCodeGenerator())
            using (var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M))
            using (var code = new QRCode(data)) return code.GetGraphic(6);
        }
        public static void SelfCheck() { using (var bitmap = Render("https://www.miyoushe.com/")) { if (bitmap.Width < 50) throw new System.IO.IOException("二维码组件自检失败。"); } }
    }
}
