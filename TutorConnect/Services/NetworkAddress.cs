using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TutorConnect.Services
{
    /// <summary>
    /// Lets phones reach the site during development. On a phone "localhost" means the phone itself, so QR codes
    /// use this computer's Wi-Fi/LAN address instead (phones must be on the same network). Tools\EnablePhoneAccess.cmd
    /// allows IIS Express to accept those connections.
    /// </summary>
    public static class NetworkAddress
    {
        private static readonly string[] VirtualAdapters = { "virtual", "vethernet", "hyper-v", "vmware", "virtualbox", "loopback", "bluetooth", "wsl", "docker", "tap", "vpn" };

        public static bool IsLocalHost(string host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "::1" || host == "[::1]";
        }

        /// <summary>This computer's LAN IPv4 address (the adapter with a default gateway, preferring Wi-Fi and Ethernet), or null.</summary>
        public static string LanIPv4()
        {
            try
            {
                var candidates = new List<Tuple<int, string>>();
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback || nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    var name = (nic.Name + " " + nic.Description).ToLowerInvariant();
                    if (VirtualAdapters.Any(v => name.Contains(v))) continue;
                    var props = nic.GetIPProperties();
                    var hasGateway = props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                    foreach (var ua in props.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ua.Address)) continue;
                        var ip = ua.Address.ToString();
                        if (ip.StartsWith("169.254.")) continue;   // no DHCP address
                        var score = (hasGateway ? 10 : 0)
                                  + (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 3 : 0)
                                  + (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 2 : 0);
                        candidates.Add(Tuple.Create(score, ip));
                    }
                }
                return candidates.OrderByDescending(c => c.Item1).Select(c => c.Item2).FirstOrDefault();
            }
            catch (Exception ex)
            {
                AppLog.Error("NetworkAddress.LanIPv4", ex);
                return null;
            }
        }

        /// <summary>
        /// The address phones should use. SiteBaseUrl (e.g. a public server or tunnel) wins; otherwise, when the site is
        /// open on localhost, the same port on this computer's LAN address.
        /// </summary>
        public static string PhoneBaseUrl(Uri requestUrl, string applicationPath)
        {
            var configured = AppConfig.Get("SiteBaseUrl");
            if (!string.IsNullOrEmpty(configured)) return configured.TrimEnd('/');
            var app = (applicationPath ?? "").TrimEnd('/');
            if (!IsLocalHost(requestUrl.Host)) return requestUrl.GetLeftPart(UriPartial.Authority) + app;
            var ip = LanIPv4();
            if (ip == null) return requestUrl.GetLeftPart(UriPartial.Authority) + app;
            return requestUrl.Scheme + "://" + ip + ":" + requestUrl.Port + app;
        }

        /// <summary>True when the phone address answers (so IIS Express accepts outside connections). Quick, cached for a minute.</summary>
        public static bool IsReachable(string baseUrl)
        {
            lock (Sync)
            {
                bool cached;
                DateTime at;
                if (Checks.TryGetValue(baseUrl, out cached) && Times.TryGetValue(baseUrl, out at) && (DateTime.UtcNow - at).TotalSeconds < 60) return cached;
                var ok = false;
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(baseUrl + "/Home/Privacy");
                    req.Method = "HEAD"; req.Timeout = 2500; req.AllowAutoRedirect = false; req.Proxy = null;
                    using (var res = (HttpWebResponse)req.GetResponse()) ok = (int)res.StatusCode < 500;
                }
                catch (WebException ex)
                {
                    var r = ex.Response as HttpWebResponse;
                    // A 400 "Bad Request - Invalid Hostname" means IIS Express isn't bound to this address yet.
                    ok = r != null && (int)r.StatusCode < 500 && (int)r.StatusCode != 400;
                }
                catch (Exception) { ok = false; }
                Checks[baseUrl] = ok; Times[baseUrl] = DateTime.UtcNow;
                return ok;
            }
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, bool> Checks = new Dictionary<string, bool>();
        private static readonly Dictionary<string, DateTime> Times = new Dictionary<string, DateTime>();
    }
}
