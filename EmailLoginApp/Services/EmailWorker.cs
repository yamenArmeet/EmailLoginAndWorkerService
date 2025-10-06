using System;
using System.IO;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using EmailLoginApp.Data;
using EmailLoginApp.Models;
using EmailLoginApp.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DnsClient;
using System.Collections.Generic;

namespace EmailLoginApp.Services
{
    public class EmailWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailWorker> _logger;
        private readonly SmtpOptions _smtp;
        private readonly string _logDirectoryPath = "Logs";

        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HashSet<string> DisposableDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mailinator.com",
            "tempmail.com",
            "10minutemail.com",
            "guerrillamail.com",
            "yopmail.com"
        };

        public EmailWorker(IServiceScopeFactory scopeFactory,
                           IOptions<SmtpOptions> smtpOptions,
                           ILogger<EmailWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _smtp = smtpOptions.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await LogAsync("Email worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var email = await db.EmailQueue
                        .Where(e => e.Status == EmailStatus.Pending)
                        .OrderBy(e => e.Id)
                        .FirstOrDefaultAsync(stoppingToken);

                    if (email == null)
                    {
                        await LogAsync("No emails to send at this time.");
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        continue;
                    }

                    try
                    {
                        // Step 1 - Validate format
                        if (!IsValidFormat(email.To))
                            throw new Exception("Invalid email format.");

                        // Step 2 - Block disposable domains
                        if (IsDisposableDomain(email.To))
                            throw new Exception("Disposable email domains are not allowed.");

                        // Step 3 - Check MX records
                        if (!await HasMxRecordAsync(email.To))
                            throw new Exception("Domain has no MX records (cannot receive email).");

                        string htmlBody = $@"<div style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; border-radius: 8px;'>{{
                                                <h2 style='color: #4CAF50;'>📧 Email Notification</h2>
                                                <p><strong>To:</strong> {email.To}</p>
                                                <p style='font-size: 16px; color: #333;'>{email.BodyHtml}</p>
                                                <hr />
                                                <p style='font-size: 12px; color: #888;'>This is an automated message.</p>
                                            }}</div>";

                        using var smtp = new SmtpClient(_smtp.Host)
                        {
                            Port = _smtp.Port,
                            EnableSsl = _smtp.EnableSsl,
                            Credentials = new System.Net.NetworkCredential(_smtp.User, _smtp.Password)
                        };

                        using var mail = new MailMessage
                        {
                            From = new MailAddress(_smtp.From, _smtp.FromName),
                            Subject = email.Subject,
                            Body = htmlBody,
                            IsBodyHtml = true
                        };
                        mail.To.Add(email.To);

                        await smtp.SendMailAsync(mail);

                        // 🔹 Delay to avoid SMTP rate limits
                        await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);

                        // Wrap status updates in try/catch
                        try
                        {
                            email.Status = EmailStatus.Sent;
                            email.SentAt = DateTime.UtcNow;
                            await db.SaveChangesAsync(stoppingToken);

                            await LogAsync($"Email {email.Id} SENT to {email.To} at {email.SentAt}");

                            if (_smtp.AssumeDeliveredOnSend)
                            {
                                email.Status = EmailStatus.Delivered;
                                email.DeliveredAt = DateTime.UtcNow;
                                await db.SaveChangesAsync(stoppingToken);

                                await LogAsync($"Email {email.Id} DELIVERED to {email.To} at {email.DeliveredAt}");
                            }
                        }
                        catch (Exception dbEx)
                        {
                            await LogAsync($"Failed to update email status for {email.Id}: {dbEx.Message}", dbEx);
                        }
                    }
                    catch (Exception exSend)
                    {
                        try
                        {
                            email.Status = EmailStatus.Failed;
                            email.FailedAt = DateTime.UtcNow;
                            email.LastError = exSend.Message;
                            await db.SaveChangesAsync(stoppingToken);

                            await LogAsync($"Failed to send email {email.Id} to {email.To}: {exSend.Message}", exSend);

                            // Notify admin
                            await NotifyAdminAsync(email.To, exSend.Message);
                        }
                        catch (Exception dbEx)
                        {
                            await LogAsync($"Failed to update email as failed for {email.Id}: {dbEx.Message}", dbEx);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await LogAsync($"Worker loop error: {ex.Message}", ex);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        private bool IsValidFormat(string email) => EmailRegex.IsMatch(email);

        private bool IsDisposableDomain(string email)
        {
            var domain = email.Split('@').LastOrDefault();
            return domain != null && DisposableDomains.Contains(domain);
        }

        private async Task<bool> HasMxRecordAsync(string email)
        {
            try
            {
                var domain = email.Split('@').LastOrDefault();
                if (string.IsNullOrEmpty(domain))
                    return false;

                var lookup = new LookupClient();
                var result = await lookup.QueryAsync(domain, QueryType.MX);
                return result.Answers.MxRecords().Any();
            }
            catch
            {
                return false;
            }
        }

        private async Task LogAsync(string message, Exception ex = null)
        {
            string timeStampedMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";

            if (ex == null)
                _logger.LogInformation(message);
            else
                _logger.LogError(ex, message);

            try
            {
                Directory.CreateDirectory(_logDirectoryPath);
                string fileName = $"log-{DateTime.Now:yyyy-MM-dd}.txt";
                string fullPath = Path.Combine(_logDirectoryPath, fileName);

                await File.AppendAllTextAsync(fullPath, timeStampedMsg + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception fileEx)
            {
                _logger.LogError(fileEx, "Failed to write to log file");
            }
        }

        private async Task NotifyAdminAsync(string failedEmail, string reason)
        {
            try
            {
                using var smtp = new SmtpClient(_smtp.Host)
                {
                    Port = _smtp.Port,
                    EnableSsl = _smtp.EnableSsl,
                    Credentials = new System.Net.NetworkCredential(_smtp.User, _smtp.Password)
                };

                string body = $@"<div style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; border-radius: 8px;'>
                                    <h2 style='color: #F44336;'>❌ Failed Email Notification</h2>
                                    <p><strong>Failed To:</strong> {failedEmail}</p>
                                    <p><strong>Reason:</strong> {reason}</p>
                                    <p style='font-size: 12px; color: #888;'>This is an automated notification to admin.</p>
                                </div>";

                using var mail = new MailMessage
                {
                    From = new MailAddress(_smtp.From, _smtp.FromName),
                    Subject = "⚠️ Failed Email Notification",
                    Body = body,
                    IsBodyHtml = true
                };
                mail.To.Add(_smtp.From);

                await smtp.SendMailAsync(mail);
                await LogAsync($"Admin notified about failed email: {failedEmail}");
            }
            catch (Exception ex)
            {
                await LogAsync($"Failed to notify admin about email: {failedEmail}", ex);
            }
        }
    }
}
