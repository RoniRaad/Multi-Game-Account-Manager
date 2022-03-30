﻿using AccountManager.Core.Models.RiotGames.Valorant;
using System.Net;

namespace AccountManager.Core.Models.RiotGames
{
    public class RiotAuthResponse
    {
        public TokenResponseWrapper Content { get; set; }
        public RiotAuthCookies Cookies { get; set; }
    }
    public class RiotAuthCookies
    {
        public Cookie? Tdid { get; set; }
        public Cookie? Ssid { get; set; }
        public Cookie? Sub { get; set; }
        public Cookie? Csid { get; set; }
        public Cookie? Clid { get; set; }
        public Cookie? Asid { get; set; }

        public CookieCollection GetCollection()
        {
            var cookies = new CookieCollection();
            if (Tdid is not null)
                cookies.Add(Tdid);
            if (Ssid is not null)
                cookies.Add(Ssid);
            if (Sub is not null)
                cookies.Add(Sub);
            if (Csid is not null)
                cookies.Add(Csid);
            if (Clid is not null)
                cookies.Add(Clid);
            if (Asid is not null)
                cookies.Add(Asid);

            return cookies;
        }
    }
}