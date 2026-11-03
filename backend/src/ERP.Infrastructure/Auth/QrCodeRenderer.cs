using ERP.Application.Auth;
using QRCoder;

namespace ERP.Infrastructure.Auth;

/// <summary>PNG QR codes (QRCoder, pure managed code). Used for the authenticator setup link; the text is never logged.</summary>
public sealed class QrCodeRenderer : IQrCodeRenderer
{
    public string PngDataUri(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(8); // ~8 px per module: crisp at 200-260 px, small payload
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
