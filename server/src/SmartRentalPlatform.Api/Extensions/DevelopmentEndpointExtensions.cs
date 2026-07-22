using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Api.Extensions;

public static class DevelopmentEndpointExtensions
{
    public static WebApplication MapDevelopmentEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapPost("/dev/email/test-otp", async (
            TestEmailOtpRequest request,
            IEmailSender emailSender,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = "Email is required."
                });
            }

            var otp = Random.Shared.Next(0, 1_000_000).ToString("D6");
            var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? "Tester"
                : request.DisplayName.Trim();

            await emailSender.SendEmailVerificationOtpAsync(
                request.Email.Trim(),
                displayName,
                otp,
                cancellationToken);

            return Results.Ok(new
            {
                success = true,
                email = request.Email.Trim(),
                otp,
                message = "Test OTP email sent in Development environment."
            });
        })
        .AllowAnonymous()
        .WithTags("Dev");

        return app;
    }
}

public sealed record TestEmailOtpRequest(string Email, string? DisplayName);
