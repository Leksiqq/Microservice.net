namespace Net.Leksi.MicroService.Common;

public class TemplateWorkerConfig
{
    public int PollPeriod {  get; set; }
    public string? VarPath { get; set; }
    public TimeSpan InoperativeDurationWarning { get; set; }
    public TimeSpan InoperativeDurationError { get; set; }
}
