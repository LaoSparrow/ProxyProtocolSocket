using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Terraria;
using Terraria.Net.Sockets;
using TerrariaApi.Server;

using TShockAPI;
using TShockAPI.Configuration;
using TShockAPI.Hooks;

using TerrariaPP.Utils.Net;
using TerrariaPP.Utils;

namespace TerrariaPP
{
    [ApiVersion(2, 1)]
    public class TerrariaPPPlugin : TerrariaPlugin
    {
        public override string Name => "TerrariaPP";
        public override string Author => "LaoSparrow";
        public override string Description => "Accept proxy protocol v1 and v2 on Terraria server";
        public override Version Version => new Version(1, 0);

        public static string ConfigFileName = "TerrariaPP.json";
        public static string ConfigFilePath => Path.Combine(TShock.SavePath, ConfigFileName);
        public static ConfigFile<TerrariaPPSettings> Config = new ConfigFile<TerrariaPPSettings>();

        public TerrariaPPPlugin(Main game) : base(game)
        {
            // Must be the last to handle "Hooks.Net.Socket.Create"
            // Otherwise the plugin will not function
            Order = 1000;
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnGameInitialize);
            GeneralHooks.ReloadEvent += OnReload;
            // Override the netplay listening socket
            OTAPI.Hooks.Net.Socket.Create += OnSocketCreate;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnGameInitialize);
                GeneralHooks.ReloadEvent -= OnReload;
                OTAPI.Hooks.Net.Socket.Create -= OnSocketCreate;
            }
            base.Dispose(disposing);
        }

        private void OnGameInitialize(EventArgs args)
        {
            if (!Directory.Exists(TShock.SavePath))
                Directory.CreateDirectory(TShock.SavePath);
            LoadConfig();
        }

        private void OnReload(ReloadEventArgs args) => LoadConfig();

        private ISocket OnSocketCreate()
        {
            Logger.Log("OnSocketCreate called!");
            Logger.Log($"Listening on port {Netplay.ListenPort} through proxy protocol v1 and v2", LogLevel.INFO);
            return new ProxyProtocolSocket();
        }

        private static void LoadConfig()
        {
            Logger.Log("Loading config!");
            bool writeConfig = true;
            if (File.Exists(ConfigFilePath))
                Config.Read(ConfigFilePath, out writeConfig);
            if (writeConfig)
                Config.Write(ConfigFilePath);
        }
    }
}
