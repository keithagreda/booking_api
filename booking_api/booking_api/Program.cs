using System.Text.Json.Serialization;
using booking_api.Data;
using booking_api.Endpoints;
using booking_api.Extensions;
using booking_api.Hubs;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddOpenApi();
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddAuth(builder.Configuration);
builder.Services.AddDomainServices(builder.Configuration);
builder.Services
    .AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Centre Court Booking API");
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapGameEndpoints();
app.MapBookingEndpoints();
app.MapAdminPaymentEndpoints();
app.MapOpenPlayEndpoints();
app.MapDisplayEndpoints();
app.MapAdminMatchEndpoints();
app.MapAdminCatalogEndpoints();
app.MapHub<LiveHub>("/hubs/live");

await DataSeeder.SeedAdminAsync(app.Services);

app.Run();
