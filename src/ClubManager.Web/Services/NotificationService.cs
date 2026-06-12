using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ClubManager.Services;

public class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
}

public class NtfySettings
{
    public string BaseUrl { get; set; } = "https://ntfy.sh";
    public string? Auth { get; set; }
}

public class ClubNotificationService
{
    private readonly SmtpSettings _smtp;
    private readonly NtfySettings _ntfy;
    private readonly HttpClient _http;
    private readonly ILogger<ClubNotificationService> _logger;

    public ClubNotificationService(
        IOptions<SmtpSettings> smtp,
        IOptions<NtfySettings> ntfy,
        HttpClient http,
        ILogger<ClubNotificationService> logger)
    {
        _smtp = smtp.Value;
        _ntfy = ntfy.Value;
        _http = http;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_smtp.Host) || string.IsNullOrWhiteSpace(_smtp.User))
        {
            _logger.LogWarning("SMTP not configured, skipping email to {Email}", toEmail);
            return false;
        }

        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("ClubManager", _smtp.From));
            msg.To.Add(new MailboxAddress(toName, toEmail));
            msg.Subject = subject;
            msg.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(_smtp.Host, _smtp.Port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_smtp.User, _smtp.Password);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendNtfyAsync(string topic, string title, string message, string? tags = null)
    {
        if (string.IsNullOrWhiteSpace(_ntfy.BaseUrl))
            return false;

        try
        {
            var url = $"{_ntfy.BaseUrl.TrimEnd('/')}/{topic}";
            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(message)
            };
            req.Headers.Add("Title", title);
            if (!string.IsNullOrWhiteSpace(tags))
                req.Headers.Add("Tags", tags);
            if (!string.IsNullOrWhiteSpace(_ntfy.Auth))
                req.Headers.Add("Authorization", _ntfy.Auth);

            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ntfy to topic {Topic}", topic);
            return false;
        }
    }
}
