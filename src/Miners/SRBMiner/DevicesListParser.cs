using NHM.Common;
using NHM.Common.Device;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SRBMiner
{
    internal class DevicesListParser
    {
        private static string[] _keywords = new string[] { "GPU", "[" };

        private static Func<string, string, int?>[] _keywordsParseNumber = new Func<string, string, int?>[] { NumberAfterPattern, HexNumberAfterPattern };

        private static bool KeepLine(string line) => _keywords.All(word => line?.Contains(word) ?? false);

        const string HexAlphabet = "0123456789abcdef";
        private static bool IsHexChar(char c) => HexAlphabet.Contains(char.ToLower(c));

        private static int? NumberAfterPatternGeneric(string pattern, string line, Func<char, bool> isDigit, NumberStyles numberStyles)
        {
            try
            {
                var index = line?.IndexOf(pattern) ?? -1;
                if (index < 0) return null;

                var numericChars = line
                    .Substring(index + pattern.Length)
                    .SkipWhile(c => !isDigit(c))
                    .TakeWhile(isDigit)
                    .ToArray();
                var numberString = new string(numericChars);
                if (int.TryParse(numberString, numberStyles, CultureInfo.InvariantCulture, out var number)) return number;
            }
            catch
            { }
            return null;
        }

        private static int? HexNumberAfterPattern(string pattern, string line) => NumberAfterPatternGeneric(pattern, line, IsHexChar, NumberStyles.HexNumber);

        private static int? NumberAfterPattern(string pattern, string line) => NumberAfterPatternGeneric(pattern, line, char.IsDigit, NumberStyles.Integer);

        private static int[] LineToGPU_PCIe_Pair(string line)
        {
            return _keywords.Zip(_keywordsParseNumber, (pattern, parseFunction) => (pattern, parseFunction))
                .Select(p => p.parseFunction(p.pattern, line))
                .Where(num => num.HasValue)
                .Select(num => num.Value)
                .ToArray();
        }



        public static IEnumerable<(string uuid, int gpuIndex)> ParseSRBMinerOutput(string output, List<BaseDevice> baseDevices)
        {
            try
            {
                var gpus = baseDevices.Where(dev => dev is AMDDevice).Cast<AMDDevice>();

                var mappedDevices = output.Split(new[] { "\r\n", "\n", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(KeepLine)
                    .Select(LineToGPU_PCIe_Pair)
                    .Where(numPair => numPair.Length == 2)
                    .Select(numPair => (gpuIndex: numPair[0], pcie: numPair[1]))
                    .Select(p => (gpu: gpus.FirstOrDefault(gpu => gpu.PCIeBusID == p.pcie), gpuIndex: p.gpuIndex))
                    .Where(p => p.gpu != null)
                    .Select(p => (uuid: p.gpu.UUID, gpuIndex: p.gpuIndex))
                    .ToArray();

                return mappedDevices;
            }
            catch (Exception ex)
            {
                Logger.Error("SRBMinerPlugin", $"DevicesListParser error: {ex.Message}");
                return Enumerable.Empty<(string uuid, int gpuId)>();
            }
        }
    }
}
