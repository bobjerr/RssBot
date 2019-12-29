using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RssBot.Configuration;
using RssBot.Data;

namespace RssBot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    configApp.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
                    configApp.AddJsonFile("appsettings.json", false).AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<AppSettings>(hostContext.Configuration.GetSection(nameof(AppSettings)));
                    services.AddHostedService<Worker>();
                    services.AddSingleton<RssManager>();
                    services.AddDbContext<Context>(options => options.UseSqlServer(hostContext.Configuration.GetConnectionString("DefaultConnection")));
                });
    }
}
