using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;

namespace TCPServer.Tests.Controllers
{
	public class Request
	{
		public string Name
		{
			get; set;
		}
	}
	public class MainController : Controller
	{
		[HttpGet]
		[Route("v1/hello/{hello}")]
		public IActionResult Hello(string hello, string test)
		{
			return Json(hello + test);
		}

		[HttpPost]
		[Route("v1/hellojson")]
		public IActionResult HelloJson([FromBody]Request req)
		{
			return Json(req.Name);
		}

		[HttpGet]
		[Route("v1/badrequest/{hello}")]
		public IActionResult Bad(string hello)
		{
			return BadRequest("boom");
		}
	}
}
