// <copyright file="RedisFtpServerBuilderExtensions.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using FubarDev.FtpServer.FileSystem;
using FubarDev.FtpServer.FileSystem.Redis;

using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace FubarDev.FtpServer
{
    /// <summary>
    /// Extension methods for <see cref="IFtpServerBuilder"/>.
    /// </summary>
    public static class RedisFtpServerBuilderExtensions
    {
        /// <summary>
        /// Uses the in-memory file system API.
        /// </summary>
        /// <param name="builder">The server builder used to configure the FTP server.</param>
        /// <returns>the server builder used to configure the FTP server.</returns>
        public static IFtpServerBuilder UseRedisFileSystem(this IFtpServerBuilder builder)
        {
            builder.Services.AddSingleton<IFileSystemClassFactory, RedisFileSystemProvider>();
            return builder;
        }
    }
}
