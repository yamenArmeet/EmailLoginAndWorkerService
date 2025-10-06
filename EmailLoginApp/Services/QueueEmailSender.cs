using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EmailLoginApp.Data;
using EmailLoginApp.Models;
using EmailLoginApp.Options;

namespace EmailLoginApp.Services
{
    public class QueueEmailSender : IEmailSender
    {
        private readonly ApplicationDbContext _db;
        private readonly AppOptions _app;
        private readonly ILogger<QueueEmailSender> _logger;

        public QueueEmailSender(ApplicationDbContext db, IOptions<AppOptions> appOptions, ILogger<QueueEmailSender> logger)
        {
            _db = db;
            _app = appOptions.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var token = System.Guid.NewGuid().ToString("N");
            var baseUrl = _app.PublicUrl?.TrimEnd('/') ?? "";
            var pixelUrl = $"{baseUrl}/t/{token}";

            // Add read-tracking pixel
            var bodyWithPixel = htmlMessage + $"<img src=\"{pixelUrl}\" width=\"1\" height=\"1\" style=\"display:none\" />";

            var msg = new EmailQueue
            {
                To = email,
                Subject = subject,
                BodyHtml = bodyWithPixel,
                Status = EmailStatus.Pending,
                TrackingToken = token
            };

            _db.EmailQueue.Add(msg);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Queued email to {Email} (token {Token})", email, token);
        }
    }
}
