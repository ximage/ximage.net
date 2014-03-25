﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Web;
using XImage.Utilities;

namespace XImage.Filter
{
	public class Circle : IFilter
	{
		public string Documentation
		{
			get { return "Applies a mask in the shape of a circle with a diameter of either w or h, whichever is shortest."; }
		}

		public void ProcessImage(XImageRequest request, XImageResponse response)
		{
			var size = response.OutputSize;

			var origin = Point.Empty;
			if (size.Width < size.Height)
				origin.Y = (size.Height - size.Width) / 2;
			else
				origin.X = (size.Width - size.Height) / 2;

			var d = Math.Min(size.Width - 1, size.Height - 1);
			size = new Size(d, d);

			var path = new GraphicsPath();
			path.AddEllipse(new Rectangle(origin, size));

			var opaqueMask = request.Output.ContentType.Contains("jpeg") || request.Output.ContentType.Contains("gif");

			response.OutputImage.ApplyMask(path, Brushes.White, opaqueMask);
		}
	}
}