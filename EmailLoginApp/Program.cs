using EmailLoginApp.Data;
using EmailLoginApp.Options;
using EmailLoginApp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;  // <-- add this

var builder = WebApplication.CreateBuilder(args);

// -------------------------
// Configure Logging
// -------------------------
builder.Logging.ClearProviders();        // remove default providers if you want
builder.Logging.AddConsole();            // add console logging
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
// this line suppresses EF Core SQL logs

// -------------------------
// DB
// -------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=EmailLoginAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// -------------------------
// Identity
// -------------------------
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// -------------------------
// Razor + Controllers
// -------------------------
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// -------------------------
// Options
// -------------------------
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection("App"));

// -------------------------
// Email worker
// -------------------------
builder.Services.AddScoped<IEmailSender, QueueEmailSender>();
builder.Services.AddHostedService<EmailWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
