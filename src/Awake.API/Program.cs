using Awake.API.Extensions;
using Awake.API.Middleware;
using Awake.Application;
using Awake.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Application + Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// 2. API
builder.Services.AddControllers();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddRateLimitingPolicies();
builder.Services.AddCorsPolicies(builder.Configuration);

// 3. Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 4. Serilog
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

var app = builder.Build();

// 5. Middleware pipeline (order matters)
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
