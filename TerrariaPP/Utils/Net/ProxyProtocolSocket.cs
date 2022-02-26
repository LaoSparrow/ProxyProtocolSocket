using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Terraria;
using Terraria.Localization;
using Terraria.Net;
using Terraria.Net.Sockets;
using TerrariaPP.Utils.Exts;

namespace TerrariaPP.Utils.Net
{
    public class ProxyProtocolSocket : TcpSocket, ISocket
    {
		private static object _clientJoinLock = new object();

        public ProxyProtocolSocket() : base() { }

		public ProxyProtocolSocket(TcpClient tcpClient) : base(tcpClient) { }

		public ProxyProtocolSocket(TcpClient tcpClient, IPEndPoint remoteEndpoint) : base(tcpClient)
        {
			_remoteAddress = new TcpAddress(remoteEndpoint.Address, remoteEndpoint.Port);
        }

		// Override to prevent unexpected situation
        public void Connect(RemoteAddress address)
        {
            throw new NotImplementedException();
        }

        public bool StartListening(SocketConnectionAccepted callback)
        {
            IPAddress linstenAddr = IPAddress.Any;
            string ipString;
            if (Program.LaunchParameters.TryGetValue("-ip", out ipString) && !IPAddress.TryParse(ipString, out linstenAddr))
            {
                linstenAddr = IPAddress.Any;
            }
            this._isListening = true;
            this._listenerCallback = callback;
            if (this._listener == null)
            {
                this._listener = new TcpListener(linstenAddr, Netplay.ListenPort);
            }
            try
            {
                this._listener.Start();
            }
            catch (Exception)
            {
                return false;
            }
            new Thread(new ThreadStart(this.ListenLoop))
            {
                IsBackground = true,
                Name = "Proxy Protocol Listen Thread"
            }.Start();
            return true;
        }

        public new void ListenLoop()
		{
			while (_isListening && !Netplay.Disconnect)
			{
				try
				{
					TcpClient acceptedClient = _listener.AcceptTcpClient();
					Task.Run(async () =>
					{
						// Make a reference copy, (IDK if it is nessary or not, due to some reason, I keep this)
						TcpClient client = acceptedClient;
                        try
                        {
							Logger.Log($"Accepted a tcp connection from {client.Client.RemoteEndPoint}");
							NetworkStream ns = client.GetStream();
							Logger.Log($"Wait until recieve any data from {client.Client.RemoteEndPoint}");
							await TaskExt.WaitUntilAsync(() => ns.DataAvailable, timeout: TerrariaPPPlugin.Config.Settings.TimeOut);
							Logger.Log($"Recieved data from {client.Client.RemoteEndPoint}");
							ProxyProtocol pp = new ProxyProtocol(ns, (IPEndPoint)client.Client.RemoteEndPoint);
							Logger.Log($"Checking protocol version of {client.Client.RemoteEndPoint}");
							ProxyProtocolVersion version = await pp.GetVersion();
							if (version == ProxyProtocolVersion.UNKNOWN)
                            {
								Logger.Log($"Rejected unknown proxy protocol version from {client.Client.RemoteEndPoint}", LogLevel.INFO);
								client.Close();
								return;
                            }
							Logger.Log($"Version of {client.Client.RemoteEndPoint} is {version:G}");
							Logger.Log($"Parsing header from {client.Client.RemoteEndPoint}");
							await pp.Parse();
							Logger.Log($"Getting source end point from {client.Client.RemoteEndPoint}");
							IPEndPoint sourceEP = await pp.GetSourceEndpoint();
							ISocket socket = new ProxyProtocolSocket(client, sourceEP);
							lock (_clientJoinLock)
                            {
								Logger.Log($"{sourceEP} connecting through proxy protocol version {version:G}", LogLevel.INFO);
								Console.WriteLine(Language.GetTextValue("Net.ClientConnecting", socket.GetRemoteAddress()));
								_listenerCallback(socket);
							}
                        }
						catch (Exception ex)
                        {
							Logger.Log($"Connection {client.Client.RemoteEndPoint} caused\n{ex}", LogLevel.WARNING);
							client.Close();
                        }
					});
				}
				catch (Exception)
				{
				}
			}
			_listener.Stop();
		}
	}
}
