﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenMod.API.Ioc;
using OpenMod.API.Prioritization;
using OpenMod.API.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenMod.Core.Helpers;

namespace OpenMod.Core.Users
{
    [ServiceImplementation(Lifetime = ServiceLifetime.Singleton, Priority = Priority.Lowest)]
    public class UserManager : IUserManager, IAsyncDisposable
    {
        private readonly List<IUserProvider> m_UserProviders;
        public UserManager(IOptions<UserManagerOptions> options, IServiceProvider serviceProvider)
        {
            m_UserProviders = new List<IUserProvider>();
            foreach (var provider in options.Value.UserProviderTypes)
            {
                m_UserProviders.Add((IUserProvider)ActivatorUtilities.CreateInstance(serviceProvider, provider));
            }
        }

        public IReadOnlyCollection<IUserProvider> UserProviders
        {
            get { return m_UserProviders; }
        }

        public async Task<IReadOnlyCollection<IUser>> GetUsersAsync(string type)
        {
            var list = new List<IUser>();

            foreach (var userProvider in UserProviders.Where(d => d.SupportsUserType(type)))
            {
                list.AddRange(await userProvider.GetUsersAsync(type));
            }

            return list;
        }

        public async Task<IUser> FindUserAsync(string type, string searchString, UserSearchMode searchMode)
        {
            foreach (var userProvider in UserProviders.Where(d => d.SupportsUserType(type)))
            {
                var user = await userProvider.FindUserAsync(type, searchString, searchMode);
                if (user != null)
                {
                    return user;
                }
            }

            return null;
        }

        public async ValueTask DisposeAsync()
        {
            await UserProviders.DisposeAllAsync();
        }
    }
}