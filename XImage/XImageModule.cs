﻿using System;
using System.Diagnostics;
using System.Net;
using System.Web;

namespace XImage
{
	public class XImageModule : IHttpModule
	{
		public void Init(HttpApplication context)
		{
			context.PostRequestHandlerExecute += InterceptImage;
		}

		void InterceptImage(object sender, EventArgs e)
		{
			var app = sender as HttpApplication;

			if (app.Response.StatusCode == 200 && app.Response.ContentType.StartsWith("image/"))
			{
				try
				{
					var stopwatch = Stopwatch.StartNew();

					var xImageParams = new XImageParameters(app.Context);

					if (xImageParams.HasAnyValues)
					{
						app.Response.ContentType = xImageParams.GetContentType();

						var output = app.Response.Filter;
						app.Response.Filter = new InterceptingStream(bufferedStream =>
						{
							var properties = new XImager(xImageParams).CopyTo(bufferedStream, output);

							foreach (var property in properties)
								app.Response.Headers.Add(property.Key, property.Value);

							app.Response.Headers.Add("X-Image-Response-Time", string.Format("{0:N2}ms", 1000D * (double)stopwatch.ElapsedTicks / (double)Stopwatch.Frequency));
						});
					}
				}
				catch (ArgumentException ex)
				{
					EndWithError(app, HttpStatusCode.BadRequest, ex.Message);
				}
			}
		}

		void EndWithError(HttpApplication app, HttpStatusCode statusCode, string message)
		{
			app.Response.ClearHeaders();
			app.Response.ClearContent();
			app.Response.TrySkipIisCustomErrors = true;
			app.Response.StatusCode = (int)statusCode;
			app.Response.ContentType = "text/html";
			app.Response.Write("<pre>" + message + "</pre>");
			app.Response.End();
		}

		public void Dispose()
		{
		}
	}
}
