using Net.Leksi.MicroService.Common;


int number = 3;
CountdownEvent countdown =  new(number);

List<Thread> threads = [];

for (int i = 0; i < number; ++i)
{
    threads.Add(new Thread(() =>
    {
        MinioClientAdapter minio1 = new(new Net.Leksi.MicroService.Common.MinioConfig
        {
            Bucket = "bucket1",
            Folder = "/1/2/3",
            Endpoint = "vm-kafka:9000",
            AccessKey = "microservices_demo",
            SecretKey = "microservices#_demo"
        });

        MemoryStream ms = new(File.ReadAllBytes(@"W:\Learning\Config\config.json"));
        countdown.Signal();
        countdown.Wait();
        minio1.UploadFile(ms, "config.json", "application/json", ms.Length, CancellationToken.None).Wait();
    }));
    threads.Last().Start();
}

foreach (Thread t in threads)
{
    t.Join();
}
