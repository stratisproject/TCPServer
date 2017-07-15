using System;
using System.Collections.Generic;
using System.Net;
using System.Buffers;
using System.Net.Sockets;
using System.Text;

namespace TCPServer
{
	public class ServerOptions
	{
		public ServerOptions(IPEndPoint endpoint)
		{
			if(endpoint == null)
				throw new ArgumentNullException(nameof(endpoint));
			EndPoint = endpoint;
			AddressFamily = AddressFamily.InterNetworkV6;
			SocketOptions.Add(new SocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false));
			Backlog = 8;
		}

		public List<SocketOption> SocketOptions
		{
			get;
			set;
		} = new List<SocketOption>();

		public AddressFamily AddressFamily
		{
			get; set;
		}

		public IPEndPoint EndPoint
		{
			get;
			private set;
		}
		public int Backlog
		{
			get; set;
		}

		public bool IncludeHeaders
		{
			get; set;
		} = true;

		public int MaxBytesArrayLength
		{
			get; set;
		} = 1024 * 1024;

		public ArrayPool<byte> ArrayPool
		{
			get; set;
		} = ArrayPool<byte>.Shared;

		public Socket CreateSocket()
		{
			var socket = new Socket(AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			foreach(var option in SocketOptions)
			{
				socket.SetSocketOption(option.SocketOptionLevel, option.SocketOptionName, option.Value);
			}
			socket.Bind(EndPoint);
			socket.Listen(Backlog);
			return socket;
		}
	}

	public class SocketOption
	{
		public SocketOption(SocketOptionLevel socketOptionLevel, SocketOptionName socketOptionName, bool value)
		{
			this.SocketOptionLevel = socketOptionLevel;
			this.SocketOptionName = socketOptionName;
			this.Value = value;
		}
		public SocketOptionLevel SocketOptionLevel
		{
			get; set;
		}
		public SocketOptionName SocketOptionName
		{
			get; set;
		}
		public bool Value
		{
			get; set;
		}
	}
}
