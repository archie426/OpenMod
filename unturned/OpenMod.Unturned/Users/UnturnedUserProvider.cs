﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenMod.API;
using OpenMod.API.Eventing;
using OpenMod.API.Prioritization;
using OpenMod.API.Users;
using OpenMod.Core.Helpers;
using OpenMod.Core.Users;
using OpenMod.Core.Users.Events;
using SDG.Unturned;
using Steamworks;

namespace OpenMod.Unturned.Users
{
    [Priority(Priority = Priority.Low)]
    public class UnturnedUserProvider : IUserProvider, IDisposable
    {
        private readonly HashSet<UnturnedUser> m_UnturnedUsers;
        private readonly HashSet<UnturnedPendingUser> m_PendingUsers;

        private readonly IEventBus m_EventBus;
        private readonly IRuntime m_Runtime;
        private readonly IUserDataSeeder m_DataSeeder;
        private readonly IUserDataStore m_UserDataStore;

        public UnturnedUserProvider(
            IEventBus eventBus,
            IUserDataSeeder dataSeeder,
            IUserDataStore userDataStore,
            IRuntime runtime)
        {
            m_EventBus = eventBus;
            m_DataSeeder = dataSeeder;
            m_UserDataStore = userDataStore;
            m_Runtime = runtime;
            m_UnturnedUsers = new HashSet<UnturnedUser>();
            m_PendingUsers = new HashSet<UnturnedPendingUser>();

            Provider.onCheckValidWithExplanation += OnPendingPlayerConnected;
            Provider.onEnemyConnected += OnPlayerConnected;
            Provider.onEnemyDisconnected += OnEnemyDisconnected;
            Provider.onRejectingPlayer += OnRejectingPlayer;
        }

        protected virtual void OnEnemyDisconnected(SteamPlayer player)
        {
            AsyncHelper.RunSync(async () =>
            {
                var user = GetUser(player);
                if (user.Session is UnturnedUserSession session)
                {
                    session.OnSessionEnd();
                }

                var disconnectedEvent = new UserDisconnectedEvent(user);

                await m_EventBus.EmitAsync(m_Runtime, this, disconnectedEvent);
                m_UnturnedUsers.Remove(user);

                var userData = await m_UserDataStore.GetUserDataAsync(user.Id, user.Type);
                userData.LastSeen = DateTime.Now;
                await m_UserDataStore.SaveUserDataAsync(userData);
            });
        }

        public virtual UnturnedUser GetUser(SDG.Unturned.Player player)
        {
            return GetUser(player.channel.owner);
        }

        public virtual UnturnedUser GetUser(SteamPlayer player)
        {
            return GetUser(player.playerID.steamID);
        }

        public virtual UnturnedUser GetUser(CSteamID id)
        {
            return m_UnturnedUsers.FirstOrDefault(d => d.SteamId == id);
        }

        protected virtual void OnPlayerConnected(SteamPlayer player)
        {
            AsyncHelper.RunSync(() =>
            {
                var pending = m_PendingUsers.FirstOrDefault(d => d.SteamId == player.playerID.steamID);
                if (pending == null)
                    return Task.CompletedTask;

                FinishSession(pending);

                var user = new UnturnedUser(m_UserDataStore, player.player, pending);
                m_UnturnedUsers.Add(user);

                var connectedEvent = new UserConnectedEvent(user);
                return m_EventBus.EmitAsync(m_Runtime, this, connectedEvent);
            });
        }

        protected virtual void OnRejectingPlayer(CSteamID steamId, ESteamRejection rejection, string explanation)
        {
            AsyncHelper.RunSync(async () =>
            {
                var pending = m_PendingUsers.FirstOrDefault(d => d.SteamId == steamId);
                if (pending == null)
                    return;

                var disconnectedEvent = new UserDisconnectedEvent(pending);
                await m_EventBus.EmitAsync(m_Runtime, this, disconnectedEvent);

                FinishSession(pending);

                var userData = await m_UserDataStore.GetUserDataAsync(pending.Id, pending.Type);
                userData.LastSeen = DateTime.Now;
                await m_UserDataStore.SaveUserDataAsync(userData);
            });
        }

