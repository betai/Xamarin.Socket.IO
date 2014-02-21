using System;
using WebSocket4Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Collections;

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

		public event Action<object, EventArgs> SocketConnected = delegate {};
		public event Action<object, MessageReceivedEventArgs> SocketReceivedMessage = delegate {};
		public event Action<object, DataReceivedEventArgs> SocketReceivedData = delegate {};

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
						var heartbeatTime = Convert.ToInt32 (responseElements [1]);
						var timeoutTime = Convert.ToInt32 (responseElements [2]);

						Manager = new Manager (heartbeatTime, timeoutTime);

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
			WebSocket.Send (string.Format ("5:::{{\"name\":\"{0}\",\"args\":[{1}]}}", name, "blah"));
			var variable = string.Format ("5:::{{\"name\":\"{0}\",\"args\":[{1}]}}", name, "blah");
			Emit (new Message (name, args));
		}

		/// <summary>
		/// Emit the specified messageObject.
		/// </summary>
		/// <param name="messageObject">Message object.</param>
		void Emit (Message messageObject)
		{
			string message = JsonConvert.SerializeObject (messageObject);
			var blah = string.Format ("5:::{0}", message);
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

		void SocketOpen (object o, EventArgs e)
		{
			SocketConnected (o, e);
		}

		void SocketMessage (object o, MessageReceivedEventArgs e)
		{
			SocketReceivedMessage (o, e);
		}

		void SocketData (object o, DataReceivedEventArgs e)
		{
			SocketReceivedData (o, e);
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

