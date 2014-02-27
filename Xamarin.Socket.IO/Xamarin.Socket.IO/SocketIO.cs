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
	public class SocketIO : IDisposable
	{

		WebSocket WebSocket;
		MessageBroker MessageBroker;
		Dictionary <string, List <Action <JArray>>> EventHandlers = new Dictionary<string, List <Action <JArray>>> ();

		#pragma warning disable 414
		Timer HeartbeatTimer;
		#pragma warning restore


		#region Constants

		const string socketIOConnectionString = "socket.io/1";
		const string socketIOEncodingPattern = @"^([0-9]):([0-9]+[+]?)?:([^:]*)?(:[^\n]*)?";

		enum MessageType {
			Disconnect = 0,
			Connect = 1,
			Heartbeat = 2,
			Message = 3,
			Json = 4,
			Event = 5,
			Ack = 6,
			Error = 7,
			Noop = 8
		}

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
		/// The host must be of the form www.example.com - not http://www.example.com/
		/// Defaults to http over https
		/// </summary>
		/// <param name="host">Host.</param>
		/// <param name="port">Port.</param>
		/// <param name="secure">If set to <c>true</c> secure.</param>
		/// <param name="parameters">Parameters.</param>
		/// <param name="connectionType">Connection type.</param>
		public SocketIO (string host, int port = 80, bool secure = false, List<string> parameters = null, ConnectionType connectionType = ConnectionType.WebSocket)
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
		/// Occurs when socket connects. The enpoint is passed in the argument
		/// </summary>
		public event Action<object, string> SocketConnected = delegate {};

		/// <summary>
		/// Occurs when socket disconnects. The enpoint is passed in the argument
		/// </summary>
		public event Action<object, string> SocketDisconnected = delegate {};

		/// <summary>
		/// Occurs when socket received a message. JObject is in NewtonSoft.Json.Linq
		/// </summary>
		public event Action<object, JObject> SocketReceivedMessage = delegate {};

		/// <summary>
		/// Occurs when socket received json. . JObject is in NewtonSoft.Json.Linq
		/// </summary>
		public event Action<object, JObject> SocketReceivedJson = delegate {};




		#endregion


		#region Public

		/**************
		 * Properties *
		***************/

		public enum ConnectionStatus {
			Connected, NotConnected
		}

		public enum ConnectionType {
			WebSocket, LongPolling
		}



		/***********
		 * Methods * 
		***********/

		/// <summary>
		/// Connects to http://host:port/ or https://host:port asynchronously depending on the security parameter passed in the constructor
		/// </summary>
		/// <returns>ConnectionStatus</returns>
		public async Task<ConnectionStatus> ConnectAsync ()
		{
			if (!Connected && !Connecting) {
				Connecting = true;

				var responseBody = "";

				using (var client = new HttpClient ()) {

					try {
						var scheme = Secure ? "https" : "http";
						var handshakeUri = string.Format ("{0}://{1}:{2}/{3}", scheme, Host, Port, socketIOConnectionString);
						responseBody = await client.GetStringAsync (handshakeUri);

						var responseElements = responseBody.Split (':');
						var sessionID = responseElements[0];
						var heartbeatTime = int.Parse(responseElements [1]) * 1000; // convert heartbeatTime to milliseconds
						var timeoutTime = int.Parse (responseElements [2]) * 1000;

						MessageBroker = new MessageBroker (heartbeatTime, timeoutTime);

						HeartbeatTimer = new Timer (_ => {
							SendHeartbeat ();
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
						Debug.WriteLine (e.Message);
						return ConnectionStatus.NotConnected;
					}

				}

			}
			return ConnectionStatus.Connected; 
		}

		/// <summary>
		/// Disconnect this instance.
		/// </summary>
		public void Disconnect ()
		{
			//TODO: clean up Timer, Websocket, and Message Broker

			if (Connected) {
				SendDisconnectMessage (null, "");
			} else if (Connecting) {
				SocketConnected += SendDisconnectMessage;
			}
		}

		/// <summary>
		/// Equivalent to socket.on("name", function (data) { }) in JavaScript. 
		/// Calls <param name="handler">handler</param> when the server emits an event named <param name="name">Name.</param>
		/// </summary>
		/// <param name="name">Name.</param>
		/// <param name="handler">Handler.</param>
		public void On (string name, Action <JArray> handler)
		{
			if (!string.IsNullOrEmpty (name)) {
				if (EventHandlers.ContainsKey (name))
					EventHandlers [name].Add (handler);
				else 
					EventHandlers [name] = new List<Action<JArray>> () { handler };
			}
		}

		/// <summary>
		/// Emit the event named <param name="name">Name.</param> with args <param name="args">Arguments.</param>.
		/// <param name="args">Arguments.</param> *must* be JsonSerializeable
		/// </summary>
		/// <param name="name">Name.</param>
		/// <param name="args">Arguments.</param>
		public void Emit (string name, IEnumerable args)
		{
			if (!string.IsNullOrEmpty (name))
				Emit (new Message (name, args));
			else
				Debug.WriteLine ("Tried to Emit empty name");
		}
			

		#endregion

		#region Helper functions

		void SendHeartbeat ()
		{
			if (Connected)
				WebSocket.Send (string.Format ("{0}::", (int)MessageType.Heartbeat));
		}

		void AddCallbacksToSocket (ref WebSocket socket)
		{
			socket.Opened += SocketOpenedFunction;
			socket.MessageReceived += SocketMessageReceivedFunction;
		}
			
		// internal
		void SocketOpenedFunction (object o, EventArgs e)
		{
			Debug.WriteLine ("Socket opened");
		}

		void SocketMessageReceivedFunction (object o, MessageReceivedEventArgs e)
		{
			Debug.WriteLine ("Received Message: {0}", e.Message);

			var match = Regex.Match (e.Message, socketIOEncodingPattern);

			var messageType = int.Parse (match.Groups [1].Value);
			var messageId = match.Groups [2].Value;
			var endPoint = match.Groups [3].Value;
			var	data = (match.Groups [4].Value);

			if (!string.IsNullOrEmpty (data))
				data = data.Substring (1); //ignore leading ':'

			JObject jObjData = null;
			if (!string.IsNullOrEmpty (data))
				jObjData = JObject.Parse (data);

			switch (messageType) {

			case (int)MessageType.Disconnect:
				Debug.WriteLine ("Disconnected");
				SocketDisconnected (o, endPoint);
				break;

			case (int)MessageType.Connect:
				Debug.WriteLine ("Connected");
				SocketConnected (o, endPoint);
				break;

			case (int)MessageType.Heartbeat:
				Debug.WriteLine ("Heartbeat");
				SendHeartbeat ();
				break;

			case (int)MessageType.Message:
				Debug.WriteLine ("Message");
				SocketReceivedMessage (o, jObjData); // general message received handler
				break;

			case (int)MessageType.Json:
				Debug.WriteLine ("Json");
				SocketReceivedJson (o, jObjData);
				break;

			case (int)MessageType.Event:
				Debug.WriteLine ("Event");
				string eventName = "";
				if (jObjData != null)
					eventName = jObjData ["name"].ToString ();

				if (!string.IsNullOrEmpty (eventName) && EventHandlers.ContainsKey (eventName)) {
					var handlers = EventHandlers [eventName];
					foreach (var handler in handlers) {
						if (handler != null) {
							var args = JArray.Parse (jObjData ["args"].ToString ());
							handler (args);
						}
					}
				}
				break;

			case (int)MessageType.Ack:
				break;

			case (int)MessageType.Error:
				Debug.WriteLine ("Error");
				break;

			case (int)MessageType.Noop:
				Debug.WriteLine ("Noop");
				break;

			default:
				Debug.WriteLine ("Something went wrong here...");
				if (jObjData != null)
					Debug.WriteLine ("jObj = {0}", jObjData.ToString ());
				break;
			}

		}
				
		void SendDisconnectMessage (object o, string s)
		{
			Debug.WriteLine ("Send Disconnect Message");
			WebSocket.Send (string.Format ("{0}::", (int)MessageType.Disconnect));
			WebSocket.Close ();
			Connected = false;
			SocketConnected -= SendDisconnectMessage;
		}

		//TODO: create enum for message types

		void Emit (Message messageObject)
		{
			Debug.WriteLine ("Emit");
			string message = JsonConvert.SerializeObject (messageObject);
			Debug.WriteLine( string.Format ("{0}:::{1}", (int)MessageType.Event, message));
			if (Connected)
				WebSocket.Send (string.Format ("{0}:::{1}", (int)MessageType.Event, message));
		}

		#endregion

		#region Helper classes

		class Message
		{
			public string name { get; set; }
			public IEnumerable args { get; set; }

			public Message () : this ("", null) {}

			public Message (string Name, IEnumerable Args)
			{
				name = Name;
				args = Args;
			}

		}

		#endregion

		#region IDisposable implementation

		public void Dispose ()
		{
			Disconnect ();
		}

		#endregion
	}
}