        protected virtual void OnPendingPlayerConnected(ValidateAuthTicketResponse_t callback, ref bool isValid, ref string explanation)
        {
            if (!isValid)
            {
                return;
            }

            //todo check if it is working, if not this should be patched
            var isPendingValid = isValid;
            var rejectExplanation = explanation;

            AsyncHelper.RunSync(async () =>
            {
                var steamPending = Provider.pending.FirstOrDefault(d => d.playerID.steamID == callback.m_SteamID);
                if (steamPending == null)
                    return;


                var pendingUser = new UnturnedPendingUser(m_UserDataStore, steamPending);
                await m_DataSeeder.SeedUserDataAsync(pendingUser.Id, pendingUser.Type, pendingUser.DisplayName);

                var userData = await m_UserDataStore.GetUserDataAsync(pendingUser.Id, pendingUser.Type);
                userData.LastSeen = DateTime.Now;
                userData.LastDisplayName = pendingUser.DisplayName;
                await m_UserDataStore.SaveUserDataAsync(userData);

                m_PendingUsers.Add(pendingUser);

                var userConnectingEvent = new UserConnectingEvent(pendingUser);
                await m_EventBus.EmitAsync(m_Runtime, this, userConnectingEvent);

                if (!string.IsNullOrEmpty(userConnectingEvent.RejectionReason))
                {
                    isPendingValid = false;
                    rejectExplanation = userConnectingEvent.RejectionReason;
                }

                if (userConnectingEvent.IsCancelled)
                {
                    isPendingValid = false;
                }
            });

            isValid = isPendingValid;
            explanation = rejectExplanation;
        }

        protected virtual void FinishSession(UnturnedPendingUser pending)
        {
            if (pending.Session is UnturnedPendingUserSession session)
            {
                session.OnSessionEnd();
            }

            m_PendingUsers.Remove(pending);
        }

        public bool SupportsUserType(string userType)
        {
            return userType.Equals(KnownActorTypes.Player, StringComparison.OrdinalIgnoreCase);
        }

        public Task<IUser> FindUserAsync(string userType, string searchString, UserSearchMode searchMode)
        {
            var confidence = 0;
            var unturnedUser = (IUser) null;

            foreach (var user in m_UnturnedUsers)
            {
                switch (searchMode)
                {
                    case UserSearchMode.NameOrId:
                    case UserSearchMode.Id:
                        if (user.Id.Equals(searchString, StringComparison.OrdinalIgnoreCase))
                            return Task.FromResult((IUser)user);

                        if (searchMode == UserSearchMode.NameOrId)
                            goto case UserSearchMode.Name;
                        break;

                    case UserSearchMode.Name:
                        var currentConfidence = NameConfidence(user.DisplayName, searchString, confidence);
                        if (currentConfidence > confidence)
                        {
                            unturnedUser = user;
                            confidence = currentConfidence;
                        }
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(searchMode), searchMode, null);
                }
            }

            return Task.FromResult(unturnedUser);
        }

        // ReSharper disable once MemberCanBeMadeStatic.Local
        private int NameConfidence(string userName, string searchName, int currentConfidence = -1)
        {
            switch (currentConfidence)
            {
                case 2:
                    if (userName.Equals(searchName, StringComparison.OrdinalIgnoreCase))
                        return 3;
                    goto case 1;

                case 1:
                    if (userName.StartsWith(searchName, StringComparison.OrdinalIgnoreCase))
                        return 2;
                    goto case 0;

                case 0:
                    if (userName.IndexOf(searchName, StringComparison.OrdinalIgnoreCase) != -1)
                        return 1;
                    break;

                default:
                    goto case 2;
            }

            return -1;
        }

        public Task<IReadOnlyCollection<IUser>> GetUsersAsync(string userType)
        {
            return Task.FromResult<IReadOnlyCollection<IUser>>(m_UnturnedUsers);
        }

        public void Dispose()
        {
            Provider.onCheckValidWithExplanation -= OnPendingPlayerConnected;
            Provider.onEnemyConnected -= OnPlayerConnected;
            Provider.onEnemyDisconnected -= OnEnemyDisconnected;
            Provider.onRejectingPlayer -= OnRejectingPlayer;
        }
    }
}