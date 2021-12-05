using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ThermalControl
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<HPWMIService>();
                    services.AddHostedService<AdaptiveCoolingService>();
                })
            .RunConsoleAsync();
        }

    }
}
