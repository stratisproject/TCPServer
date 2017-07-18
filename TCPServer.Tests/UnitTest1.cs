using Microsoft.AspNetCore.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TCPServer;
using TCPServer.Client;
using Xunit;

namespace TCPServer.Tests
{
	public class UnitTest1
	{
		IPEndPoint serverBind = new System.Net.IPEndPoint(IPAddress.Parse("0.0.0.0"), 29472);
		IPEndPoint serverIp = new System.Net.IPEndPoint(IPAddress.Parse("127.0.0.1"), 29472);

		[Fact]
		public void CanTimeout()
		{
			using(var host = CreateHost(new ServerOptions(serverBind) { MaxConnections = 1 }))
			{
				using(var client = new HttpClient(new TCPHttpMessageHandler()))
				{
					client.Timeout = TimeSpan.FromSeconds(5);

					Assert.Throws<TaskCanceledException>(() => client.GetAsync("http://127.0.0.1:29472/v1/timeout").GetAwaiter().GetResult());
				}
			}
		}

		[Fact]
		public void CanSetupServer()
		{
			using(var host = CreateHost(new ServerOptions(serverBind) { MaxConnections = 1 }))
			{
				using(var client = new HttpClient(new TCPHttpMessageHandler()))
				{
					var content = client.GetAsync("http://127.0.0.1:29472/v1/nothing").Result.Content;
					Assert.NotNull(content);

					//DNS
					content = client.GetAsync("http://localhost:29472/v1/nothing").Result.Content;
					Assert.NotNull(content);


					var nico = client.GetAsync("http://127.0.0.1:29472/v1/hello/nico").Result.Content.ReadAsStringAsync().Result;
					Assert.Equal("\"nico\"", nico);
					nico = client.GetAsync("http://127.0.0.1:29472/v1/hello/nico?test=toto").Result.Content.ReadAsStringAsync().Result;
					Assert.Equal("\"nicototo\"", nico);


					var error = Assert.Throws<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:29472/v1/badrequest/nico").Result.EnsureSuccessStatusCode());
					Assert.Contains("400", error.Message);

					nico = client.PostAsync("http://127.0.0.1:29472/v1/hellojson/", new StringContent("{ \"Name\" : \"Nicoo\" }", Encoding.UTF8, "application/json")).Result.Content.ReadAsStringAsync().Result;
					Assert.Equal("\"Nicoo\"", nico);
				}
			}
		}

		[Fact]
		public void CanEvictConnections()
		{
			using(var host = CreateHost(new ServerOptions(serverBind) { MaxConnections = 1 }))
			{
				var client1Handler = new TCPHttpMessageHandler(new ClientOptions() { AutoReconnect = false });
				using(var client1 = new HttpClient(client1Handler))
				{
					var nico = client1.GetAsync("http://127.0.0.1:29472/v1/hello/nico").Result.Content.ReadAsStringAsync().Result;
					using(var client2 = new HttpClient(new TCPHttpMessageHandler()))
					{
						nico = client2.GetAsync("http://127.0.0.1:29472/v1/hello/nico").Result.Content.ReadAsStringAsync().Result;
						Assert.Throws<IOException>(() => client1.GetAsync("http://127.0.0.1:29472/v1/hello/nico").GetAwaiter().GetResult());
						client1Handler.Options.AutoReconnect = true;
						client1.GetAsync("http://127.0.0.1:29472/v1/hello/nico").GetAwaiter().GetResult();
					}
				}
			}
		}

		private IWebHost CreateHost(ServerOptions options)
		{
			var host = Utils.TryUntilMakeIt(() =>
			{
				var h = new WebHostBuilder()
					.UseStartup<Startup>()
					.UseTCPServer(options)
					.Build();
				h.Start();
				return h;
			});
			return host;
		}

		[Fact]
		public void CanSetupServerNoHeader()
		{
			using(var host = CreateHost(new ServerOptions(serverBind) { MaxConnections = 1, IncludeHeaders = false }))
			{
				var client = new HttpClient(new TCPHttpMessageHandler(new ClientOptions() { IncludeHeaders = false }));
				var nico = client.GetAsync("http://127.0.0.1:29472/v1/hello/nico").Result.Content.ReadAsByteArrayAsync().Result;
				Assert.Equal(6, nico.Length);
				nico = client.GetAsync("http://127.0.0.1:29472/v1/hello/nico?test=toto").Result.Content.ReadAsByteArrayAsync().Result;
				Assert.Equal(10, nico.Length);


				var error = Assert.Throws<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:29472/v1/badrequest/nico").Result.EnsureSuccessStatusCode());
				Assert.Contains("400", error.Message);
			}
		}
	}
}
