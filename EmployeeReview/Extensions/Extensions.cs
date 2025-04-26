using System.Threading.RateLimiting;

namespace EmployeeReview.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(context =>
                {
                    return RateLimitPartition.GetFixedWindowLimiter("GlobalLimiter", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10
                    });
                });

                options.OnRejected = (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = 429; // Too many requests
                    context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", cancellationToken);
                    return ValueTask.CompletedTask;
                };
            });

            return services;
        }
    }
}