using LocketServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 100 * 1024 * 1024;
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyMethod().AllowAnyHeader().SetIsOriginAllowed(origin => true).AllowCredentials()));

var app = builder.Build();

// --- LOGGING MIDDLEWARE  ---

app.Use(async (context, next) =>
{
    ServerLogger.LogRequest(context.Request.Method, context.Request.Path);
    await next();

    if (context.Response.StatusCode >= 400)
        ServerLogger.LogError($"Response: {context.Response.StatusCode}");
});

Directory.CreateDirectory("wwwroot/uploads");
app.UseStaticFiles();
app.UseCors();

app.MapPost("/upload", async (IFormFile file) =>
{
    if (file == null || file.Length == 0)
    {
        ServerLogger.LogWarning("Upload failed: No file received");
        return Results.BadRequest("No file");
    }

    var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
    var path = Path.Combine("wwwroot/uploads", fileName);

    using (var stream = new FileStream(path, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    ServerLogger.LogInfo($"File uploaded: {fileName} ({file.Length / 1024} KB)");

 
    var url = $"http://{GetLocalIpAddress()}:5000/uploads/{fileName}";
    return Results.Ok(new { Url = url });
})
.DisableAntiforgery();

app.MapHub<LocketHub>("/lockethub");

// Lắng nghe mọi IP
app.Run("http://0.0.0.0:5000");

// 1. Thêm hàm lấy IP thật 
string GetLocalIpAddress()
{
    var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
    foreach (var ip in host.AddressList)
    {

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            && ip.ToString().StartsWith("192.168.1"))
        {
            return ip.ToString();
        }
    }

    return "localhost";
}



app.UseStaticFiles();

// 2. Sửa API Upload
app.MapPost("/upload", async (IFormFile file) =>
{
    if (file == null || file.Length == 0) return Results.BadRequest("No file");
    // Lưu file vào ổ cứng
    var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
    var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", fileName);
    // Tạo thư mục nếu chưa có
    Directory.CreateDirectory(Path.GetDirectoryName(savePath));
    using (var stream = new FileStream(savePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }
    string serverIp = GetLocalIpAddress();
    var url = $"http://{serverIp}:5000/uploads/{fileName}";

    ServerLogger.LogInfo($"Uploaded: {url}");
    return Results.Ok(new { Url = url });
});