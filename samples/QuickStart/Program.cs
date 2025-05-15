using System;
using System.Threading;
using System.Threading.Tasks;

using FubarDev.FtpServer;
using FubarDev.FtpServer.AccountManagement.Anonymous;
using FubarDev.FtpServer.FileSystem.Redis;

using Microsoft.Extensions.DependencyInjection;

using StackExchange.Redis;

namespace Thisislogic.FTPInMemoryServer
{
    class Program
    {
        static async Task Main()
        {
            // Setup dependency injection
            var services = new ServiceCollection();

            services.AddFtpServer(builder => builder
               .UseRedisFileSystem()
               .EnableAnonymousAuthentication());

            services.AddSingleton<IAnonymousPasswordValidator>(new NoValidation());

            // Configure the FTP server
            services.Configure<FtpServerOptions>(opt => opt.ServerAddress = "*");
            services.Configure<RedisFileSystemOptions>(opt => opt.KeepAnonymousFileSystem = true);

            services.AddSingleton<IConnectionMultiplexer, ConnectionMultiplexer>(x => ConnectionMultiplexer.Connect("redis2.thisislogic.ru:6379,password=123,asyncTimeout=15000"));
            services.AddSingleton<RedisTree>();

            // Build the service provider
            using (var serviceProvider = services.BuildServiceProvider(true))
            {
                // Initialize the FTP server
                var ftpServerHost = serviceProvider.GetRequiredService<IFtpServerHost>();

                // Start the FTP server
                await ftpServerHost.StartAsync(CancellationToken.None).ConfigureAwait(false);

                Console.WriteLine("Press ENTER/RETURN to close the test application.");
                Console.ReadLine();

                // Stop the FTP server
                await ftpServerHost.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
