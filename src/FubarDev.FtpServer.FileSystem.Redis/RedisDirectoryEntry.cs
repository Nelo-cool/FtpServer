// <copyright file="RedisDirectoryEntry.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System;
using System.Text.Json.Serialization;

using FubarDev.FtpServer.FileSystem.Generic;

namespace FubarDev.FtpServer.FileSystem.Redis
{
    /// <summary>
    /// The im-memory directory entry.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="RedisDirectoryEntry"/> class.
    /// </remarks>
    public class RedisDirectoryEntry : IUnixDirectoryEntry
    {
        public RedisDirectoryEntry()
        {
            Permissions = new GenericUnixPermissions(
                new GenericAccessMode(true, true, true),
                new GenericAccessMode(true, true, true),
                new GenericAccessMode(true, true, true));
        }

        public bool IsRoot => ParentPath == null;

        /// <inheritdoc/>
        public bool IsDeletable => !IsRoot;

        public string Name { get; set; }

        public string ParentPath { get; set; }

        public string FullName => ParentPath != "/" && Name != "/" ? $"{ParentPath}/{Name}" : $"{ParentPath}{Name}";

        [JsonIgnore]
        public IUnixPermissions Permissions { get; set; }
        public DateTimeOffset? LastWriteTime { get; set; }

        public DateTimeOffset? CreatedTime { get; set; }

        public long NumberOfLinks { get; } = 1;

        /// <inheritdoc/>
        public string Owner => "owner";

        /// <inheritdoc/>
        public string Group => "group";
    }
}
