using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text.Json;
using System.Threading.Tasks;

using StackExchange.Redis;

namespace FubarDev.FtpServer.FileSystem.Redis
{
    public class RedisTree
    {
        private const string RootPoint = "";//"ftp:root:";

        private readonly IConnectionMultiplexer _connection;

        public RedisTree(IConnectionMultiplexer connection)
        {
            _connection = connection;
            var db = _connection.GetDatabase();
        }

        public async Task<IReadOnlyList<IUnixFileSystemEntry>> GetEntriesAsync(IUnixDirectoryEntry directoryEntry)
        {
            var directory = (RedisDirectoryEntry)directoryEntry;
            var key = GetDirectoryKey(directory.ParentPath, directory.Name);

            var db = _connection.GetDatabase();
            var hashEntries = await db.HashGetAllAsync(key);

            var entries = new List<IUnixFileSystemEntry>();

            foreach (var item in hashEntries)
            {
                var fieldName = item.Name.ToString();
                var json = item.Value.ToString();

                try
                {
                    if (fieldName.EndsWith(":file"))
                    {
                        var entry = JsonSerializer.Deserialize<RedisFileEntry>(json);
                        entries.Add(entry);
                    }
                    else
                    {
                        var entry = JsonSerializer.Deserialize<RedisDirectoryEntry>(json);
                        entries.Add(entry);
                    }
                }
                catch (Exception ex)
                {

                }
            }

            return entries;
        }

        public async Task<IUnixFileSystemEntry> GetEntryByNameAsync(IUnixDirectoryEntry directoryEntry, string name)
        {
            var directory = (RedisDirectoryEntry)directoryEntry;
            var hashKey = $"{RootPoint}{directory.FullName}";
            var fieldKey = GetDirectoryKey(directory.FullName, name);

            var db = _connection.GetDatabase();
            //var keys = db.HashKeys(hashKey);

            var directoryJson = await db.HashGetAsync(hashKey, fieldKey);

            if (directoryJson.HasValue)
            {
                var entry = JsonSerializer.Deserialize<RedisDirectoryEntry>(directoryJson);

                return entry;
            }

            var fileJson = await db.HashGetAsync(hashKey, $"{name}:file");

            if (fileJson.HasValue)
            {
                var entry = JsonSerializer.Deserialize<RedisFileEntry>(fileJson);

                return entry;
            }

            return null;
        }

        public async Task<RedisDirectoryEntry> CreateDirectoryAsync(IUnixDirectoryEntry directoryEntry, string directoryName)
        {
            var currentDirectory = (RedisDirectoryEntry)directoryEntry;
            var parentHashKey = $"{RootPoint}{currentDirectory.FullName}";

            var directory = new RedisDirectoryEntry
            {
                Name = directoryName,
                ParentPath = currentDirectory.FullName,
                CreatedTime = DateTime.UtcNow,
                LastWriteTime = DateTime.UtcNow,
            };

            var json = JsonSerializer.Serialize(directory);

            var key = $"{RootPoint}{directory.FullName}";

            var db = _connection.GetDatabase();

            await db.HashSetAsync(parentHashKey, key, json);

            await db.KeyExpireAsync(parentHashKey, TimeSpan.FromMinutes(3));

            return directory;
        }

        internal async Task CreateFileAsync(IUnixDirectoryEntry targetDirectory, string fileName, Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var content = ms.ToArray();

            var directory = (RedisDirectoryEntry)targetDirectory;
            var entry = new RedisFileEntry(directory, fileName, content);
            entry.LastWriteTime = entry.CreatedTime = DateTimeOffset.Now;
            var hashKey = $"{RootPoint}{directory.FullName}";
            entry.HashKey = hashKey;

            var json = JsonSerializer.Serialize(entry);

            var db = _connection.GetDatabase();

            await db.HashSetAsync(hashKey, entry.FieldName, json);

            await db.HashFieldExpireAsync(hashKey, [entry.FieldName], TimeSpan.FromMinutes(3)); //Для отладки удаляем через 3 минуты
        }

        public async Task UnlinkAsync(IUnixDirectoryEntry targetDirectory)
        {
            var directory = (RedisDirectoryEntry)targetDirectory;
            var hashKey = $"{RootPoint}{directory.FullName}";

            var db = _connection.GetDatabase();

            await db.KeyDeleteAsync(hashKey);

            var parentHashKey = $"{RootPoint}{directory.ParentPath}";
            await db.HashDeleteAsync(parentHashKey, hashKey);
        }

        public async Task UnlinkAsync(IUnixFileEntry targetFile)
        {
            var file = (RedisFileEntry)targetFile;

            var db = _connection.GetDatabase();

            await db.HashDeleteAsync(file.HashKey, file.FieldName);
        }

        public async Task WriteAsync(Action<string> logger)
        {
            var db = _connection.GetDatabase();
            var directories = await db.ListRangeAsync(RootPoint);

            foreach (var directory in directories)
            {
                logger(directory);

                var files = await db.HashKeysAsync((string)directory);

                foreach (var file in files)
                {
                    logger(file);
                }
            }
        }

        private static string GetDirectoryKey(string parentPath, string directoryName)
        {
            if (parentPath == null)
                return $"{RootPoint}{directoryName}";

            if (parentPath == "/")
                return $"{RootPoint}{parentPath}{directoryName}";

            return $"{RootPoint}{parentPath}/{directoryName}";
        }
    }
}
