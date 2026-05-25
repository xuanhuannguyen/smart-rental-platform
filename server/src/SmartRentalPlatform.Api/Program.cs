using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Services;
using SmartRentalPlatform.Application;
using SmartRentalPlatform.Application.Abstractions;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Requests.Kyc;
using SmartRentalPlatform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            bool HasFieldError(string key) =>
                context.ModelState.TryGetValue(key, out var entry) && entry.Errors.Count > 0;

            var code = ErrorCodes.ValidationError;

            if (HasFieldError(nameof(SubmitKycRequest.FrontImage)))
                code = ErrorCodes.FrontImageRequired;
            else if (HasFieldError(nameof(SubmitKycRequest.BackImage)))
                code = ErrorCodes.BackImageRequired;
            else if (HasFieldError(nameof(SubmitKycRequest.SelfieImage)))
                code = ErrorCodes.SelfieRequired;

            return new BadRequestObjectResult(new { success = false, code });
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

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

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("ClientApp");
app.UseAuthorization();
app.MapControllers();

app.Run();
