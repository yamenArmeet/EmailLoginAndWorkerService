using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EmailLoginApp.Pages.Emails
{
    public class SendModel : PageModel
    {
        private readonly IEmailSender _emailSender;

        public SendModel(IEmailSender emailSender)
        {
            _emailSender = emailSender;
        }

        [BindProperty]
        public SendInput Input { get; set; } = new();

        public bool Success { get; set; }

        public class SendInput
        {
            [Required, EmailAddress]
            public string To { get; set; } = string.Empty;

            [Required, StringLength(200)]
            public string Subject { get; set; } = string.Empty;

            [Required, StringLength(4000)]
            public string Body { get; set; } = string.Empty;
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            await _emailSender.SendEmailAsync(Input.To, Input.Subject, Input.Body);
            Success = true;
            ModelState.Clear();
            Input = new();

            return Page();
        }
    }
}
