using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using System.Text.RegularExpressions;

namespace Net.Leksi.MicroService.Common;

public class MinioClientAdapter(MinioConfig config) : ICloudClient
{
    private const string s_slash = "/";
    private static readonly Regex manySlashes = new("/{2,}");
    private IMinioClient _minio = null!;
    public void Initialize()
    {
        var builder = new MinioClient()
            .WithEndpoint(config.Endpoint)
            .WithCredentials(config.AccessKey, config.SecretKey);
        _minio = builder.Build();
    }
    public async Task<bool> FileExists(string path, CancellationToken stoppingToken)
    {
        await EnsureBucketExists(stoppingToken);
        var soArgs = new StatObjectArgs()
            .WithBucket(config.Bucket)
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
            .WithBucket(config.Bucket)
            .WithObject(GetAbsolutePath(path))
            .WithObjectSize(size)
            .WithStreamData(stream)
            .WithContentType(mimeType);
        await _minio.PutObjectAsync(putObjectArgs, stoppingToken);
    }
    public void Dispose()
    {
        _minio?.Dispose();
    }
    private string GetAbsolutePath(string path)
    {
        return manySlashes.Replace($"{s_slash}{config.Folder ?? string.Empty}{s_slash}{path}", s_slash);
    }
    private async Task EnsureBucketExists(CancellationToken stoppingToken)
    {
        var beArgs = new BucketExistsArgs()
            .WithBucket(config.Bucket);

        bool found = await _minio.BucketExistsAsync(beArgs, stoppingToken);
        if (!found)
        {
            var mbArgs = new MakeBucketArgs()
                .WithBucket(config.Bucket);
            await _minio.MakeBucketAsync(mbArgs, stoppingToken);
        }
    }

}
