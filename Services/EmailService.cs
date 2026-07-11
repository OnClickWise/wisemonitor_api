using MailKit.Net.Smtp;
using MimeKit;
using MailKit.Security;

namespace WiseMonitor.Api.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string htmlMessage)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("WiseMonitor", _config["Smtp:From"]));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlMessage };
            email.Body = bodyBuilder.ToMessageBody();

            using var smtp = new SmtpClient();

            // Verifica se os valores existem
            var host = _config["Smtp:Host"] ?? throw new InvalidOperationException("Smtp:Host não configurado.");
            var portString = _config["Smtp:Port"] ?? throw new InvalidOperationException("Smtp:Port não configurado.");
            var user = _config["Smtp:User"] ?? throw new InvalidOperationException("Smtp:User não configurado.");
            var pass = _config["Smtp:Pass"] ?? throw new InvalidOperationException("Smtp:Pass não configurado.");

            // Converte a porta de forma segura
            if (!int.TryParse(portString, out var port))
                throw new InvalidOperationException("Smtp:Port inválido.");

            await smtp.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect);
            await smtp.AuthenticateAsync(user, pass);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);

        }
    }
}
