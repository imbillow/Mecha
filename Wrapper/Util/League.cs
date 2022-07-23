#nullable enable
using System;
using Newtonsoft.Json;

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Wrapper.Util
{
    class League
    {
        public static bool IsLeagueOpen()
            => LeagueClientUx != null;

        public static Process? LeagueClientUx => Process.GetProcessesByName("LeagueClientUx")[0];

        public static string? GetLeaguePath()
            => LeagueClientUx?.MainModule?.FileName.Replace("LeagueClientUx.exe", "");

        public static ushort? AppPort
        {
            get
            {
                var cmd = GetProcessCommandLine(LeagueClientUx);
                if (cmd == null)
                {
                    return null;
                }
                var re = new Regex("--riotclient-app-port=(?<port>\\d+)");
                var portString = re?.Match(cmd).Groups["port"]?.Value;
                if (portString != null && ushort.TryParse(portString, out var port))
                {
                    return port;
                }

                return null;
            }
        }

        public static string? AuthToken
        {
            get
            {
                var cmd = GetProcessCommandLine(LeagueClientUx);
                if (cmd == null)
                {
                    return null;
                }
                var re = new Regex("--riotclient-auth-token=(?<token>\\S+)");
                return re?.Match(cmd).Groups["token"].Value;
            }
        }

        public static void KillLeagueProcesses()
        {
            Process[] procs = Process
                .GetProcessesByName("LeagueClientUx")
                .Concat(Process.GetProcessesByName("LeagueClient"))
                .Concat(Process.GetProcessesByName("LeagueClientUxRender"))
                .ToArray();

            foreach (Process proc in procs)
            {
                if (!proc.HasExited)
                {
                    proc.Kill();
                    proc.WaitForExit();
                }
            }
        }

        static string? GetProcessCommandLine(Process? process)
        {
            if (process == null)
            {
                return null;
            }
            using var searcher = new ManagementObjectSearcher(
                       "SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id);
            using var objects = searcher.Get();
            var @object = objects.Cast<ManagementBaseObject>().SingleOrDefault();
            return @object?["CommandLine"]?.ToString();
        }

        public static void OpenDevTools()
        {
            if (AppPort == null)
            {
                MessageBox.Show(@"Not found app port");
                return;
            }
            string port = AppPort.ToString();
            string resp;

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create($"http://localhost:{port}/json");
                request.AutomaticDecompression = DecompressionMethods.GZip;

                using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                using Stream stream = response.GetResponseStream()!;
                using StreamReader sr = new StreamReader(stream);
                resp = sr.ReadToEnd();

                if (string.IsNullOrEmpty(resp))
                {
                    return;
                }

                dynamic json = JsonConvert.DeserializeObject(resp);

                Process.Start($"http://localhost:{port}{json[0].devtoolsFrontendUrl}");

                return;
            }
            catch (Exception e)
            {
                MessageBox.Show($@"There was an error while opening the DevTools URL. {e.Message}", @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }
        }
    }
}
