using System;
using System.Threading;
using System.Threading.Tasks;

using JKang.IpcServiceFramework;
using JKang.IpcServiceFramework.Client;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TestFtpServer.Shell
{
    class Program
    {
        static async Task<int> Main()
        {
            try
            {
                var config = new ConfigurationBuilder()
                   .AddJsonFile("appsettings.json")
                   .AddJsonFile("appsettings.Development.json", true)
                   .Build();

                var tempServiceProvider = new ServiceCollection()
                                         .AddNamedPipeIpcClient<Api.IFtpServerHost>("client1", pipeName: "ftpserver")
                                         .BuildServiceProvider();
                // resolve IPC client factory
                var clientFactory = tempServiceProvider.GetRequiredService<IIpcClientFactory<Api.IFtpServerHost>>();
                var client = clientFactory.CreateClient("client1");
                

                var simpleModuleInfoNames = await client.InvokeAsync(host => host.GetSimpleModules()).ConfigureAwait(false);
                var extendedModuleInfoName = await client.InvokeAsync(host => host.GetExtendedModules()).ConfigureAwait(false);

                var services = new ServiceCollection()
                   .AddLogging(builder => builder.AddConfiguration(config.GetSection("Logging")).AddConsole())
                   .AddSingleton(client)
                   .AddSingleton<IShellStatus>(new ShellStatus(simpleModuleInfoNames, extendedModuleInfoName))
                   .Scan(
                        ts => ts
                           .FromAssemblyOf<FtpShellCommandAutoCompletion>()
                           .AddClasses(itf => itf.AssignableTo<ICommandInfo>(), true).As<ICommandInfo>()
                           .WithSingletonLifetime())
                   .AddSingleton<FtpShellCommandAutoCompletion>()
                   .AddSingleton<ServerShell>();

                var serviceProvider = services.BuildServiceProvider(true);

                var shell = serviceProvider.GetRequiredService<ServerShell>();
                await shell.RunAsync(CancellationToken.None)
                   .ConfigureAwait(false);

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }
    }
}
