﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using XImage.Crops;

namespace XImage.ShopSavvy.Legacy.Crops
{
	[Obsolete("Use Zoom instead.")]
	public class Fit : Zoom
	{
		public Fit() : base() { }

		public Fit(int color) : base(color) { }

		public Fit(string color) : base(color) { }
	}
}