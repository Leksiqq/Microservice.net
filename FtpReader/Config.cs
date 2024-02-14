using FluentFTP;
using Net.Leksi.MicroService.Common;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService.FtpReader;

public class Config: FileReceiverConfig
{
    public bool? LogClient { get; set; }
    public string? Encoding { get; set; }
    public string? Pattern { get; set; }
    [JsonIgnore]
    public FtpConfig.CustomParser? ListingParser { get; set;}
    public int? ServerTimeZone {  get; set; }
    public bool? FullTimeListing { get; set; }
    public int? SizeChangeTimeout { get; set; }
    [JsonConverter(typeof(ListingSortConverter))]
    public ListingSort? ListingSort { get; set; }
}