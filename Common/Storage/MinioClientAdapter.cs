using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;

namespace Net.Leksi.MicroService.Common;

public class MinioClientAdapter : ICloudClient
{
    private const int s_maxUni = 99999;
    private const string s_locationFormat = "{0}:{1}";
    private const string s_fullNameFormat = "{0}/{1}/{2}{3}{4}";
    private static readonly string s_uniFormat = $".{{0:D{s_maxUni.ToString().Length}}}";
    private readonly IMinioClient _minio = null!;
    private readonly MinioConfig _config;
    private readonly string _folder;
    private readonly Random _rnd = new();
    public string Bucket => _config.Bucket;
    public string Folder => _folder;

    public MinioClientAdapter(MinioConfig config)
    {
        _config = config;
        _folder = Util.CollapseSlashes($"/{_config.Folder ?? string.Empty}");
        IMinioClient builder = new MinioClient()
            .WithEndpoint(_config.Endpoint)
            .WithCredentials(_config.AccessKey, _config.SecretKey);
        _minio = builder.Build();
    }
    public async Task<bool> FileExists(string path, CancellationToken stoppingToken)
    {
        await EnsureBucketExists(stoppingToken);
        var soArgs = new StatObjectArgs()
            .WithBucket(_config.Bucket)
            .WithObject(GetAbsolutePath(path));
            ObjectStat objectStat = await _minio.StatObjectAsync(soArgs, stoppingToken);
        return objectStat.LastModified > DateTime.MinValue;
    }
    public async Task<string> UploadFile(Stream stream, string path, string mimeType, long size, CancellationToken stoppingToken)
    {
        await EnsureBucketExists(stoppingToken);
        long ticks;
        string fullName;
        
        do
        {
            string uni = string.Format(s_uniFormat, _rnd.Next(s_maxUni + 1));
            ticks = DateTime.UtcNow.Ticks;
            fullName = string.Format(
                s_fullNameFormat,
                Path.GetDirectoryName(path), 
                ticks, 
                Path.GetFileNameWithoutExtension(path), 
                uni, 
                Path.GetExtension(path)
            );
        }
        while (await FileExists(fullName, stoppingToken)) ;

        string absPath = GetAbsolutePath(fullName);
        Console.WriteLine($"UploadFile: {path}, {fullName}, {absPath}");
        var putObjectArgs = new PutObjectArgs()
            .WithBucket(_config.Bucket)
            .WithObject(absPath)
            .WithObjectSize(size)
            .WithStreamData(stream)
            .WithContentType(mimeType);
        await _minio.PutObjectAsync(putObjectArgs, stoppingToken);
        return string.Format(s_locationFormat, Bucket, absPath);
    }
    public void Dispose() 
    { 
        GC.SuppressFinalize(this);
        _minio?.Dispose(); 
    }
    private string GetAbsolutePath(string path)
    {
        return Util.CollapseSlashes($"/{_config.Folder ?? string.Empty}/{path}");
    }
    private async Task EnsureBucketExists(CancellationToken stoppingToken)
    {
        var beArgs = new BucketExistsArgs()
            .WithBucket(_config.Bucket);

        bool found = await _minio.BucketExistsAsync(beArgs, stoppingToken);
        if (!found)
        {
            var mbArgs = new MakeBucketArgs()
                .WithBucket(_config.Bucket);
            await _minio.MakeBucketAsync(mbArgs, stoppingToken);
        }
    }

}
