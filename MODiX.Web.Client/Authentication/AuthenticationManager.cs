﻿using System;
using System.Reactive.Subjects;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Browser;

using Modix.Web.Protocol.Authentication;

namespace Modix.Web.Client.Authentication
{
    public interface IAuthenticationManager
    {
        ulong? ActiveGuildId { get; set; }

        IObservable<ulong?> ActiveGuildIdChanged { get; }

        string? BearerToken { get; }

        IObservable<AuthenticationTicket?> CurrentTicket { get; }

        Task<AuthenticationState> GetAuthenticationStateAsync();

        void OnSignedIn(string bearerToken, AuthenticationTicket ticket);

        void OnSignedOut();
    }

    public class AuthenticationManager
        : AuthenticationStateProvider,
            IAuthenticationManager
    {
        public AuthenticationManager(ILocalStorageManager localStorageManager)
        {
            _activeGuildIdChanged   = new();
            _bearerToken            = null;
            _currentState           = _unauthenticatedState;
            _currentTicket          = new(null);
            _localStorageManager    = localStorageManager;

            TryLoadFromLocalStorage();

            async void TryLoadFromLocalStorage()
            {
                try
                {
                    var guildId = await _localStorageManager.TryGetValueAsync<ulong>(ActiveGuildIdStorageKey);
                    if (guildId is not null)
                        ActiveGuildId = guildId.Value;

                    var token   = await _localStorageManager.TryGetObjectAsync<string>(BearerTokenStorageKey);
                    var ticket  = await _localStorageManager.TryGetObjectAsync<AuthenticationTicket>(CurrentTicketStorageKey);
                    if ((token is not null) && (ticket is not null))
                        OnSignedIn(token, ticket);
                }
                catch (Exception ex)
                {
                    // TODO: Implement Logging
                    Console.WriteLine("Exception loading authentication data from local storage: " + ex.Message);
                }
            }
        }

        public ulong? ActiveGuildId
        {
            get => _activeGuildId;
            set
            {
                if (value != _activeGuildId)
                {
                    _activeGuildId = value;
                    _activeGuildIdChanged.OnNext(value);
                }
            }
        }

        public IObservable<ulong?> ActiveGuildIdChanged
            => _activeGuildIdChanged;

        public string? BearerToken
            => _bearerToken;

        public IObservable<AuthenticationTicket?> CurrentTicket
            => _currentTicket;

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(_currentState);

        public void OnSignedIn(string bearerToken, AuthenticationTicket ticket)
        {
            SaveToLocalStorage(bearerToken, ticket);

            _bearerToken = bearerToken;
            _currentTicket.OnNext(ticket);

            var state = new AuthenticationState(
                new ClaimsPrincipal(
                    new ClaimsIdentity(
                        claims:             new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, ticket.UserId.ToString(), ClaimValueTypes.UInteger64)
                        },
                        authenticationType: "Modix.Web.Protocol.Authentication.AuthenticationTicket")));


            _currentState = state;
            NotifyAuthenticationStateChanged(Task.FromResult(_currentState));

            async void SaveToLocalStorage(string token, AuthenticationTicket ticket)
            {
                try
                {
                    await _localStorageManager.SetValueAsync(BearerTokenStorageKey, token);
                    await _localStorageManager.SetValueAsync(CurrentTicketStorageKey, ticket);
                }
                catch (Exception ex)
                {
                    // TODO: Implement Logging
                    Console.WriteLine("Exception saving authentication data to local storage: " + ex.Message);
                }
            }
        }

        public void OnSignedOut()
        {
            ClearLocalStorage();

            _bearerToken = null;
            _currentTicket.OnNext(null);
            _currentState = _unauthenticatedState;
            NotifyAuthenticationStateChanged(Task.FromResult(_currentState));

            async void ClearLocalStorage()
            {
                try
                {
                    await _localStorageManager.RemoveKeyAsync(BearerTokenStorageKey);
                    await _localStorageManager.RemoveKeyAsync(CurrentTicketStorageKey);
                }
                catch (Exception ex)
                {
                    // TODO: Implement Logging
                    Console.WriteLine("Exception saving authentication data to local storage: " + ex.Message);
                }
            }
        }

        private readonly Subject<ulong?>                        _activeGuildIdChanged;
        private readonly BehaviorSubject<AuthenticationTicket?> _currentTicket;
        private readonly ILocalStorageManager                   _localStorageManager;

        private ulong?              _activeGuildId;
        private string?             _bearerToken;
        private AuthenticationState _currentState;

        private const string ActiveGuildIdStorageKey
            = "AuthenticationManager.ActiveGuildId";
        private const string BearerTokenStorageKey
            = "AuthenticationManager.BearerToken";
        private const string CurrentTicketStorageKey
            = "AuthenticationManager.CurrentTicket";

        private static readonly AuthenticationState _unauthenticatedState
            = new(new ClaimsPrincipal());
    }
}
