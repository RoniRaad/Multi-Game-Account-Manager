using AccountManager.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using AccountManager.Infrastructure.Clients;

namespace AccountManager.Infrastructure.CachedClients
{
    public class CachedLeagueTokenClient : ILeagueTokenClient
    {
        private readonly ILeagueTokenClient _tokenClient;
        private readonly IMemoryCache _memoryCache;
        private static readonly SemaphoreSlim semaphore = new(1, 1);
        public CachedLeagueTokenClient(IMemoryCache memoryCache, ILeagueTokenClient tokenClient)
        {
            _memoryCache = memoryCache;
            _tokenClient = tokenClient;
        }

        public async Task<string> CreateLeagueSession(Account account)
        {
            return await _tokenClient.CreateLeagueSession(account);
        }

        public async Task<string> GetLeagueSessionToken(Account account)
        {
            var cacheKey = nameof(GetLeagueSessionToken);
            await semaphore.WaitAsync();
            try
            {
                if (_memoryCache.TryGetValue(cacheKey, out string? sessionToken)
                && sessionToken is not null
                && await TestLeagueToken(sessionToken))
                            return sessionToken;

                sessionToken = await _tokenClient.GetLeagueSessionToken(account);

                if (!string.IsNullOrEmpty(sessionToken))
                    _memoryCache.Set(cacheKey, sessionToken);

                return sessionToken;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                semaphore.Release(1);
            }
        }

        public async Task<string> GetLocalSessionToken()
        {
            return await _tokenClient.GetLocalSessionToken();
        }

        public async Task<bool> TestLeagueToken(string token)
        {
            return await _tokenClient.TestLeagueToken(token);
        }
    }
}
