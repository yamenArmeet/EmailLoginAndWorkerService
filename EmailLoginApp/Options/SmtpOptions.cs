namespace EmailLoginApp.Options
{
    public class SmtpOptions
    {
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string From { get; set; } = "";
        public string FromName { get; set; } = "EmailLoginApp";
        public bool AssumeDeliveredOnSend { get; set; } = true;
    }
}
