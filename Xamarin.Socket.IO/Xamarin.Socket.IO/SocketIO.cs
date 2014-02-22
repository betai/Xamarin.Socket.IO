using System;
using WebSocket4Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace Xamarin.Socket.IO
{
	public class SocketIO
	{

		WebSocket WebSocket;
		Manager Manager;

		#region Constants

		string socketIOConnectionString = "socket.io/1";

		#endregion


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

		public SocketIO () : this ("127.0.0.1", 3000)
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

			JsonConvert.DefaultSettings = () => {
				return new JsonSerializerSettings () {
					StringEscapeHandling = StringEscapeHandling.EscapeNonAscii
				};
			};
		}

		#endregion


		#region Socket Callbacks

		/// <summary>
		/// Occurs when socket connected. The enpoint is passed in the argument
		/// </summary>
		public event Action<object, string> SocketConnected = delegate {};

		/// <summary>
		/// Occurs when socket received a message (socket.emit ('foo', args) on the server). JObject is in NewtonSoft.Json.Linq
		/// </summary>
		public event Action<object, JObject> SocketReceivedMessage = delegate {};

		/// <summary>
		/// Occurs when socket received data
		/// </summary>
		public event Action<object, byte[]> SocketReceivedData = delegate {};

		#endregion


		#region Public

		/*************
		 * Properties 
		*************/

		public enum ConnectionStatus {
			Connected, NotConnected
		}

		public enum ConnectionType {
			WebSocket, LongPolling
		}

		public string ConnectionErrorString;


		/***********
		 * Methods 
		***********/

//		Timer HeartbeatTimer;

		/// <summary>
		/// Connects to http://host:port/ or https://host:port asynchronously depending on the security parameter passed in the constructor
		/// </summary>
		/// <returns>ConnectionStatus</returns>
		public async Task<ConnectionStatus> ConnectAsync ()
		{
			if (!Connected && !Connecting) {
				Connecting = true;

				var scheme = Secure ? "https" : "http";
				var handshakeUri = string.Format ("{0}://{1}:{2}/{3}", scheme, Host, Port, socketIOConnectionString);

				var responseBody = "";

				using (var client = new HttpClient ()) {

					try {
						responseBody = await client.GetStringAsync (handshakeUri);

						var responseElements = responseBody.Split (':');
						var sessionID = responseElements[0];
						var heartbeatTime = int.Parse(responseElements [1]) * 1000; // convert heartbeatTime to milliseconds
						var timeoutTime = int.Parse (responseElements [2]) * 1000;

						Manager = new Manager (heartbeatTime, timeoutTime);

						var HeartbeatTimer = new Timer (_ => {
							SendHeartBeat ();
						}, null, heartbeatTime / 2, heartbeatTime / 2);

						var websocketScheme = Secure ? "wss" : "ws";
						var websocketUri = string.Format ("{0}://{1}:{2}/{3}/websocket/{4}", websocketScheme, Host, Port, socketIOConnectionString, sessionID);
						WebSocket = new WebSocket (websocketUri);
						AddCallbacksToSocket (ref WebSocket);

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
		public void Emit (string name, IEnumerable args)
		{
			Emit (new Message (name, args));
		}

		/// <summary>
		/// Emit the specified messageObject.
		/// </summary>
		/// <param name="messageObject">Message object.</param>
		void Emit (Message messageObject)
		{
			string message = JsonConvert.SerializeObject (messageObject);
			Debug.WriteLine( string.Format ("5:::{0}", message));
			if (Connected)
				WebSocket.Send (string.Format ("5:::{0}", message));
			
		}

		public void SendHeartBeat ()
		{
			if (Connected)
				WebSocket.Send ("2:::");
		}


		#endregion

		#region Helper functions

		void AddCallbacksToSocket (ref WebSocket socket)
		{
			socket.Opened += SocketOpen;
			socket.MessageReceived += SocketMessage;
			socket.DataReceived += SocketData;
		}

		// internal
		void SocketOpen (object o, EventArgs e)
		{
		}

		const string socketIOEncodingPattern = @"^([0-9]):([0-9]+[+]?)?:([^:]*)?(:[^\n]*)?";

		void SocketMessage (object o, MessageReceivedEventArgs e)
		{
			var match = Regex.Match (e.Message, socketIOEncodingPattern);

			var messageType = int.Parse (match.Groups [1].Value);
			var messageId = match.Groups [2].Value;
			var endPoint = match.Groups [3].Value;
			var	data = (match.Groups [4].Value);

			if (data != "")
				data = data.Substring (1); //ignore leading ':'

			switch (messageType) {
			case 1:
				SocketConnected (o, endPoint);
				break;
			case 5:
				JObject jObj = JObject.Parse (data);
				SocketReceivedMessage (o, jObj);
				break;
			}

		}

		void SocketData (object o, DataReceivedEventArgs e)
		{
			SocketReceivedData (o, e.Data);
		}
	
		#endregion

		#region Helper classes

		class Message
		{
			public string name { get; set; }
			public IEnumerable args { get; set; }

			public Message () : this ("", null) {	}

			public Message (string Name, IEnumerable Args)
			{
				name = Name;
				args = Args;
			}

		}

		#endregion
	}
}

