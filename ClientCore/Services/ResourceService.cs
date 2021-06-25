using CefSharp;
using Clutch.DeltaModel;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using ResourceType = Streamster.ClientData.Model.ResourceType;

namespace Streamster.ClientCore.Services
{
    public class ResourceService
    {
        public CoreData _coreData;
        private readonly string _folder;
        private Dictionary<string, byte[]> _cache = new Dictionary<string, byte[]>();

        private Dictionary<string, bool> _errorCache = new Dictionary<string, bool>();

        public ResourceService(CoreData coreData, IAppEnvironment environment)
        {
            _coreData = coreData;
            _folder = Path.Combine(environment.GetStorageFolder(), "Resources");
        }

        public void Start()
        {
            try
            {
                if (!Directory.Exists(_folder))
                    Directory.CreateDirectory(_folder);
            }
            catch(Exception e)
            {
                Log.Error(e, $"Cannot create folder {_folder}");
            }

            _coreData.Subscriptions.SubscribeForType<IResource>((i, t) => CreateCacheItem(i, t));
        }

        private void CreateCacheItem(IResource resource, ChangeType changeType)
        {
            var id = _coreData.GetId(resource);
            if (resource.Data != null && !File.Exists(GetFilePath(id)))
            {
                SaveToFile(id, resource.Data);
                lock (_cache)
                    _cache[id] = resource.Data;
            }
        }

        public byte[] GetResource(string id)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(id, out var res))
                    return res;

                if (_errorCache.ContainsKey(id))
                    return null;
            }
            try
            {

                if (!File.Exists(GetFilePath(id)))
                {
                    Log.Warning($"No file for '{id}'");
                    lock (_cache)
                        _errorCache[id] = true;
                }
                else
                {
                    var data = File.ReadAllBytes(GetFilePath(id));

                    lock (_cache)
                        _cache[id] = data;

                    return data;
                }
            }
            catch (Exception e)
            {
                lock (_cache)
                    _errorCache[id] = true;
                Log.Warning(e, $"Failed to load '{id}' from file cache");
            }
            return null;
        }

        internal string AddResource(string fileName, byte[] data, ResourceType type)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);

            var res = _coreData.Create<IResource>();
            var now = DateTime.UtcNow;
            res.LastUse = now;
            res.Data = data;
            res.Info = new ResourceInfo { Added = now, Type = GetResourceType(fileName, type), Name = name, DataHash = GetHash(data) };

            var id = IdGenerator.New();

            SaveToFile(id, data);
            lock (_cache)
                _cache[id] = data;
            _coreData.Root.Resources[id] = res;
            return id;
        }

        private string GetFilePath (string id) => Path.Combine(_folder, id);

        private void SaveToFile(string id, byte[] data)
        {
            try
            {
                File.WriteAllBytes(GetFilePath(id), data);
            }
            catch (Exception e)
            {
                Log.Error(e, $"failed to save '{id}' into file cache");
            }
        }

        private ResourceType GetResourceType(string fileName, ResourceType type)
        {
            var ext = Path.GetExtension(fileName)?.ToLower();

            if (type == ResourceType.ImageJpeg || type == ResourceType.ImagePng)
            {
                if (ext == ".png")
                    return ResourceType.ImagePng;
                else
                    return ResourceType.ImageJpeg;
            }
            else if (type == ResourceType.LutCube || type == ResourceType.LutPng)
            {
                if (ext == ".png")
                    return ResourceType.LutPng;
                else
                    return ResourceType.LutCube;
            }
            else return type;
        }

        private string GetHash(byte[] data)
        {
            using (MD5 md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(data);
                return Convert.ToBase64String(hash);
            }
        }
    }

    public class ResourceCacheItem
    {

    }
}
