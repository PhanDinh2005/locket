using LocketServer;

var builder = WebApplication.CreateBuilder(args);

// Thêm dịch vụ SignalR
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 100 * 1024 * 1024; // Cho phép gửi tin nhắn lớn (100MB)
});

// Thêm CORS để Client gọi được nếu khác máy
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyMethod()
          .AllowAnyHeader()
          .SetIsOriginAllowed(origin => true)
          .AllowCredentials()));

var app = builder.Build();

// Tạo thư mục lưu file nếu chưa có
Directory.CreateDirectory("wwwroot/uploads");
app.UseStaticFiles(); // Để client tải ảnh qua HTTP

app.UseCors();

// --- API Upload file (HTTP) ---
// QUAN TRỌNG: Thêm .DisableAntiforgery() để sửa lỗi System.InvalidOperationException
app.MapPost("/upload", async (IFormFile file) =>
{
    if (file == null || file.Length == 0) return Results.BadRequest("No file");

    var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
    var path = Path.Combine("wwwroot/uploads", fileName);

    using (var stream = new FileStream(path, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    // Trả về URL để Client truy cập
    var url = $"http://localhost:5000/uploads/{fileName}";
    return Results.Ok(new { Url = url });
})
.DisableAntiforgery(); // <--- DÒNG NÀY LÀ CHÌA KHÓA SỬA LỖI CỦA BẠN

// Map Hub WebSocket
app.MapHub<LocketHub>("/lockethub");

// Lắng nghe cổng 5000
app.Run("http://0.0.0.0:5000");