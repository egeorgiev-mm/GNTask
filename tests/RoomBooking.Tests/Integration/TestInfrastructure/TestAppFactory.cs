using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace RoomBooking.Tests.Integration.TestInfrastructure;

public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public TestAppFactory(string sqliteFilePath)
    {
        _connectionString = $"Data Source={sqliteFilePath}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:RoomBooking", _connectionString);

        builder.ConfigureAppConfiguration((context, conf) =>
        {
            var dict = new Dictionary<string, string?>
            {
                ["ConnectionStrings:RoomBooking"] = _connectionString
            };

            conf.AddInMemoryCollection(dict);
        });

        builder.UseEnvironment("Development");
    }
}
