// <copyright file="RedisFileEntry.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using FubarDev.FtpServer.FileSystem.Generic;

namespace FubarDev.FtpServer.FileSystem.Redis
{
    /// <summary>
    /// The in-memory file.
    /// </summary>
    public class RedisFileEntry : RedisFileSystemEntry, IUnixFileEntry
    {
        private static readonly IUnixPermissions _defaultPermissions = new GenericUnixPermissions(
            new GenericAccessMode(true, true, false),
            new GenericAccessMode(true, true, false),
            new GenericAccessMode(true, false, false));

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisFileEntry"/> class.
        /// </summary>
        /// <param name="parent">The parent entry.</param>
        /// <param name="name">The name of this entry.</param>
        /// <param name="data">The file data.</param>
        public RedisFileEntry(
            RedisDirectoryEntry parent,
            string name,
            byte[] data)
            : base(parent, name, _defaultPermissions)
        {
            Data = data;
        }

        /// <inheritdoc />
        public long Size => Data.Length;

        /// <summary>
        /// Gets or sets the data of this file entry.
        /// </summary>
        public byte[] Data { get; set; }
    }
}
