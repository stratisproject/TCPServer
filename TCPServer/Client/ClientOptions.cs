using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace TCPServer.Client
{
	public class ClientOptions
	{
		public bool IncludeHeaders
		{
			get; set;
		} = true;

		public int MaxMessageSize
		{
			get; set;
		} = 1024 * 1024;

		public int MaxArrayLength
		{
			get; set;
		} = 1024 * 1024;

		public ArrayPool<byte> ArrayPool
		{
			get; set;
		} = ArrayPool<byte>.Shared;
		public bool AutoReconnect
		{
			get;
			set;
		} = true;

		public TimeSpan ConnectTimeout
		{
			get; set;
		} = TimeSpan.FromMinutes(1.0);
	}
}
