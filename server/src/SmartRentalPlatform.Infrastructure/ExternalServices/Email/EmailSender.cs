using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Infrastructure.ExternalServices.Email;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(
        IConfiguration configuration,
        ILogger<EmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task SendEmailVerificationOtpAsync(
        string email,
        string displayName,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var subject = "Ma OTP xac thuc email Smart Rental Platform";
        var textBody = $"""
            Xin chao {displayName},

            Ma OTP xac thuc email cua ban la: {otp}

            Ma nay chi co hieu luc trong thoi gian ngan. Neu ban khong thuc hien yeu cau nay, vui long bo qua email.

            Smart Rental Platform
            """;
        var htmlBody = BuildOtpHtml(
            displayName,
            otp,
            "Xac thuc email",
            "Dung ma OTP ben duoi de hoan tat dang ky tai khoan.");

        return SendAsync(email, displayName, subject, textBody, htmlBody, cancellationToken);
    }

    public Task SendResetPasswordOtpAsync(
        string email,
        string displayName,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var subject = "Ma OTP dat lai mat khau Smart Rental Platform";
        var textBody = $"""
            Xin chao {displayName},

            Ma OTP dat lai mat khau cua ban la: {otp}

            Neu ban khong yeu cau dat lai mat khau, vui long bo qua email nay.

            Smart Rental Platform
            """;
        var htmlBody = BuildOtpHtml(
            displayName,
            otp,
            "Dat lai mat khau",
            "Dung ma OTP ben duoi de xac nhan yeu cau dat lai mat khau.");

        return SendAsync(email, displayName, subject, textBody, htmlBody, cancellationToken);
    }

    private async Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string textBody,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        var host = _configuration["Email:Smtp:Host"];
        var port = _configuration.GetValue("Email:Smtp:Port", 587);
        var username = _configuration["Email:Smtp:Username"];
        var password = _configuration["Email:Smtp:Password"];
        var useSsl = _configuration.GetValue("Email:Smtp:UseSsl", false);
        var fromEmail = _configuration["Email:FromEmail"];
        var fromName = _configuration["Email:FromName"] ?? "Smart Rental Platform";

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(fromEmail))
        {
            _logger.LogWarning(
                "Email SMTP is not configured. Fallback log email to {Email}. Subject: {Subject}. Body: {Body}",
                toEmail,
                subject,
                textBody);

            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            TextBody = textBody,
            HtmlBody = htmlBody
        }.ToMessageBody();

        using var smtpClient = new SmtpClient();
        var socketOptions = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        await smtpClient.ConnectAsync(host, port, socketOptions, cancellationToken);
        await smtpClient.AuthenticateAsync(username, password, cancellationToken);
        await smtpClient.SendAsync(message, cancellationToken);
        await smtpClient.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Sent email OTP to {Email} with subject {Subject}", toEmail, subject);
    }

    private static string BuildOtpHtml(
        string displayName,
        string otp,
        string title,
        string description)
    {
        return $"""
            <!doctype html>
            <html>
            <body style="font-family: Arial, sans-serif; color: #0f172a; line-height: 1.6;">
                <h2 style="color: #0f766e;">{title}</h2>
                <p>Xin chao {displayName},</p>
                <p>{description}</p>
                <p style="font-size: 28px; font-weight: 700; letter-spacing: 6px; color: #0f172a;">{otp}</p>
                <p>Neu ban khong thuc hien yeu cau nay, vui long bo qua email.</p>
                <p>Smart Rental Platform</p>
            </body>
            </html>
            """;
    }
}

