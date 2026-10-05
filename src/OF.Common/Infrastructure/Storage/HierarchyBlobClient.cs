using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Newtonsoft.Json;
using System.Text;

namespace OF.Common.Infrastructure.Storage
{
    public class HierarchyBlobClient
    {
        public static string ContainerName = "hierarchy";

        private readonly BlobServiceClient blobServiceClient;

        public HierarchyBlobClient(BlobServiceClient blobServiceClient)
        {
            this.blobServiceClient = blobServiceClient;
        }

        public async Task AddAsync(string key, object instance)
        {
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
            BlobClient blobClient = containerClient.GetBlobClient(key);

            var json = JsonConvert.SerializeObject(instance);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            await blobClient.UploadAsync(new MemoryStream(bytes), overwrite: true);
        }

        public async Task<T> GetAsync<T>(string key)
        {
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
            BlobClient blobClient = containerClient.GetBlobClient(key);

            BlobDownloadInfo blobDownloadInfo = await blobClient.DownloadAsync();
            string blobContent = await new StreamReader(blobDownloadInfo.Content, Encoding.UTF8).ReadToEndAsync();

            return JsonConvert.DeserializeObject<T>(blobContent)!;

        }
    }
}
