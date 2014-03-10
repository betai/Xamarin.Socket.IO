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
		Dictionary <string, List <Action <JToken>>> EventHandlers = new Dictionary<string, List <Action <JToken>>> ();
		Timer HeartbeatTimer;
		Timer TimeoutTimer;

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
		ConnectionType DefaultConnectionType { get; set; } 

		#endregion


		#region Connection status

		public bool Connected { get; set; }
		public bool Connecting { get; set; }

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
		public SocketIO (string host, int port = 80, bool secure = false, ConnectionType connectionType = ConnectionType.WebSocket)
		{
			//TODO: Regex the host to decrease the sensitivity

			Secure = secure;
			Host = host;
			Port = port;
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
		public event Action<MessageID, string> SocketConnected = delegate {};

		/// <summary>
		/// Occurs when socket fails to connect. The error message is passed in the argument
		/// </summary>
		public event Action<string> SocketFailedToConnect = delegate {};

		/// <summary>
		/// Occurs when socket disconnects. The enpoint is passed in the argument
		/// </summary>
		public event Action<MessageID, string> SocketDisconnected = delegate {};

		/// <summary>
		/// Occurs when socket receives a message. JObject is in NewtonSoft.Json.Linq
		/// </summary>
		public event Action<MessageID, string> SocketReceivedMessage = delegate {};

		/// <summary>
		/// Occurs when socket receives json. JObject is in NewtonSoft.Json.Linq
		/// </summary>
		public event Action<MessageID, JObject> SocketReceivedJson = delegate {};

		/// <summary>
		/// Occurs when socket receives error.
		/// </summary>
		public event Action<MessageID, string> SocketReceivedError = delegate {};

		/// <summary>
		/// Occurs when socket received acknowledgement from int messageId.
		/// </summary>
		public event Action<int, JArray> SocketReceivedAcknowledgement = delegate {};

		/// <summary>
		/// Occurs on timeout.
		/// </summary>
		public event Action TimedOut = delegate {};

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
		/// 
		public async Task<ConnectionStatus> ConnectAsync ()
		{
			return await ConnectAsync ("");
		}


		/// <summary>
		/// Connects to http://host:port/ or https://host:port asynchronously depending on the security parameter passed in the constructor
		/// This method constructs a query string from a Dictionary of queries (i.e. key,value => ?key=value)
		/// See https://github.com/LearnBoost/socket.io-spec#query for more info on query		
		/// </summary>
		/// <returns>The async.</returns>
		/// <param name="queries">Queries.</param>
		public async Task<ConnectionStatus> ConnectAsync (Dictionary<string, string> queries)
		{
			var queryString = "";
			foreach (KeyValuePair<string, string> query in queries)
				queryString += string.Format ("?{0}={1}", query.Key, query.Value);
			return await ConnectAsync (queryString);
		}

		/// <summary>
		/// Connects to http://host:port/ or https://host:port asynchronously depending on the security parameter passed in the constructor
		/// See https://github.com/LearnBoost/socket.io-spec#query for more info on query
		/// </summary>
		/// <returns>The async.</returns>
		/// <param name="query">Query.</param>
		public async Task<ConnectionStatus> ConnectAsync (string query)
		{
			if (!Connected && !Connecting) {
				Connecting = true;

				var responseBody = "";

				using (var client = new HttpClient ()) {

					try {
						var scheme = Secure ? "https" : "http";

						//TODO: add timestamp

						var handshakeUri = string.Format ("{0}://{1}:{2}/{3}/{4}", scheme, Host, Port, socketIOConnectionString, query);
						responseBody = await client.GetStringAsync (handshakeUri);

						var responseElements = responseBody.Split (':');
						SessionID = responseElements[0];
						HeartbeatTime = int.Parse(responseElements [1]) * 1000; // convert heartbeatTime to milliseconds
						TimeoutTime = int.Parse (responseElements [2]) * 1000;

						HeartbeatTimer = new Timer (_ => {
							SendHeartbeat ();
						}, null, HeartbeatTime / 2, HeartbeatTime / 2);

						TimeoutTimer = new Timer (_ => {
							Disconnect ();
							TimedOut ();
						}, null, TimeoutTime, Timeout.Infinite);

						//TODO: allow for long-polling

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
						SocketFailedToConnect (e.Message);
						return ConnectionStatus.NotConnected;
					}

				}

			}
			return ConnectionStatus.Connected; 
		}

		/// <summary>
		/// Only used for multiple sockets. Connects to endpoint.
		/// See https://github.com/LearnBoost/socket.io-spec#1-connect for more info
		/// </summary>
		/// <param name="path">Path.</param>
		/// <param name="query">Query.</param>
		public void ConnectToEndpoint (string path, string query)
		{
			if (string.IsNullOrEmpty (query)) {
				if (query [0] != '?')
					query = "?" + query;
			}

			if (Connected)
				WebSocket.Send (string.Format ("{0}::{1}{2}", (int)MessageType.Connect, path, query));
		}

		/// <summary>
		/// If no endpoint is specified, this disconnects the entire socket by default
		/// </summary>
		public void Disconnect (string endPoint = "")
		{
			if (Connected || Connecting)
				SendDisconnectMessage (null, endPoint); // SendDisconnectMessage in private helper methods below
		}

		/// <summary>
		/// Equivalent to socket.on("name", function (data) { }) in JavaScript. 
		/// Calls <param name="handler">handler</param> when the server emits an event named <param name="name">Name.</param>
		/// </summary>
		/// <param name="name">Name.</param>
		/// <param name="handler">Handler.</param>
		public void On (string name, Action <JToken> handler)
		{
			if (!string.IsNullOrEmpty (name)) {
				if (EventHandlers.ContainsKey (name))
					EventHandlers [name].Add (handler);
				else 
					EventHandlers [name] = new List<Action<JToken>> () { handler };
			}
		}

		/// <summary>
		/// Emit the event named "name" with arguments "args"
		/// "args" can be customized via JSon.Net attributes
		/// </summary>
		/// <param name="name">Name.</param>
		/// <param name="args">Arguments.</param>
		public void Emit (string name, IEnumerable args)
		{
			Emit (name, args, "", "");
		}

		/// <summary>
		/// Emit the event named "name" with arguments "args" to "endpoint"
		/// "args" can be customized via JSon.Net attributes
		/// </summary>
		/// <param name="name">Name.</param>
		/// <param name="args">Arguments.</param>
		/// <param name="endpoint">Endpoint.</param>
		public void Emit (string name, IEnumerable args, string endpoint)
		{
			Emit (name, args, endpoint, "");
		}

		/// <summary>
		/// Emit the event named "name" with arguments "args" to "endpoint" with ackId "messageId"
		/// "args" can be customized via JSon.Net attributes
		/// </summary>
		/// <param name="name">Name.</param>
		/// <param name="args">Arguments.</param>
		/// <param name="endpoint">Endpoint.</param>
		/// <param name="messageId">Message identifier.</param>
		public void Emit (string name, IEnumerable args, string endpoint, string messageId)
		{
			if (!string.IsNullOrEmpty (name))
				EmitMessage (new Message (name, args), endpoint, messageId); // EmitMessage in private helper methods below
			else
				Debug.WriteLine ("Tried to Emit empty name");
		}


		/// <summary>
		/// Sends the string "message". Consider using Emit instead
		/// </summary>
		/// <param name="message">Message.</param>
		public void Send (string message)
		{
			Send (message, "", "");
		}

		/// <summary>
		/// Sends the string "message" to "endpoint". Consider using Emit instead
		/// </summary>
		/// <param name="message">Message.</param>
		/// <param name="endpoint">Endpoint.</param>
		public void Send (string message, string endpoint)
		{
			Send (message, endpoint, "");
		}

		/// <summary>
		/// Sends the string "message" to "endpoint" with ackId "messageId". Consider using Emit instead
		/// </summary>
		/// <param name="message">Message.</param>
		/// <param name="endpoint">Endpoint.</param>
		/// <param name="messageId">Message identifier.</param>
		public void Send (string message, string endpoint, string messageId)
		{
			if (Connected)
				WebSocket.Send (string.Format ("{0}:{1}:{2}:{3}", (int)MessageType.Message, messageId, endpoint, message));
		}

		/// <summary>
		/// Sends the obj as JSon. Consider using Emit instead
		/// </summary>
		/// <param name="obj">Object.</param>
		public void SendJson (object obj, string endpoint = "", string messageId = "")
		{
			if (obj != null) {
				string json = JsonConvert.SerializeObject (obj);
				SendJson (json, endpoint, messageId);
			}
		}

		/// <summary>
		/// Sends the JSon string json. Consider using Emit instead
		/// </summary>
		/// <param name="json">Json.</param>
		public void SendJson (string json, string endpoint = "", string messageId = "")
		{
			Debug.WriteLine ("sending json");
			Debug.WriteLine (string.Format ("{0}:{1}:{2}:{3}", (int)MessageType.Json, messageId, endpoint, json));
			if (Connected && !string.IsNullOrEmpty (json))
				WebSocket.Send (string.Format ("{0}:{1}:{2}:{3}", (int)MessageType.Json, messageId, endpoint, json));
		}

		/// <summary>
		/// Sends an acknowledgement packet.
		/// See https://github.com/LearnBoost/socket.io-spec#6-ack for more info
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

			TimeoutTimer.Change (TimeoutTime, Timeout.Infinite);

			var match = Regex.Match (e.Message, socketIOEncodingPattern);

			var messageType = (MessageType)int.Parse (match.Groups [1].Value);
			var messageId = match.Groups [2].Value;
			var endpoint = match.Groups [3].Value;

			var socketMessageInfo = new MessageID (messageId, endpoint);
			var	data = match.Groups [4].Value;

			if (!string.IsNullOrEmpty (data))
				data = data.Substring (1); //ignore leading ':'

			JObject jObjData = null;

			switch (messageType) {

			case MessageType.Disconnect:
				Debug.WriteLine ("Disconnected");
				SocketDisconnected (socketMessageInfo, endpoint);
				break;

			case MessageType.Connect:
				Debug.WriteLine ("Connected");
				SocketConnected (socketMessageInfo, endpoint);
				break;

			case MessageType.Heartbeat:
				Debug.WriteLine ("Heartbeat");
				SendHeartbeat ();
				break;

			case MessageType.Message:
				Debug.WriteLine ("Message = {0}", data);
				SocketReceivedMessage (socketMessageInfo, data);
				break;

			case MessageType.Json:
				Debug.WriteLine ("Json = {0}", data);
				if (!string.IsNullOrEmpty (data))
					jObjData = JObject.Parse (data);
				SocketReceivedJson (socketMessageInfo, jObjData);
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
							if (jObjData != null && jObjData["args"] != null)
								args = JArray.Parse (jObjData ["args"].ToString ());
							handler (args.First);
							Debug.WriteLine ("event: {0} with args: {1}", eventName, args);
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
					JArray jData = null;

					if (!string.IsNullOrEmpty (ackData)) {
						ackData = ackData.Substring (1); //ignore leading '+'
						jData = JArray.Parse (ackData);
					}
					SocketReceivedAcknowledgement (ackMessageId, jData);
				}
				break;

			case MessageType.Error:
				Debug.WriteLine ("Error");
				SocketReceivedError (socketMessageInfo, data);
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
				
		void SendDisconnectMessage (object o, string endPoint = "")
		{
			Debug.WriteLine ("Send Disconnect Message");
			WebSocket.Send (string.Format ("{0}::{1}", (int)MessageType.Disconnect, endPoint));
			if (string.IsNullOrEmpty (endPoint)) {
				WebSocket.Close ();
				HeartbeatTimer.Change (0, 0);
				Connected = false;
			}
		}

		void EmitMessage (Message messageObject, string endpoint, string messageId)
		{
			Debug.WriteLine ("Emit");
			string message = JsonConvert.SerializeObject (messageObject);
			Debug.WriteLine (string.Format ("{0}:{1}:{2}:{3}", (int)MessageType.Event, messageId, endpoint, message));
			if (Connected)
				WebSocket.Send (string.Format ("{0}:{1}:{2}:{3}", (int)MessageType.Event, messageId, endpoint, message));
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
