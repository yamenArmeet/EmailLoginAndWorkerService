using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmailLoginApp.Data;
using EmailLoginApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace EmailLoginApp.Controllers
{
    [ApiController]
    public class EmailTrackingController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly string _logDirectoryPath = "Logs";

        public EmailTrackingController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET /t/{token}
        [HttpGet("/t/{token}")]
        public async Task<IActionResult> TrackOpen(string token)
        {
            var msg = _db.EmailQueue.FirstOrDefault(e => e.TrackingToken == token);
            if (msg != null && msg.Status != EmailStatus.Read)
            {
                msg.Status = EmailStatus.Read;
                msg.ReadAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                string logMessage = $"Email {msg.Id} READ by {msg.To} at {msg.ReadAt}";
                Console.WriteLine(logMessage); // log to terminal
                await LogToFileAsync(logMessage); // log to file
            }

            // Return 1x1 transparent GIF
            byte[] pixel = new byte[]
            {
                71,73,70,56,57,97,1,0,1,0,128,0,0,255,255,255,
                0,0,0,33,249,4,1,0,0,1,0,44,0,0,0,0,1,0,1,0,
                0,2,2,68,1,0,59
            };
            return File(pixel, "image/gif");
        }

        private async Task LogToFileAsync(string message)
        {
            try
            {
                Directory.CreateDirectory(_logDirectoryPath);
                string fileName = $"log-{DateTime.Now:yyyy-MM-dd}.txt";
                string fullPath = Path.Combine(_logDirectoryPath, fileName);

                string timeStampedMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
                await System.IO.File.AppendAllTextAsync(fullPath, timeStampedMsg + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to write to log file: " + ex.Message);
            }
        }
    }
}
