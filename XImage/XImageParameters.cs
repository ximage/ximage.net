﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web;

namespace XImage
{
	public class Crops
	{
		public const string NONE = "none";
		public const string FILL = "fill";
		public const string STRETCH = "stretch";
		public const string COLOR = "color";
	}

	public class XImageParameters
	{
		static readonly int MAX_SIZE = ConfigurationManager.AppSettings["XImage.MaxSize"].AsNullableInt() ?? 1000;
		static readonly string[] PARAM_ORDER = { "w", "h", "c", "q", "f" };

		static readonly Dictionary<string, ImageFormat> _supportedFormats = new Dictionary<string, ImageFormat>()
		{
			{ "jpg", ImageFormat.Jpeg },
			{ "gif", ImageFormat.Gif },
			{ "png", ImageFormat.Png },
		};
		static readonly Dictionary<string, ImageFormat> _conentTypeFormats = new Dictionary<string, ImageFormat>()
		{
			{ "image/jpg", ImageFormat.Jpeg },
			{ "image/jpeg", ImageFormat.Jpeg },
			{ "image/gif", ImageFormat.Gif },
			{ "image/png", ImageFormat.Png },
		};

		public int? Width { get; private set; }
		public int? Height { get; private set; }
		public bool AllowUpscaling { get; private set; }
		public string Crop { get; private set; }
		public Color? CropAsColor { get; private set; }
		public int? Quality { get; private set; }
		public bool QualityAsKb { get; private set; }
		public ImageFormat OutputFormat { get; private set; }
		public ImageFormat SourceFormat { get; private set; }

		public bool HasAnyValues
		{
			get
			{
				return
					Width != null ||
					Height != null ||
					Crop != null ||
					Quality != null ||
					OutputFormat != null;
			}
		}

		public XImageParameters(HttpContext httpContext)
		{
			var q = HttpUtility.ParseQueryString(httpContext.Request.Url.Query);

			ParseHelp(httpContext);
			ParseWidthAndHeight(q);
			ParseCrop(q);
			ParseQuality(q);
			ParseFormats(httpContext, q);
			ParseOrder(q);
		}

		void ParseHelp(HttpContext httpContext)
		{
			if (httpContext.Request.RawUrl.EndsWith("?help"))
				throw new ArgumentException(string.Empty);
		}

		void ParseWidthAndHeight(NameValueCollection q)
		{
			bool allowWUpscaling = false, allowHUpscaling = false;
			var w = q["w"];
			if (w != null)
			{
				allowWUpscaling = w.EndsWith("!");
				if (allowWUpscaling == true)
					w = w.Substring(0, w.Length - 1);
				Width = w.AsNullableInt();
				if (Width == null || Width <= 0)
					throw new ArgumentException("Width must be a positive integer.");
				if (Width > MAX_SIZE)
					throw new ArgumentException(string.Format("Cannot request a width larger than the max configured value of {0}.", MAX_SIZE));
			}

			var h = q["h"];
			if (h != null)
			{
				allowHUpscaling = h.EndsWith("!");
				if (allowHUpscaling == true)
					h = h.Substring(0, h.Length - 1);
				Height = h.AsNullableInt();
				if (Height == null || Height <= 0)
					throw new ArgumentException("Height must be a positive integer.");
				if (Height > MAX_SIZE)
					throw new ArgumentException(string.Format("Cannot request a height larger than max configured value of {0}.", MAX_SIZE));
			}

			if (Width != null && Height != null && allowWUpscaling != allowHUpscaling)
				throw new ArgumentException("If upscaling '!' is enabled and both w and h are specified, the '!' must be used on both.  Enforcing this strictly helps optimize cache hit ratios.");
			AllowUpscaling = allowWUpscaling || allowHUpscaling;
		}

		void ParseCrop(NameValueCollection q)
		{
			var c = q["c"];
			if (c != null)
			{
				if (c == Crops.NONE)
					throw new ArgumentException("It isn't necessary to spcify a crop of 'none'.  Just leave it off.  Enforcing this strictly helps optimize cache hit ratios.");
				else if (c == Crops.FILL || c == Crops.STRETCH)
					Crop = c;
				else
				{
					if (c.Length != 6 && (c.Length != 8 || c.StartsWith("ff")))
						throw new ArgumentException("When specifying a color for crop (c), it must be a valid 6 digit hex number (or an 8 digit hex number for translucency).  Enforcing this strictly helps optimize cache hit ratios.");
					try
					{
						CropAsColor = ColorTranslator.FromHtml("#" + c);
						Crop = Crops.COLOR;
						if (c != c.ToLower())
							throw new ArgumentException("When specifying a color for crop (c), do not use capital letters.");
					}
					catch
					{
						throw new ArgumentException("When specifying a color for crop (c), it must be a valid 6 digit hex number (or an 8 digit hex number for translucency).  Enforcing this strictly helps optimize cache hit ratios.");
					}
				}

				if (Crop == null)
					throw new ArgumentException("The only valid options for crop are (none), fill, stretch or a hex color.  See below for more details.");

				if ((Width == null || Height == null) && Crop != null)
					throw new ArgumentException("A cropping mode is only valid when both width and height are specified.");
			}
		}

		void ParseQuality(NameValueCollection q)
		{
			var quality = q["q"];
			if (quality != null)
			{
				QualityAsKb = quality.EndsWith("kb");
				if (QualityAsKb)
					quality = quality.Substring(0, quality.Length - 2);
				Quality = quality.AsNullableInt();
				if (Quality == null)
					throw new ArgumentException("Quality must be an integer between 1 and 100 or a size in kb, e.g. 150kb.");
				if (Quality <= 0)
					throw new ArgumentException("Quality must be an integer between 1 and 100.");
				if (!QualityAsKb && Quality > 100)
					throw new ArgumentException("Quality must be an integer between 1 and 100 unless you are specifing a content size, e.g. 150kb.");
			}
		}

		void ParseFormats(HttpContext httpContext, NameValueCollection q)
		{
			var o = q["o"];
			if (o != null)
			{
				OutputFormat = _supportedFormats.GetValueOrDefault(o);
				if (OutputFormat == null)
					throw new ArgumentException("Output format must be either jpg, gif or png.");
				foreach (var format in _supportedFormats.Keys)
					if (httpContext.Request.Url.AbsolutePath.EndsWith("." + format, StringComparison.OrdinalIgnoreCase) && o == format)
						throw new ArgumentException("If the source image is a {0}, don't specify o={0}.  Enforcing this strictly helps optimize cache hit ratios.", format);
			}
			SourceFormat = _conentTypeFormats.GetValueOrDefault(httpContext.Response.ContentType);
			if (SourceFormat == null)
				throw new ArgumentException("Unrecognized content-type: {0}.", httpContext.Response.ContentType);
		}

		void ParseOrder(NameValueCollection q)
		{
			var requestedOrder = q.AllKeys.Where(k => PARAM_ORDER.Contains(k)).ToArray();
			var correctOrder = PARAM_ORDER.Where(p => requestedOrder.Contains(p)).ToArray();
			if (string.Concat(requestedOrder) != string.Concat(correctOrder))
				throw new ArgumentException("Each parameter is optional.  But they must appear in the order of w, h, c, q, f. Enforcing this strictly helps optimize cache hit ratios.");
		}

		public string GetContentType()
		{
			return "image/" + (OutputFormat ?? SourceFormat).ToString().ToLower();
		}
	}
}