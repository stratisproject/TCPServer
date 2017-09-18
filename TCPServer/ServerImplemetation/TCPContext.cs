using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Http.Features;
using System.Security.Claims;
using System.Threading;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace TCPServer.ServerImplemetation
{
	class TCPContext : HttpContext
	{
		public TCPContext(TCPRequest request,
						IFeatureCollection features,
						ConnectionInfo connectionInfo)
		{
			request.SetHttpContext(this);
			_Features = features;
			_Request = request;
			_Response = new TCPResponse(this);
			_Response.Body = new MemoryStream();
			RequestAborted = cts.Token;
			RequestServices = features.Get<IServiceProvidersFeature>().RequestServices;
			_Connection = connectionInfo;
		}
		IFeatureCollection _Features;
		ConnectionInfo _Connection;
		public override IFeatureCollection Features => _Features;

		HttpRequest _Request;
		public override HttpRequest Request => _Request;

		HttpResponse _Response;
		public override HttpResponse Response => _Response;

		public override ConnectionInfo Connection => _Connection;

		public override WebSocketManager WebSockets => null;

		[Obsolete]
		public override AuthenticationManager Authentication => null;

		public override ClaimsPrincipal User
		{
			get;
			set;
		}
		public override IDictionary<object, object> Items
		{
			get;
			set;
		} = new Dictionary<object, object>();
		public override IServiceProvider RequestServices
		{
			get;
			set;
		}
		public override CancellationToken RequestAborted
		{
			get;
			set;
		}
		public override string TraceIdentifier
		{
			get;
			set;
		}
		public override ISession Session
		{
			get;
			set;
		}

		CancellationTokenSource cts = new CancellationTokenSource();
		public override void Abort()
		{
			cts.Cancel();
		}
	}
}
