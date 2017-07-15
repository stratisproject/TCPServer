using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;

namespace TCPServer.Tests.Controllers
{
	public class MainController : Controller
	{
		[HttpGet]
		[Route("v1/hello/{hello}")]
		public IActionResult Hello(string hello, string test)
		{
			return Json(hello + test);
		}

		[HttpGet]
		[Route("v1/badrequest/{hello}")]
		public IActionResult Bad(string hello)
		{
			return BadRequest("boom");
		}
	}
}
