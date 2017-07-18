using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace TCPServer
{
	class SafeSocketDisposable : IDisposable
	{
		private readonly Socket socket;

		public SafeSocketDisposable(Socket socket)
		{
			if(socket == null)
				throw new ArgumentNullException(nameof(socket));
			this.socket = socket;
		}
		public void Dispose()
		{
			try
			{
				socket.Shutdown(SocketShutdown.Both);
			}
			catch
			{
			}
			try
			{
				socket.Dispose();
			}
			catch
			{
			}

		}
	}
}
