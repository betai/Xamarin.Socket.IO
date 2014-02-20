using System;
using WebSocket4Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Socket.IO.iOS
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

		#region Constructors

		public SocketIO () : this (@"127.0.0.1", 3000)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Xamarin.Socket.IO.SocketIO"/> class.
		/// Defaults to http over https
		/// </summary>
		/// <param name="host">Host.</param>
		/// <param name="port">Port.</param>
		/// <param name="secure">If set to <c>true</c> secure.</param>
		/// <param name="parameters">Parameters.</param>
		/// <param name="connectionType">Connection type.</param>
		public SocketIO (string host, int port, bool secure = false, List<string> parameters = null, ConnectionType connectionType = ConnectionType.WebSocket)
		{
			Secure = secure;
			Host = host;
			Port = port;
			Parameters = parameters;
			DefaultConnectionType = connectionType;
		}

		#endregion


		#region Public

		/**
		 * Properties 
		**/

		public enum ConnectionStatus {
			Connected, NotConnected
		}

		public enum ConnectionType {
			WebSocket, LongPolling
		}

		public string ConnectionErrorString;


		/**
		 * Methods 
		**/

		string socketIOConnectionString = "socket.io/1";


		/// <summary>
		/// Connects to http://host:port/ or https://host:port asynchronously depending on the security parameter passed in the constructor
		/// </summary>
		/// <returns>ConnectionStatus</returns>
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
//						AddCallbacksToWebSocket (ref WebSocket);
						WebSocket.DataReceived += (object sender, DataReceivedEventArgs e) => {
							DebugReceived = e.Data.ToString ();
							Console.WriteLine ("DataReceived");
						};

						WebSocket.MessageReceived += (object sender, MessageReceivedEventArgs e) => {
							DebugReceived = e.Message;
							Console.WriteLine ("Message Received {0}", e.Message);
						};

						WebSocket.Opened += (object sender, EventArgs e) => {
							DebugOpened = "opened";
							Console.WriteLine ("Opened");
						};
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

		/// <summary>
		/// Emit the event named <param name="name">Name.</param> with args <param name="args">Arguments.</param>.
		/// <param name="args">Arguments.</param> *must* be JsonSerializeable
		/// </summary>
		/// <param name="name">Name.</param>
		/// <param name="args">Arguments.</param>
		public void Emit (string name, object args)
		{
			var json = JsonConvert.SerializeObject (args);
			Emit (name, json);
		}

		/// <summary>
		/// Emit the event name with Json formatted string as arguments.
		/// </summary>
		/// <param name="name">Name.</param>
		/// <param name="jsonArgs">Json arguments.</param>
		public void Emit (string name, string jsonArgs)
		{
			//remove public
			WebSocket.Send ("5:::{\"name\":\"news\",\"args\":[{\"hello\":\"world\"}]}");
//			WebSocket.Send (string.Format(@"5:::{""name"":""{0}"",""args"":[{1}]}", name, jsonArgs));

		}

		public void SendHeartBeat ()
		{
			WebSocket.Send ("2:::");
		}


		#endregion

		#region Helper functions

		public string DebugOpened;
		public string DebugReceived;

		void AddCallbacksToWebSocket (ref WebSocket socket) 
		{
			socket.Opened += (object sender, EventArgs e) => {
				DebugOpened = "opened";
			};

			socket.DataReceived += delegate {
				DebugReceived = "blah";
			};
		}

		#endregion
	}
}

