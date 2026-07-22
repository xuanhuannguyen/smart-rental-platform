namespace SmartRentalPlatform.Api.Extensions;

public static class StaticFileExtensions
{
    public static IApplicationBuilder UsePublicStaticFiles(this IApplicationBuilder app)
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            }
        });

        return app;
    }
}
