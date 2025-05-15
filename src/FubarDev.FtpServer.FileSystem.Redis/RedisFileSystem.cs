// <copyright file="RedisFileSystem.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FubarDev.FtpServer.BackgroundTransfer;

using StackExchange.Redis;

namespace FubarDev.FtpServer.FileSystem.Redis
{
    /// <summary>
    /// The implementation of the in-memory file system.
    /// </summary>
    public class RedisFileSystem : IUnixFileSystem
    {
        private IUnixDirectoryEntry _root;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisFileSystem"/> class.
        /// </summary>
        /// <param name="connectionMultiplexer">Redis</param>
        /// <param name="fileSystemEntryComparer">The file system entry name comparer.</param>
        public RedisFileSystem(IConnectionMultiplexer connectionMultiplexer, StringComparer fileSystemEntryComparer)
        {
            Tree = new RedisTree(connectionMultiplexer);
            Root = new RedisDirectoryEntry { Name = "/", };
            FileSystemEntryComparer = fileSystemEntryComparer;
        }

        public RedisTree Tree { get; private set; }

        /// <inheritdoc />
        public bool SupportsAppend { get; } = true;

        /// <inheritdoc />
        public bool SupportsNonEmptyDirectoryDelete { get; } = true;

        /// <inheritdoc />
        public StringComparer FileSystemEntryComparer { get; }

        /// <inheritdoc />
        public IUnixDirectoryEntry Root { get; }

        /// <inheritdoc />
        public async Task<IReadOnlyList<IUnixFileSystemEntry>> GetEntriesAsync(IUnixDirectoryEntry directoryEntry, CancellationToken ct)
        {
            return await Tree.GetEntriesAsync(directoryEntry);
        }

        /// <inheritdoc />
        public async Task<IUnixFileSystemEntry?> GetEntryByNameAsync(IUnixDirectoryEntry directoryEntry, string name, CancellationToken ct)
        {
            var entry = await Tree.GetEntryByNameAsync(directoryEntry, name);

            return entry;
        }

        /// <inheritdoc />
        public Task<IUnixFileSystemEntry> MoveAsync(IUnixDirectoryEntry parent, IUnixFileSystemEntry source, IUnixDirectoryEntry target, string fileName, CancellationToken ct)
        {
            var parentEntry = (RedisDirectoryEntry)parent;
            var sourceEntry = (RedisFileSystemEntry)source;
            var targetEntry = (RedisDirectoryEntry)target;

            //lock (parentEntry.ChildrenLock)
            //{
            //    if (!parentEntry.Children.Remove(source.Name))
            //    {
            //        targetEntry.Children.Remove(fileName);
            //        throw new FileUnavailableException(
            //            $"The source file {source.Name} couldn't be found in directory {parentEntry.Name}");
            //    }
            //}

            //var now = DateTimeOffset.Now;
            //parentEntry.SetLastWriteTime(now);
            //targetEntry.SetLastWriteTime(now);

            //lock (targetEntry.ChildrenLock)
            //{
            //    sourceEntry.Parent = targetEntry;
            //    sourceEntry.Name = fileName;
            //    targetEntry.Children.Add(fileName, source);
            //}

            return Task.FromResult(source);
        }

        /// <inheritdoc />
        public async Task UnlinkAsync(IUnixFileSystemEntry entry, CancellationToken ct)
        {
            var directory = entry as IUnixDirectoryEntry;

            if (directory != null)
            {
                await Tree.UnlinkAsync(directory);
                return;
            }

            var file = entry as IUnixFileEntry;
            await Tree.UnlinkAsync(file);
        }

        /// <inheritdoc />
        public async Task<IUnixDirectoryEntry> CreateDirectoryAsync(IUnixDirectoryEntry targetDirectory, string directoryName, CancellationToken ct)
        {
            var directory = await Tree.CreateDirectoryAsync(targetDirectory, directoryName);

            return directory;
        }

        /// <inheritdoc />
        public Task<Stream> OpenReadAsync(IUnixFileEntry fileEntry, long startPosition, CancellationToken ct)
        {
            var entry = (RedisFileEntry)fileEntry;
            var stream = new MemoryStream(entry.Data)
            {
                Position = startPosition,
            };

            return Task.FromResult<Stream>(stream);
        }

        /// <inheritdoc />
        public async Task<IBackgroundTransfer?> AppendAsync(IUnixFileEntry fileEntry, long? startPosition, Stream data, CancellationToken ct)
        {
            var entry = (RedisFileEntry)fileEntry;

            // Copy original data into memory stream
            var temp = new MemoryStream();
            temp.Write(entry.Data, 0, entry.Data.Length);

            // Set new write position (if given)
            if (startPosition.HasValue)
            {
                temp.Position = startPosition.Value;
            }

            // Copy given data
            await data.CopyToAsync(temp, 81920, ct)
                .ConfigureAwait(false);

            // Update data
            entry.Data = temp.ToArray();
            entry.SetLastWriteTime(DateTimeOffset.Now);

            return null;
        }

        /// <inheritdoc />
        public async Task<IBackgroundTransfer?> CreateAsync(IUnixDirectoryEntry targetDirectory, string fileName, Stream data, CancellationToken ct)
        {
            await Tree.CreateFileAsync(targetDirectory, fileName, data);

            return default;
        }

        /// <inheritdoc />
        public async Task<IBackgroundTransfer?> ReplaceAsync(IUnixFileEntry fileEntry, Stream data, CancellationToken ct)
        {
            var temp = new MemoryStream();
            await data.CopyToAsync(temp, 81920, ct)
                .ConfigureAwait(false);

            var entry = (RedisFileEntry)fileEntry;
            entry.Data = temp.ToArray();

            var now = DateTimeOffset.Now;
            entry.SetLastWriteTime(now);

            return null;
        }

        /// <inheritdoc />
        public Task<IUnixFileSystemEntry> SetMacTimeAsync(IUnixFileSystemEntry entry, DateTimeOffset? modify, DateTimeOffset? access, DateTimeOffset? create, CancellationToken ct)
        {
            var fsEntry = (RedisFileSystemEntry)entry;

            if (modify != null)
            {
                fsEntry.SetLastWriteTime(modify.Value);
            }

            if (create != null)
            {
                fsEntry.SetCreateTime(create.Value);
            }

            return Task.FromResult(entry);
        }
    }
}
