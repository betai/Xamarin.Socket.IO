using System;
using WebSocket4Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Xamarin.Socket.IO
{
	public class SocketIO
	{

		WebSocket WebSocket;
		Manager Manager;

		#region Connection Params

		bool Secure { get; set; }
		string Host { get; set; }
		int Port { get; set; }
		List<string> Parameters { get; set; }
		ConnectionType DefaultConnectionType { get; set; } 

		#endregion

		#region Connection status

		bool Connected { get; set; }
		bool Connecting { get; set; }

		#endregion

		#region Public properties

		public enum ConnectionStatus {
			Connected, NotConnected
		}

		public enum ConnectionType {
			WebSocket, LongPolling
		}

		public string ConnectionErrorString;

		#endregion

		public SocketIO () : this (@"127.0.0.1", 3000)
		{
		}

		public SocketIO (string host, int port, bool secure = false, List<string> parameters = null, ConnectionType connectionType = ConnectionType.WebSocket)
		{
			Secure = secure;
			Host = host;
			Port = port;
			Parameters = parameters;
			DefaultConnectionType = connectionType;
		}

		string socketIOConnectionString = "socket.io/1";

		public async Task<ConnectionStatus> ConnectAsync ()
		{
			if (!Connected && !Connecting) {
				Connecting = true;

				var scheme = Secure ? @"https" : @"http";
				var handshakeUri = string.Format (@"{0}://{1}:{2}/{3}", scheme, Host, Port, socketIOConnectionString);

				var responseBody = "";

				using (var client = new HttpClient ()) {

					try {
						responseBody = await client.GetStringAsync (handshakeUri);

						var responseElements = responseBody.Split (':');
						var sessionID = responseElements[0];
						var heartbeatTime = Convert.ToInt32 (responseElements [1]);
						var timeoutTime = Convert.ToInt32 (responseElements [2]);

						var websocketScheme = Secure ? @"wss" : @"ws";
						var websocketUri = string.Format (@"{0}://{1}:{2}/{3}/websocket/{4}", websocketScheme, Host, Port, socketIOConnectionString, sessionID);
						
						WebSocket = new WebSocket (websocketUri);
						Manager = new Manager (heartbeatTime, timeoutTime);
						AddCallbacksToWebSocket (ref WebSocket);
						WebSocket.Open ();

						Connecting = false;
						Connected = true;
						return ConnectionStatus.Connected;

					} catch (Exception e) {
						ConnectionErrorString = e.Data.ToString ();
						return ConnectionStatus.NotConnected;
					}

				}

			}
			return ConnectionStatus.Connected; 
		}

		void AddCallbacksToWebSocket (ref WebSocket socket) 
		{
			socket.Opened += (object sender, EventArgs e) => {
			};

			socket.DataReceived += (object sender, DataReceivedEventArgs e) => {
			};
		}
	}
}

