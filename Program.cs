using ABCRetailers.Services;
using Azure.Data.Tables;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register your existing storage service
builder.Services.AddSingleton<IAzureStorageService, AzureStorageService>();

// Add HTTP client factory for calling Azure Functions
builder.Services.AddHttpClient();

var app = builder.Build();

// Ensure storage resources exist
using (var scope = app.Services.CreateScope())
{
    try
    {
        var storage = scope.ServiceProvider.GetRequiredService<IAzureStorageService>();
        await storage.EnsureInitializedAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to initialize storage resources");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "upload",
    pattern: "upload-proof",
    defaults: new { controller = "Upload", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();