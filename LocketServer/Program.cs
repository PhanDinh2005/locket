using LocketServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 100 * 1024 * 1024;
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyMethod().AllowAnyHeader().SetIsOriginAllowed(origin => true).AllowCredentials()));

var app = builder.Build();

// --- LOGGING MIDDLEWARE (Mới thêm) ---
// Đoạn này giúp hiện log mỗi khi có ai gọi API (Upload ảnh)
app.Use(async (context, next) =>
{
    ServerLogger.LogRequest(context.Request.Method, context.Request.Path);
    await next();
    // Sau khi xử lý xong thì báo mã lỗi (200 là OK, 400 là Lỗi)
    if (context.Response.StatusCode >= 400)
        ServerLogger.LogError($"Response: {context.Response.StatusCode}");
});
// -------------------------------------

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

    ServerLogger.LogInfo($"File uploaded: {fileName} ({file.Length / 1024} KB)"); // Log file upload

    // Thay localhost bằng IP máy bạn (VD: 192.168.1.5) hoặc để nguyên nếu chạy local
    var url = $"http://{GetLocalIpAddress()}:5000/uploads/{fileName}";
    return Results.Ok(new { Url = url });
})
.DisableAntiforgery();

app.MapHub<LocketHub>("/lockethub");

// Lắng nghe mọi IP
app.Run("http://0.0.0.0:5000");

// Helper lấy IP tự động (để in ra cho bạn biết mà nhập vào Client)
// 1. Thêm hàm lấy IP thật (Ưu tiên lấy IP Wifi 192.168.1...)
string GetLocalIpAddress()
{
    var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
    foreach (var ip in host.AddressList)
    {
        // Chỉ lấy IPv4 và ưu tiên dải 192.168.1.xxx (IP của máy thật bạn)
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            && ip.ToString().StartsWith("192.168.1"))
        {
            return ip.ToString();
        }
    }
    // Nếu không tìm thấy thì đành trả về localhost
    return "localhost";
}

// ... (Code cấu hình khác) ...

// QUAN TRỌNG: Phải có dòng này thì người khác mới tải được file từ thư mục wwwroot
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

    // --- KHÚC QUAN TRỌNG NHẤT ---
    string serverIp = GetLocalIpAddress();
    // Tạo link chứa IP thật: http://192.168.1.133:5000/uploads/abc.jpg
    var url = $"http://{serverIp}:5000/uploads/{fileName}";
    // -----------------------------

    ServerLogger.LogInfo($"Uploaded: {url}");
    return Results.Ok(new { Url = url });
});