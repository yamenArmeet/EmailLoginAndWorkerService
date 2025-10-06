using System;

namespace EmailLoginApp.Models
{
    public enum EmailStatus { Pending, Sent, Delivered, Read, Failed }

    public class EmailQueue
    {
        public int Id { get; set; }

        public string To { get; set; } = default!;
        public string Subject { get; set; } = default!;
        public string BodyHtml { get; set; } = default!;

        public EmailStatus Status { get; set; } = EmailStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime? FailedAt { get; set; }
        public string? LastError { get; set; }

        // For read tracking pixel
        public string TrackingToken { get; set; } = Guid.NewGuid().ToString("N");

        // If you later integrate a provider (SendGrid/Mailgun/SES)
        public string? ProviderMessageId { get; set; }
    }
}
