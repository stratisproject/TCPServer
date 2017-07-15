using Microsoft.AspNetCore.Hosting;
using TCPServer.ServerImplemetation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting.Server;
using System.Net;

namespace TCPServer
{
	public static class Extensions
	{
		public static IWebHostBuilder UseTCPServer(this IWebHostBuilder builder, ServerOptions opts)
		{
			builder.ConfigureServices(services =>
			{
				services.AddSingleton<ServerOptions>(opts);
				services.AddSingleton<IServer, Server>();
			});
			
			return builder;
		}

		internal static Task ConnectAsync(this Socket socket, EndPoint endpoint, CancellationToken cancellationToken)
		{
			var args = new SocketAsyncEventArgs();
			CancellationTokenRegistration registration = default(CancellationTokenRegistration);

			TaskCompletionSource<bool> clientSocket = new TaskCompletionSource<bool>();
			Action processClientSocket = () =>
			{
				try
				{
					registration.Dispose();
				}
				catch { }
				if(cancellationToken.IsCancellationRequested)
					clientSocket.TrySetCanceled(cancellationToken);
				else if(args.SocketError != SocketError.Success)
					clientSocket.TrySetException(new SocketException((int)args.SocketError));
				else
					clientSocket.TrySetResult(true);
			};
			args.RemoteEndPoint = endpoint;
			args.Completed += (s, e) => processClientSocket();
			registration = cancellationToken.Register(() =>
			{
				clientSocket.TrySetCanceled(cancellationToken);
				try
				{
					registration.Dispose();
				}
				catch { }
			});
			cancellationToken.Register(() =>
			{
				clientSocket.TrySetCanceled(cancellationToken);
			});
			if(!socket.ConnectAsync(args))
				processClientSocket();
			return clientSocket.Task;
		}

		internal static Task<Socket> AcceptAsync(this Socket socket, CancellationToken cancellationToken)
		{
			var args = new SocketAsyncEventArgs();
			CancellationTokenRegistration registration = default(CancellationTokenRegistration);
			TaskCompletionSource<Socket> clientSocket = new TaskCompletionSource<Socket>();
			Action processClientSocket = () =>
			{
				try
				{
					registration.Dispose();
				}
				catch { }
				if(cancellationToken.IsCancellationRequested)
					clientSocket.TrySetCanceled(cancellationToken);
				else if(args.SocketError != SocketError.Success)
					clientSocket.TrySetException(new SocketException((int)args.SocketError));
				else
					clientSocket.TrySetResult(args.AcceptSocket);
			};

			args.Completed += (s, e) => processClientSocket();
			registration = cancellationToken.Register(() =>
			{
				clientSocket.TrySetCanceled(cancellationToken);
				try
				{
					registration.Dispose();
				}
				catch { }
			});
			if(!socket.AcceptAsync(args))
				processClientSocket();
			return clientSocket.Task;
		}

		internal static IDisposable AsSafeDisposable(this Socket socket)
		{
			return new SafeSocketDisposable(socket);
		}

		internal static async Task<byte> ReadByteAsync(this Stream stream, CancellationToken cts)
		{
			byte[] b = new byte[1];
			await stream.ReadAsync(b, 0, 1, cts).ConfigureAwait(false);
			return b[0];
		}
	}
}
