namespace TIAdmin.Infrastructure.Services;

using System.Net;
using System.Net.Mail;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;

/// <summary>Cola en memoria de correos; la consume <see cref="EmailDispatchWorker"/>.</summary>
public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> channel = Channel.CreateUnbounded<EmailMessage>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<EmailMessage> Reader => channel.Reader;

    public void Enqueue(EmailMessage message) => channel.Writer.TryWrite(message);
}

/// <summary>Email:Enabled=false (desarrollo/pruebas): el correo solo se registra en el log.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        logger.LogInformation("Correo (no enviado, Email:Enabled=false) a {Recipients}: {Subject}",
            string.Join(", ", message.To), message.Subject);
        return Task.CompletedTask;
    }
}

/// <summary>Envio SMTP (Email:SmtpHost/Port/UseSsl, credenciales por user-secrets o variables de entorno).</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;

        using var mail = new MailMessage
        {
            From = new MailAddress(settings.From, settings.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        foreach (var recipient in message.To)
        {
            mail.To.Add(recipient);
        }

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort) { EnableSsl = settings.UseSsl };
        if (!string.IsNullOrWhiteSpace(settings.UserName))
        {
            client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
        }

        await client.SendMailAsync(mail, cancellationToken);
    }
}

/// <summary>Envia los correos encolados; un fallo se registra y no detiene la cola.</summary>
public sealed class EmailDispatchWorker(EmailQueue queue, IEmailSender sender, ILogger<EmailDispatchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await sender.SendAsync(message, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "No se pudo enviar el correo '{Subject}' a {Recipients}.", message.Subject, string.Join(", ", message.To));
            }
        }
    }
}
