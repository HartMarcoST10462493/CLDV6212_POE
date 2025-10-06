using ABCRetailers.Services;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register storage service (singleton is fine here)
builder.Services.AddSingleton<IAzureStorageService, AzureStorageService>();
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(builder.Configuration["StorageConnection:blobServiceUri"]!).WithName("StorageConnection");
    clientBuilder.AddQueueServiceClient(builder.Configuration["StorageConnection:queueServiceUri"]!).WithName("StorageConnection");
    clientBuilder.AddTableServiceClient(builder.Configuration["StorageConnection:tableServiceUri"]!).WithName("StorageConnection");
});

var app = builder.Build();

// Ensure storage resources exist (tables/containers/queues/shares)
using (var scope = app.Services.CreateScope())
{
    var storage = scope.ServiceProvider.GetRequiredService<IAzureStorageService>();
    await storage.EnsureInitializedAsync();
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
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
