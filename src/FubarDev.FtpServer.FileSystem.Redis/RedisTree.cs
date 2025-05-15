using System;
using System.Collections.Generic;
using System.Linq;
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
                try
                {
                    var entry = JsonSerializer.Deserialize<RedisDirectoryEntry>(item.Value);
                    entries.Add(entry);
                }
                catch (Exception ex)
                {

                }
            }

            return entries;
        }

        public async Task<IUnixFileSystemEntry> GetEntryByNameAsync(IUnixDirectoryEntry directoryEntry, string directoryName)
        {
            var directory = (RedisDirectoryEntry)directoryEntry;
            var hashKey = $"{RootPoint}{directory.FullName}";
            var fieldKey = GetDirectoryKey(directory.FullName, directoryName);

            var db = _connection.GetDatabase();

            var json = await db.HashGetAsync(hashKey, fieldKey);

            if (json.HasValue)
            {
                var entry = JsonSerializer.Deserialize<RedisDirectoryEntry>(json);

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

            var db = _connection.GetDatabase();
            var key = $"{RootPoint}{directory.FullName}";

            //Устанавливаем в Hash первым полем id: сам JSON дериктории
            await db.HashSetAsync(parentHashKey, key, json);

            await db.KeyExpireAsync(parentHashKey, TimeSpan.FromMinutes(3));

            ////На время дебага удаляем автоматически
            //await db.KeyExpireAsync(key, TimeSpan.FromMinutes(3));

            ////Установка у родителя ссылки на потомка
            //var parentHashKey = $"{RootPoint}:{parentPath}";
            //await db.HashSetAsync(parentHashKey, key, 0);

            return directory;
        }

        public async Task CreateFileAsync(string directory, string fileName, byte[] content)
        {
            var db = _connection.GetDatabase();
            await db.HashSetAsync(GetDirectoryKey(directory, directory), fileName, content);
        }

        public async Task<RedisFile[]> GetFilesAsync(string directory)
        {
            var db = _connection.GetDatabase();
            var files = await db.HashGetAllAsync(GetDirectoryKey(directory, directory));

            return files.Select(x => new RedisFile(x.Name, x.Value)).ToArray();
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

        public class RedisFile(string name, byte[] content)
        {
            public string Name = name;
            public byte[] Content = content;
        }
    }
}
