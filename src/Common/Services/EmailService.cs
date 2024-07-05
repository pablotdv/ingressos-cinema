using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using SendGrid;
using SendGrid.Helpers.Mail;
using QRCoder;

namespace VendaIngressosCinema.Services
{
    public class EmailService
    {
        // private readonly ISendGridClient _client;

        // public EmailService(ISendGridClient client)
        // {
        //     _client = client;
        // }

        public async Task<bool> Enviar(Ingresso ingresso)
        {
            return true;
            // var from_email = new EmailAddress("pablotdvsm@gmail.com", "Pablo Vargas");
            // var subject = "[IngressosCinema] Aqui está seu ingresso";
            // var to_email = new EmailAddress(ingresso.Cliente.Email, ingresso.Cliente.Nome);
            // var plainTextContent = "Aqui está o identificador do seu ingresso: " + ingresso.Id;

            // var qrCodeBase64 = GenerateBarcodeImage(ingresso.Id.ToString());
            // var htmlContent = $@"
            //     <strong>{plainTextContent}</strong>
            //     <br/><br/>
            //     <img src='data:image/png;base64,{qrCodeBase64}' alt='QR Code' />
            // ";

            // var msg = MailHelper.CreateSingleEmail(from_email, to_email, subject, plainTextContent, htmlContent);
            // var response = await _client.SendEmailAsync(msg);            
            // return response.IsSuccessStatusCode;
        }

        private string GenerateBarcodeImage(string data)
        {
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q))
                {
                    using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                    {
                        byte[] qrCodeImage = qrCode.GetGraphic(20);
                        return Convert.ToBase64String(qrCodeImage.ToArray());
                    }
                }
            }
        }
    }
}