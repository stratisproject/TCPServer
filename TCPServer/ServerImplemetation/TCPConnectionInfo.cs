using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace TCPServer.ServerImplemetation
{
	class TCPConnectionInfo : ConnectionInfo
	{
		
		public TCPConnectionInfo(Socket client, IPEndPoint endpoint)
		{
			RemoteIpAddress = ((IPEndPoint)client.RemoteEndPoint).Address;
			RemotePort = ((IPEndPoint)client.RemoteEndPoint).Port;
			LocalIpAddress = endpoint.Address;
			LocalPort = endpoint.Port;
		}

		public override string Id
		{
			get;
			set;
		}

		public override IPAddress RemoteIpAddress
		{
			get;
			set;
		}
		public override int RemotePort
		{
			get;
			set;
		}
		public override IPAddress LocalIpAddress
		{
			get;
			set;
		}
		public override int LocalPort
		{
			get;
			set;
		}
		public override X509Certificate2 ClientCertificate
		{
			get;
			set;
		}

		public override Task<X509Certificate2> GetClientCertificateAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.FromResult<X509Certificate2>(null);
		}
	}
}
