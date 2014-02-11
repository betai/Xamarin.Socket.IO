using System;
using WebSocket4Net;

namespace Xamarin.Socket.IO
{
	public class SocketIO
	{
		private WebSocket webSocket;

		public SocketIO () : this (@"http://127.0.0.1", 3000)
		{
		}

		public SocketIO (string url, int port)
		{
			webSocket = new WebSocket (string.Format ("{0}:{1}", url, port));
		}
	}
}

