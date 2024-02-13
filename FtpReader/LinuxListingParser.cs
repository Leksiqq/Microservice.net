using FluentFTP;
using FluentFTP.Client.BaseClient;
using System.Text.RegularExpressions;

namespace Net.Leksi.MicroService.FtpReader;

internal static class LinuxListingParser
{
    private static readonly Regex _pattern = new("^(<?d>[d-])(<?o>[rwx-])(<?g>[rwx-])(<?a>[rwx-])\\s+\\d+\\s+(<?user>.+?)\\s+(<?group>.+?)\\s+(<?size>\\d+)\\s+(<?month>.{3})\\s+(<?day>\\d+)(?:(<?time>\\d{1,2}\\:\\d{1,2})|(<?year>\\d{1,4}))\\s+(<?name>.+)$");
    internal static FtpListItem Parse(string line, List<FtpCapability> capabilities, BaseFtpClient client)
    {
        Console.WriteLine(line);
        return null;
    }
}
