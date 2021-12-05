using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThermalControl;

public class AdaptiveCoolingService : IHostedService
{
    private readonly ILogger _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly HPWMIService _hpWmiService;

    public AdaptiveCoolingService(
        ILogger<AdaptiveCoolingService> logger,
        IHostApplicationLifetime appLifetime, HPWMIService hpWmiService)
    {
        _logger = logger;
        _appLifetime = appLifetime;
        _hpWmiService = hpWmiService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Starting with arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");

        _appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("Setting power parameters");

                    while (true)
                    {
                        await TunePL1(45);
                        await SetTGPMin();
                        await SetFanMode();
                        await SetFanMax();
                        await PrintIrSensorValue();
                        await Task.Delay(30000);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception!");
                }
                finally
                {
                    // Stop the application once the work is done
                    _appLifetime.StopApplication();
                }
            });
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }


    async Task SetFanMode()
    {
        byte[] inputData = new byte[2] { byte.MaxValue, Convert.ToByte(96) };

        await _hpWmiService.InvokeBiosCommand(131080, 26, 0, inputData);
        _logger.LogInformation("Set Fan Mode: Done");

    }
    async Task SetFanMax()
    {
        byte[] inputData = new byte[2];


        inputData[0] = Convert.ToByte(100);
        inputData[1] = Convert.ToByte(100);
        await _hpWmiService.InvokeBiosCommand(131080, 46, (uint)0, inputData);
        _logger.LogInformation("Set Fan: Done");

    }
    async Task TunePL1(int pl1)
    {
        byte[] inputData = new byte[4];

        inputData[0] = Convert.ToByte(64);
        inputData[1] = Convert.ToByte(pl1);
        await _hpWmiService.InvokeBiosCommand(131080, 41, (uint)0, inputData);
        _logger.LogInformation("Set PL1: Done");

    }
    async Task SetTGPMax()
    {
        byte[] inputData = new byte[4];

        inputData[0] = (byte)1;
        inputData[1] = (byte)0;
        inputData[2] = (byte)1;
        await _hpWmiService.InvokeBiosCommand(131080, 34, (uint)0, inputData);
        _logger.LogInformation("Set TGP: Done");

    }
    async Task SetTGPMin()
    {
        byte[] inputData = new byte[4];
        inputData[0] = (byte)0;
        inputData[1] = (byte)0;
        inputData[2] = (byte)1;
        await _hpWmiService.InvokeBiosCommand(131080, 34, (uint)0, inputData);
        _logger.LogInformation("Set TGP: Done");

    }
    async Task<int> GetIrSensorValue()
    {
        byte[]? inputData = null;
        var result = await _hpWmiService.InvokeBiosCommand(131080, 35, 4, inputData!);
        return result.Data[0];
    }
    async Task PrintIrSensorValue()
    {
        _logger.LogInformation($"IR Temperature: {await GetIrSensorValue()} C");
    }

}
