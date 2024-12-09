// <copyright file="Issue30CustomFtpUser.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using FluentFTP;
using FluentFTP.Exceptions;

using FubarDev.FtpServer.AccountManagement;

using Microsoft.Extensions.DependencyInjection;

using Xunit;
using Xunit.Abstractions;

namespace FubarDev.FtpServer.Tests.Issues
{
    public class Issue30CustomFtpUser : FtpServerTestsBase
    {
        public Issue30CustomFtpUser(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Fact]
        public async Task LoginSucceedsWithTester()
        {
            using var client = new AsyncFtpClient("127.0.0.1", new NetworkCredential("tester", "test"), Server.Port);
            await client.Connect();
        }

        [Fact]
        public async Task LogoutCalledAfterSuccessfulLogin()
        {
            using (var client = new AsyncFtpClient("127.0.0.1", new NetworkCredential("tester", "test"), Server.Port))
            {
                await client.Connect();
            }

            var membershipProvider =
                (CustomMembershipProvider)ServiceProvider.GetRequiredService<IMembershipProvider>();
            var maxDelay = TimeSpan.FromSeconds(2);
            var start = DateTimeOffset.UtcNow;
            while (membershipProvider.LogoutCalled == 0)
            {
                await Task.Delay(100);
                var end = DateTimeOffset.UtcNow;
                var delay = end - start;
                if (delay > maxDelay)
                {
                    break;
                }
            }

            Assert.Equal(1, membershipProvider.LogoutCalled);
        }

        [Fact]
        public async Task LoginFailsWithWrongUserName()
        {
            using var client = new AsyncFtpClient("127.0.0.1", new NetworkCredential("testerX", "test"), Server.Port);
            await Assert.ThrowsAsync<FtpAuthenticationException>(() => client.Connect())
               .ConfigureAwait(false);
        }

        [Fact]
        public async Task LoginFailsWithWrongPassword()
        {
            using var client = new AsyncFtpClient("127.0.0.1", new NetworkCredential("tester", "testX"), Server.Port);
            await Assert.ThrowsAsync<FtpAuthenticationException>(() => client.Connect())
               .ConfigureAwait(false);
        }

        /// <inheritdoc />
        protected override IFtpServerBuilder Configure(IFtpServerBuilder builder)
        {
            return builder
               .UseSingleRoot()
               .UseInMemoryFileSystem();
        }

        /// <inheritdoc />
        protected override IServiceCollection Configure(IServiceCollection services)
        {
            return base.Configure(services)
               .AddSingleton<IMembershipProvider, CustomMembershipProvider>();
        }

        private class CustomMembershipProvider : IMembershipProviderAsync
        {
            public int LogoutCalled { get; set; }

            /// <inheritdoc />
            public Task<MemberValidationResult> ValidateUserAsync(
                string username,
                string password,
                CancellationToken cancellationToken)
            {
                if (username == "tester" && password == "test")
                {
                    var identity = new ClaimsIdentity();
                    return Task.FromResult(
                        new MemberValidationResult(
                            MemberValidationStatus.AuthenticatedUser,
                            new ClaimsPrincipal(identity)));
                }

                return Task.FromResult(new MemberValidationResult(MemberValidationStatus.InvalidLogin));
            }

            /// <inheritdoc />
            public Task LogOutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
            {
                LogoutCalled += 1;
                return Task.CompletedTask;
            }

            /// <inheritdoc />
            public Task<MemberValidationResult> ValidateUserAsync(string username, string password)
            {
                return ValidateUserAsync(username, password, CancellationToken.None);
            }
        }
    }
}
