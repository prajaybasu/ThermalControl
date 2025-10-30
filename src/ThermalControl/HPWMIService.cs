using Microsoft.Extensions.Logging;
using Microsoft.Management.Infrastructure;
using System;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace ThermalControl
{

    public class HPWMIService : IDisposable
    {
        public record HpBiosDataOut(string OriginalDataType, bool? Active, byte[] Data, string InstanceName,
        uint RwReturnCode, byte[] Sign);

        private bool _initialized = false;
        private ILogger<HPWMIService> _logger;

        private CimSession? _cimSession;
        private CimInstance? _cimInstance;
        private static readonly byte[] Sign = [83, 69, 67, 85];

        public HPWMIService(ILogger<HPWMIService> logger = null!)
        {
            _logger ??= logger;
        }
        public async Task InitializeAsync()
        {
            _cimSession = await CimSession.CreateAsync(null).ToTask();
            _cimInstance = await _cimSession.QueryInstancesAsync(@"root\wmi", "WQL", "SELECT * FROM hpqBIntM").ToTask();
        }
        
        public async Task<HpBiosDataOut> InvokeBiosCommand(uint command, uint commandType, uint size, byte[]? biosData = null)
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }
            biosData ??= Array.Empty<byte>();

            using var dataInClass = await _cimSession!.GetClassAsync(@"root\wmi", "hpqBDataIn").ToTask();
            using var dataIn = new CimInstance(dataInClass);

            var inParams = _cimInstance!.CimClass.CimClassMethods["hpqBIOSInt128"].Parameters;

            dataIn.CimInstanceProperties["Command"].Value = command;
            dataIn.CimInstanceProperties["CommandType"].Value = commandType;
            dataIn.CimInstanceProperties["hpqBData"].Value = biosData;
            dataIn.CimInstanceProperties["Size"].Value = biosData.Length;
            dataIn.CimInstanceProperties["Sign"].Value = Sign;

            CimMethodParametersCollection cimMethodParameters = [CimMethodParameter.Create("InData", dataIn, CimType.Instance, CimFlags.In)];
            using var dataOut = await _cimSession.InvokeMethodAsync(_cimInstance, $"hpqBIOSInt{size}", cimMethodParameters).ToTask();
            using var outData = dataOut.OutParameters["OutData"].Value as CimInstance;

            if (outData == null)
            {
                throw new Exception("BIOS not responding to WMI command");
            }

            switch ((uint)outData.CimInstanceProperties["rwReturnCode"].Value)
            {
                case 0x03:
                    throw new Exception("Command not available");
                case 0x05:
                    throw new Exception("Size is too small");
            }
            using var outDataClass = outData.CimClass;
            var outDataClassName = outDataClass.CimSystemProperties.ClassName;
            bool? outDataActive = (bool?)outData.CimInstanceProperties["Active"].Value;
            string outDataInstanceName = (string)outData.CimInstanceProperties["InstanceName"].Value;
            uint outDataReturnCode = (uint)outData.CimInstanceProperties["rwReturnCode"].Value;
            byte[] outDataSign = (byte[])outData.CimInstanceProperties["Sign"].Value;
            byte[]? outDataReturnData = (byte[])outData.CimInstanceProperties["Data"]?.Value!;

            if (outDataReturnData != null && (outDataReturnData.Length != size))
            {
                _logger.LogError("InvokeBiosCommand" + "BIOS return data length is not expected length");
            }
            return new HpBiosDataOut(outDataClassName, outDataActive,
                                       outDataReturnData!, outDataInstanceName,
                                       outDataReturnCode, outDataSign);

        }

        public void Dispose()
        {
            _cimInstance?.Dispose();
            _cimSession?.Dispose();
        }
    }
}