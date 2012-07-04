using System;
using Gtk;

namespace FrackinReserve
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Application.Init ();
			FrackinReserve win = new FrackinReserve ();
			win.Show ();
			Application.Run ();
		}
	}
}
