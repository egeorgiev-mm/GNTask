using Autofac;
using Autofac.Extensions.DependencyInjection;
using RoomBooking.Api;
using RoomBooking.Api.Endpoints;
using RoomBooking.Api.Middleware;
using RoomBooking.Data;
using RoomBooking.Data.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
	containerBuilder.RegisterModule<RoomBookingModule>();
});

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

var connectionString = builder.Configuration.GetConnectionString("RoomBooking") ?? "Data Source=roombooking.db";
builder.Services.AddRoomBookingData(connectionString);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapRoomEndpoints();
app.MapReservationEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
	var initializer = scope.ServiceProvider.GetRequiredService<RoomBookingDbInitializer>();
	await initializer.InitializeAsync();
}

app.Run();

public partial class Program;
