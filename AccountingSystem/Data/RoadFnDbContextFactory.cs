using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Roadfn.Services;

namespace AccountingSystem.Data
{
    public class RoadFnDbContextFactory : IDesignTimeDbContextFactory<RoadFnDbContext>
    {
        public RoadFnDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("RoadConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = "Server=localhost;Database=RoadDesignTime;User Id=sa;Password=Your_password123;TrustServerCertificate=True;";
            }

            var optionsBuilder = new DbContextOptionsBuilder<RoadFnDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            var userResolver = new UserResolverService(new HttpContextAccessor());
            return new RoadFnDbContext(optionsBuilder.Options, userResolver);
        }
    }
}
