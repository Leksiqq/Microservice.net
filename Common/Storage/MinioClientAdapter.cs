using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using System.Text.RegularExpressions;

namespace Net.Leksi.MicroService.Common;

public class MinioClientAdapter : ICloudClient
{
    private const string s_slash = "/";
#pragma warning disable SYSLIB1045 // Преобразовать в "GeneratedRegexAttribute".
    private static readonly Regex manySlashes = new("/{2,}");
#pragma warning restore SYSLIB1045 // Преобразовать в "GeneratedRegexAttribute".
    private readonly IMinioClient _minio = null!;
    private readonly MinioConfig _config;
    public MinioClientAdapter(MinioConfig config)
    {
        _config = config;
        var builder = new MinioClient()
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
        try
        {
            await _minio.StatObjectAsync(soArgs, stoppingToken);
            return true;
        }
        catch (ErrorResponseException) { }
        return false;
    }
    public async Task UploadFile(Stream stream, string path, string mimeType, long size, CancellationToken stoppingToken)
    {
        await EnsureBucketExists(stoppingToken);
        var putObjectArgs = new PutObjectArgs()
            .WithBucket(_config.Bucket)
            .WithObject(GetAbsolutePath(path))
            .WithObjectSize(size)
            .WithStreamData(stream)
            .WithContentType(mimeType);
        await _minio.PutObjectAsync(putObjectArgs, stoppingToken);
    }
    public void Dispose() 
    { 
        GC.SuppressFinalize(this);
        _minio?.Dispose(); 
    }
    private string GetAbsolutePath(string path)
    {
        return manySlashes.Replace($"{s_slash}{_config.Folder ?? string.Empty}{s_slash}{path}", s_slash);
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
