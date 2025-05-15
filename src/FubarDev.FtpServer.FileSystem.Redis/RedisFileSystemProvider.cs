// <copyright file="RedisFileSystemProvider.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace FubarDev.FtpServer.FileSystem.Redis
{
    /// <summary>
    /// An implementation of an in-memory file system.
    /// </summary>
    public class RedisFileSystemProvider : IFileSystemClassFactory
    {
        private readonly IAccountDirectoryQuery _accountDirectoryQuery;
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly bool _keepAnonymousFileSystem;

        private readonly bool _keepAuthenticatedUserFileSystem;

        private readonly StringComparer _fileSystemComparer;

        private readonly object _anonymousFileSystemLock = new ();

        private readonly object _authUserFileSystemLock = new ();

        private readonly Dictionary<string, RedisFileSystem> _anonymousFileSystems;

        private readonly Dictionary<string, RedisFileSystem> _authUserFileSystems;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisFileSystemProvider"/> class.
        /// </summary>
        /// <param name="options">The provider options.</param>
        /// <param name="accountDirectoryQuery">Interface to query account directories.</param>
        /// <param name="connectionMultiplexer">Redis</param>
        public RedisFileSystemProvider(
            IOptions<RedisFileSystemOptions> options,
            IAccountDirectoryQuery accountDirectoryQuery,
            IConnectionMultiplexer connectionMultiplexer)
        {
            _accountDirectoryQuery = accountDirectoryQuery;
            _connectionMultiplexer = connectionMultiplexer;
            _fileSystemComparer = options.Value.FileSystemComparer;
            _keepAnonymousFileSystem = options.Value.KeepAnonymousFileSystem;
            _keepAuthenticatedUserFileSystem = options.Value.KeepAuthenticatedUserFileSystem;
            _anonymousFileSystems = new Dictionary<string, RedisFileSystem>(options.Value.AnonymousComparer);
            _authUserFileSystems = new Dictionary<string, RedisFileSystem>(options.Value.UserNameComparer);
        }

        /// <inheritdoc />
        public Task<IUnixFileSystem> Create(IAccountInformation accountInformation)
        {
            var user = accountInformation.FtpUser;
            RedisFileSystem fileSystem;

            var directories = _accountDirectoryQuery.GetDirectories(accountInformation);
            var fileSystemId = directories.RootPath ?? string.Empty;
            if (user.IsAnonymous())
            {
                if (_keepAnonymousFileSystem)
                {
                    lock (_anonymousFileSystemLock)
                    {
                        if (!_anonymousFileSystems.TryGetValue(fileSystemId, out fileSystem))
                        {
                            fileSystem = new RedisFileSystem(_connectionMultiplexer, _fileSystemComparer);
                            _anonymousFileSystems.Add(fileSystemId, fileSystem);
                        }
                    }
                }
                else
                {
                    fileSystem = new RedisFileSystem(_connectionMultiplexer, _fileSystemComparer);
                }
            }
            else
            {
                if (_keepAuthenticatedUserFileSystem)
                {
                    lock (_authUserFileSystemLock)
                    {
                        if (!_authUserFileSystems.TryGetValue(fileSystemId, out fileSystem))
                        {
                            fileSystem = new RedisFileSystem(_connectionMultiplexer, _fileSystemComparer);
                            _authUserFileSystems.Add(fileSystemId, fileSystem);
                        }
                    }
                }
                else
                {
                    fileSystem = new RedisFileSystem(_connectionMultiplexer, _fileSystemComparer);
                }
            }

            return Task.FromResult<IUnixFileSystem>(fileSystem);
        }
    }
}
