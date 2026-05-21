using SmartRentalPlatform.Application;
using SmartRentalPlatform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký controller để dùng mô hình API Controller.
builder.Services.AddControllers();

// Đăng ký Swagger để test API trên trình duyệt.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Đăng ký Authorization trước, JWT sẽ thêm sau.
builder.Services.AddAuthorization();

// Cho phép frontend React gọi backend.
// React Vite mặc định chạy ở http://localhost:5173.
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApp", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Đăng ký các layer tự viết.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Chỉ bật Swagger ở môi trường Development.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

// CORS phải đặt trước Authorization.
app.UseCors("ClientApp");

// Sau này thêm JWT thì bật Authentication.
// app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();