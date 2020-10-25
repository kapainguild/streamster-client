using Newtonsoft.Json;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientData;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Services
{
    public class StaticFilesCacheService : IDisposable
    {
        private ConcurrentDictionary<string, Task<byte[]>> _requests = new ConcurrentDictionary<string, Task<byte[]>>();

        private readonly ConnectionService _connectionService;
        private readonly string _root;

        private Task _initCache;
        private ConcurrentDictionary<string, string> _etags;



        public StaticFilesCacheService(ConnectionService connectionService, IAppEnvironment environment)
        {
            // TODO: cache update!!!!!
            _connectionService = connectionService;
            _root = Path.Combine(environment.GetStorageFolder(), "cache");
            _initCache = Task.Run(() => InitCache());
        }

        private void InitCache()
        {
            try
            {
                var cacheFile = Path.Combine(_root, "cache.json");
                if (File.Exists(cacheFile))
                {
                    var file = JsonConvert.DeserializeObject<CacheFile>(File.ReadAllText(cacheFile));
                    _etags = new ConcurrentDictionary<string, string>(file?.Etags);
                }
            }
            catch(Exception e)
            {
                Log.Error(e, "Failed to init cache");
            }

            _etags = _etags ?? new ConcurrentDictionary<string, string>();
        }


        public void Dispose()
        {
            try
            {
                var cacheFile = Path.Combine(_root, "cache.json");
                File.WriteAllText(cacheFile, JsonConvert.SerializeObject(new CacheFile { Etags = _etags.ToDictionary(s => s.Key, s => s.Value) }));
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to store cache");
            }
        }


        public async Task<byte[]> GetFileAsync(string path)
        {
            await _initCache;
            return await _requests.GetOrAdd(path, s => GetFileInternalAsync(s));
        }

        private async Task<byte[]> GetFileInternalAsync(string path)
        {
            try
            {
                string localPath = Path.Combine(_root, path);
                var bytes = await Task.Run(() => GetLocalFile(localPath));
                if (bytes == null)
                {
                    var data = await _connectionService.RunWithRetries(async (url) => await DownloadFileAsync(url, path, null), ClientConstants.LoadBalancerServers, 3);

                    await StoreData(localPath, path, data);
                    return data.Data;
                }
                else
                {
                    TaskHelper.RunUnawaited(UpdateCache(localPath, path), "Refresh cache");
                    return bytes;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, $"Failed to download resource '{path}'");
            }
            return null;
        }

        private async Task UpdateCache(string localPath, string path)
        {
            _etags.TryGetValue(path, out var localEtag);
            var updated = await _connectionService.RunWithRetries(async (url) => await DownloadFileAsync(url, path, localEtag), ClientConstants.LoadBalancerServers, 1);
            if (updated != null)
                await StoreData(localPath, path, updated);
        }

        private Task StoreData(string localPath, string path, CacheData cacheData) => Task.Run(() =>
        {
            if (cacheData.Data != null)
            {
                File.WriteAllBytes(localPath, cacheData.Data);
                if (cacheData.ETag == null)
                    _etags.TryRemove(path, out _);
                else
                    _etags[path] = cacheData.ETag;
            }
        });

        private async Task<CacheData> DownloadFileAsync(string host, string path, string localETag)
        {
            string get = $"https://{host}:{ClientConstants.LoadBalancerServerPort}{ClientConstants.LoadBalancerFilesFolder}/{path}";
            using (HttpClient aClient = _connectionService.GetHttpClient(true))
            {
                var request = new HttpRequestMessage(HttpMethod.Get, get);

                if (localETag != null)
                {
                    request.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = new TimeSpan() };
                    request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(localETag));
                }
                HttpResponseMessage response = await aClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                string etag = response.Headers?.ETag?.Tag;
                
                if (localETag != null && etag != null && localETag == etag || 
                    response.StatusCode == HttpStatusCode.NotModified)
                {
                    return null;
                }
                response.EnsureSuccessStatusCode();

                return new CacheData
                {
                    Data = await response.Content.ReadAsByteArrayAsync(),
                    ETag = etag
                };
            }
        }

        private byte[] GetLocalFile(string localPath)
        {
            string localFolder = Path.GetDirectoryName(localPath);
            if (!Directory.Exists(localFolder))
                Directory.CreateDirectory(localFolder);

            if (File.Exists(localPath))
                return File.ReadAllBytes(localPath);
            return null;
        }

        class CacheData
        {
            public byte[] Data { get; set; }

            public string ETag { get; set; }
        }
    }

    class CacheFile
    {
        public Dictionary<string, string> Etags = new Dictionary<string, string>();
    }
}
