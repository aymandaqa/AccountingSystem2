using AccountingSystem;
using AccountingSystem.Authorization;
using AccountingSystem.Data;
using AccountingSystem.Hubs;
using AccountingSystem.Models;
using AccountingSystem.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Roadfn.Services;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using AccountingSystem.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddDbContext<RoadFnDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RoadConnection")));
builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<MappingProfile>();
});

builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events = new CookieAuthenticationEvents
    {
        OnValidatePrincipal = async context =>
        {
            var sessionId = context.Principal?.FindFirst("SessionId")?.Value;
            var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(userId))
            {
                return;
            }

            var sessionService = context.HttpContext.RequestServices.GetRequiredService<IUserSessionService>();
            if (!await sessionService.IsSessionActiveAsync(userId, sessionId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                return;
            }

            await sessionService.UpdateSessionActivityAsync(sessionId);
        }
    };
});

builder.Services.AddControllersWithViews().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver();

}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;

});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IShipmentService, ShipmentService>();

builder.Services.AddScoped<UserResolverService>();
builder.Services.AddScoped<ICurrencyService, CurrencyService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IJournalEntryService, JournalEntryService>();
builder.Services.AddScoped<ICompoundJournalService, CompoundJournalService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IWorkflowApprovalViewModelFactory, WorkflowApprovalViewModelFactory>();
builder.Services.AddScoped<IPaymentVoucherProcessor, PaymentVoucherProcessor>();
builder.Services.AddScoped<IReceiptVoucherProcessor, ReceiptVoucherProcessor>();
builder.Services.AddScoped<IDisbursementVoucherProcessor, DisbursementVoucherProcessor>();
builder.Services.AddScoped<IAssetExpenseProcessor, AssetExpenseProcessor>();
builder.Services.AddScoped<IAssetCostCenterService, AssetCostCenterService>();
builder.Services.AddScoped<IAssetDepreciationService, AssetDepreciationService>();
builder.Services.AddScoped<IUserSessionService, UserSessionService>();
builder.Services.AddScoped<IAttachmentStorageService, AttachmentStorageService>();
builder.Services.AddSignalR();

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddHostedService<CompoundJournalScheduler>();

var app = builder.Build();
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(builder.Configuration.GetValue<string>("SyncfusionLicenseProvider:Key"));

//// Initialize database
//using (var scope = app.Services.CreateScope())
//{
//    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
//    // Apply pending migrations and create the database if it doesn't exist.
//    // EnsureCreated() bypasses migrations which can lead to missing columns
//    // like ExpenseLimit when the schema evolves.
//    context.Database.Migrate();

//    // Seed initial data
//    await SeedData.InitializeAsync(app.Services);
//}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
