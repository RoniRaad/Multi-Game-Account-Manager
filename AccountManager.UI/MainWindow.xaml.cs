using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using AccountManager.Core.Enums;
using AccountManager.Core.Interfaces;
using AccountManager.Core.Models;
using AccountManager.Core.Models.AppSettings;
using AccountManager.Core.Models.RiotGames.League;
using AccountManager.Core.Models.RiotGames.League.Requests;
using AccountManager.Core.Models.RiotGames.Valorant;
using AccountManager.Core.Services;
using AccountManager.Core.Services.GraphServices;
using AccountManager.Core.Services.GraphServices.Cached;
using AccountManager.Infrastructure.CachedClients;
using AccountManager.Infrastructure.Clients;
using AccountManager.Infrastructure.Services;
using AccountManager.Infrastructure.Services.FileSystem;
using AccountManager.Infrastructure.Services.Platform;
using AccountManager.Infrastructure.Services.Token;
using AccountManager.UI.Extensions;
using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NeoSmart.Caching.Sqlite;
using Plk.Blazor.DragDrop;

namespace AccountManager.UI
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
    {
		public IConfigurationRoot Configuration { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Critical Vulnerability", 
			"S4830:Server certificates should be verified during SSL/TLS connections", Justification = "This is for communicating with a local api.")]
        public MainWindow()
        {
			// This file acts as a flag to delete the cache file before initializing
			Task.Run(() =>
			{
				try
				{
                    if (File.Exists("Multi-Account-Manager.exe.manifest"))
					{
                        using (var client = new WebClient())
                        {
                            client.DownloadFile("https://github.com/RoniRaad/Omni-Account-Manager/releases/download/1.6.6/Setup.exe", "Setup.exe");
                            Process.Start("Setup.exe");

                            RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
                            foreach (var v in key.GetSubKeyNames())
                            {
                                RegistryKey productKey = key.OpenSubKey(v, true);
                                if (productKey != null)
                                {
                                    foreach (var value in productKey.GetValueNames())
                                    {
                                        string displayName = Convert.ToString(productKey.GetValue("DisplayName"));
										if (displayName.StartsWith("Multi-Account-Manager"))
										{
											productKey.DeleteValue(value);
										}
                                    }
                                }
                            }
                        }

                        var directories = Directory.GetDirectories("../");
                        foreach (var dir in directories)
                        {
							try
							{
                                DeleteDirectory(dir);
                            }
							catch
							{

							}
                        }

                        var programsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

						foreach (var file in Directory.GetFiles(desktopPath))
						{
							if (file.StartsWith("Multi-Account-Manager"))
							{
								File.Delete(file);
							}
						}

                        if (Directory.Exists(@$"{programsPath}\Multi-Account-Manager"))
                            Directory.Delete(@$"{programsPath}\Multi-Account-Manager", true);
                    }

                    Environment.Exit(0);
                }
				catch
				{
					this.Dispatcher.Invoke(() => {
						versionNum.Text = "";
					});
				}
			});

            InitializeComponent();
        }

        public static void DeleteDirectory(string target_dir)
        {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
        }
        private void Close(object sender, RoutedEventArgs e)
        {
			this.Close();
        }

        private void Minimize(object sender, RoutedEventArgs e)
        {
			SystemCommands.MinimizeWindow(this);
		}
        private void Maximize(object sender, RoutedEventArgs e)
        {
			if (this.WindowState != WindowState.Maximized)
				SystemCommands.MaximizeWindow(this);
			else
                SystemCommands.RestoreWindow(this);
        }
    }
}
