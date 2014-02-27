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
		#region Instance variables

		WebSocket WebSocket;
		Dictionary <string, List <Action <JArray>>> EventHandlers = new Dictionary<string, List <Action <JArray>>> ();
		Timer HeartbeatTimer;

		#pragma warning disable 414
		#pragma warning restore

		// socket.io handshake data
		string SessionID;
		int HeartbeatTime;
		int TimeoutTime;

		#endregion

		#region Constants and enums

		const string socketIOConnectionString = "socket.io/1";
		const string socketIOEncodingPattern = @"^([0-9]):([0-9]+[+]?)?:([^:]*)?(:[^\n]*)?";
		const string socketAckEncodingPattern = @"^([0-9]+)(\+[^\n]*)?";

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

		/// <summary>
		/// Initializes a new instance of the <see cref="Xamarin.Socket.IO.SocketIO"/> class
		/// with localhost:3000
		/// </summary>
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
		/// Occurs when socket receives a message. JObject is in NewtonSoft.Json.Linq
		/// </summary>
		public event Action<object, string> SocketReceivedMessage = delegate {};

		/// <summary>
		/// Occurs when socket receives json. JObject is in NewtonSoft.Json.Linq
		/// </summary>
		public event Action<object, JObject> SocketReceivedJson = delegate {};

		/// <summary>
		/// Occurs when socket receives error.
		/// </summary>
		public event Action<object, string> SocketReceivedError = delegate {};

		/// <summary>
		/// Occurs when socket received acknowledgement.
		/// </summary>
		public event Action<object, int, JArray> SocketReceivedAcknowledgement = delegate {};

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
						SessionID = responseElements[0];
						HeartbeatTime = int.Parse(responseElements [1]) * 1000; // convert heartbeatTime to milliseconds
						TimeoutTime = int.Parse (responseElements [2]) * 1000;

						HeartbeatTimer = new Timer (_ => {
							SendHeartbeat ();
						}, null, HeartbeatTime / 2, HeartbeatTime / 2);

						var websocketScheme = Secure ? "wss" : "ws";
						var websocketUri = string.Format ("{0}://{1}:{2}/{3}/websocket/{4}", websocketScheme, Host, Port, socketIOConnectionString, SessionID);
						WebSocket = new WebSocket (websocketUri);
						AddCallbacksToSocket (ref WebSocket);

						WebSocket.Open ();

						Connecting = false;
						Connected = true;
						return ConnectionStatus.Connected;

					} catch (Exception e) {
						Debug.WriteLine (e.Message);
						Connecting = false;
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

		/// <summary>
		/// Send the string message. Consider using Emit instead
		/// </summary>
		/// <param name="message">Message.</param>
		public void Send (string message)
		{
			if (Connected)
				WebSocket.Send (string.Format ("{0}:::{1}", (int)MessageType.Message, message));
		}


		/// <summary>
		/// Sends the obj as JSon. Consider using Emit instead
		/// </summary>
		/// <param name="obj">Object.</param>
		public void SendJson (object obj)
		{
			if (obj != null) {
				string json = JsonConvert.SerializeObject (obj);
				SendJson (json);
			}
		}

		/// <summary>
		/// Sends the JSon string json. Consider using Emit instead
		/// </summary>
		/// <param name="json">Json.</param>
		public void SendJson (string json)
		{
			if (Connected && !string.IsNullOrEmpty (json))
				WebSocket.Send (string.Format ("{0}:::{1}", (int)MessageType.Json, json));
		}

		/// <summary>
		/// Sends an acknowledgement packet.
		/// </summary>
		/// <param name="messageId">Message identifier.</param>
		/// <param name="data">Data.</param>
		public void SendAcknowledgement (int messageId, IEnumerable data = null)
		{
			var dataToSend = data == null ? "" : "+" + JsonConvert.SerializeObject (data);
			Debug.WriteLine ("Send Ack with data = {0}", dataToSend);
			if (Connected)
				WebSocket.Send (string.Format ("{0}:::{1}{2}", (int)MessageType.Ack, messageId, dataToSend));
		}

		#endregion

		#region Helper functions

		void SendHeartbeat ()
		{
			//TODO use the TimeoutTime

			if (Connected)
				WebSocket.Send (string.Format ("{0}::", (int)MessageType.Heartbeat));
			else
				HeartbeatTimer.Change (0, 0);
		}

		void AddCallbacksToSocket (ref WebSocket socket)
		{
			socket.Opened += SocketOpenedFunction;
			socket.Closed += SocketClosed;
			socket.DataReceived += SocketDataReceivedFunction;
			socket.MessageReceived += SocketMessageReceivedFunction;
		}
			
		void SocketOpenedFunction (object o, EventArgs e)
		{
			Debug.WriteLine ("Socket opened");
		}

		void SocketClosed (object o, EventArgs e)
		{
			Debug.WriteLine ("Socket closed");
			Connected = false;
		}

		void SocketDataReceivedFunction (object o, DataReceivedEventArgs e)
		{
			Debug.WriteLine ("Socket received data");
			Debug.WriteLine (e.Data.ToString ());
		}

		void SocketMessageReceivedFunction (object o, MessageReceivedEventArgs e)
		{
			Debug.WriteLine ("Received Message: {0}", e.Message);

			var match = Regex.Match (e.Message, socketIOEncodingPattern);

			var messageType = (MessageType)int.Parse (match.Groups [1].Value);
			var messageId = match.Groups [2].Value;
			var endPoint = match.Groups [3].Value;
			var	data = match.Groups [4].Value;

			if (!string.IsNullOrEmpty (data))
				data = data.Substring (1); //ignore leading ':'

			JObject jObjData = null;

			switch (messageType) {

			case MessageType.Disconnect:
				Debug.WriteLine ("Disconnected");
				SocketDisconnected (o, endPoint);
				break;

			case MessageType.Connect:
				Debug.WriteLine ("Connected");
				SocketConnected (o, endPoint);
				break;

			case MessageType.Heartbeat:
				Debug.WriteLine ("Heartbeat");
				SendHeartbeat ();
				break;

			case MessageType.Message:
				Debug.WriteLine ("Message = {0}", data);
				SocketReceivedMessage (o, data);
				break;

			case MessageType.Json:
				Debug.WriteLine ("Json = {0}", data);
				if (!string.IsNullOrEmpty (data))
					jObjData = JObject.Parse (data);
				SocketReceivedJson (o, jObjData);
				break;

			case MessageType.Event:
				Debug.WriteLine ("Event");
				string eventName = "";
				if (!string.IsNullOrEmpty (data)) {
					jObjData = JObject.Parse (data);
					eventName = jObjData ["name"].ToString ();
				}

				if (!string.IsNullOrEmpty (eventName) && EventHandlers.ContainsKey (eventName)) {
					var handlers = EventHandlers [eventName];
					foreach (var handler in handlers) {
						if (handler != null) {
							JArray args = null;
							if (jObjData != null)
								args = JArray.Parse (jObjData ["args"].ToString ());
							handler (args);
							Debug.WriteLine ("event: {0} with args: {1}", eventName, args.ToString ());
						}
					}
				}
				break;

			case MessageType.Ack:
				Debug.WriteLine ("Ack");
				if (!string.IsNullOrEmpty (data)) {
					var ackMatch = Regex.Match (data, socketAckEncodingPattern);
					var ackMessageId = int.Parse (ackMatch.Groups [1].Value);
					var ackData = ackMatch.Groups [2].Value;
					if (!string.IsNullOrEmpty (ackData))
						ackData = ackData.Substring (1); //ignore leading '+'
					SocketReceivedAcknowledgement (o, ackMessageId, JArray.Parse (ackData));
				}
				break;

			case MessageType.Error:
				Debug.WriteLine ("Error");
				SocketReceivedError (o, data);
				break;

			case MessageType.Noop:
				Debug.WriteLine ("Noop");
				break;

			default:
				Debug.WriteLine ("Something went wrong here...");
				if (!string.IsNullOrEmpty (data)) {
					jObjData = JObject.Parse (data);
					Debug.WriteLine ("jObjData = {0}", jObjData.ToString ());
				}
				break;
			}

		}
				
		void SendDisconnectMessage (object o, string s)
		{
			Debug.WriteLine ("Send Disconnect Message");
			WebSocket.Send (string.Format ("{0}::", (int)MessageType.Disconnect));
			WebSocket.Close ();
			HeartbeatTimer.Change (0, 0);
			Connected = false;
			SocketConnected -= SendDisconnectMessage;
		}

		void Emit (Message messageObject)
		{
			Debug.WriteLine ("Emit");
			string message = JsonConvert.SerializeObject (messageObject);
			Debug.WriteLine (string.Format ("{0}:::{1}", (int)MessageType.Event, message));
			if (Connected)
				WebSocket.Send (string.Format ("{0}:::{1}", (int)MessageType.Event, message));
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

