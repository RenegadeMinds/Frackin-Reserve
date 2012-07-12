/*
 * Frackin' Reserve - Simulate fractional reserve banking and interest.
 * Copyright (C) 2012, Ryan Smyth
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * */
using System;
using Gtk;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using OpenTK;
using System.Runtime.InteropServices;
using System.IO;

namespace FrackinReserve
{
	public partial class FrackinReserve : Gtk.Window
	{
		public FrackinReserve () : 
				base(Gtk.WindowType.Toplevel)
		{
			this.Build ();
			SetupGui();
		}

		protected void SetupGui ()
		{
			// This is all just stuff to make the UI work sanely. 
			sbInitialDeposit.Adjustment.PageIncrement = 100;

			sbInitialDeposit.Xalign = 1.0f;
			sbReserveRequirement.Xalign = 1.0f;
			sbIterations.Xalign = 1.0f;
			sbBankCanLoan.Xalign = 1.0f;
			sbBankHasLoanedOut.Xalign = 1.0f;
			sbBanksNextLoan.Xalign = 1.0f;
			sbBanksReserves.Xalign = 1.0f;
			sbWhatCustomersThink.Xalign = 1.0f;
			sbTotalInterestAndPrincipal.Xalign = 1.0f;
			sbTotalInterestOnly.Xalign = 1.0f;
			sbInterestPeriods.Xalign = 1.0f;
			sbInterestRate.Xalign = 1.0f;

			lblInitDeposit.Xalign = 1.0f;
			lblReserveRequirement.Xalign = 1.0f;
			lblIterations.Xalign = 1.0f;

			// Make the headings a larger, bold font.
			Pango.FontDescription fd = new Pango.FontDescription ();
			fd.Family = "Sans";

			// The following bit is unreliable. See below.
			OperatingSystem os = Environment.OSVersion;
			PlatformID pid = os.Platform;
			switch (pid) {
			case PlatformID.MacOSX:  // Does not work.
				fd.Size = Convert.ToInt32 (16 * Pango.Scale.PangoScale);
				break;
			case PlatformID.Unix:  // Macs lie and say they are Unix.
				fd.Size = Convert.ToInt32 (12 * Pango.Scale.PangoScale);
				break;
			default: // Windows.
				fd.Size = Convert.ToInt32 (12 * Pango.Scale.PangoScale);
				break;
			}
			// Since macs lie, we need to properly determine if we're on a mac.
			// The following method was taken from a question on StackOverflow.
			if (IsRunningOnMac ()) {
				fd.Size = Convert.ToInt32 (16 * Pango.Scale.PangoScale);
			}

			fd.Weight = Pango.Weight.Bold;

			// Set the font on the headings.
			lblFractionalResults.ModifyFont(fd);
			lblInterestOwedParameters.ModifyFont(fd);
			lblInitParams.ModifyFont(fd);
			lblInterestResults.ModifyFont (fd);

			// Set the default values.
			sbInitialDeposit.Value = 1000;
			sbIterations.Value = 10;
			sbReserveRequirement.Value = 0.1f;
			sbWhatCustomersThink.Value = 0;

			cbxCompounded.Active = 1; // Set it to monthly
			sbInterestPeriods.Value = 10; // Set 10 years
			sbInterestRate.Value = 0.05; // Set 5%

			// Info
			LinkButton lb = new LinkButton("Click here for more information and tutorials.");
			lb.Uri = "http://cynic.me/";
			lb.Xalign = 0.5f;
			fixed1.Add (lb);
			lb.Clicked += CynicLinkClicked;
			lb.Show ();

			// Do the initial population of the calculated spin button values. 
			DoFractionalMath();
		}

		void CynicLinkClicked (object sender, EventArgs e)
		{
			try {
				System.Diagnostics.Process.Start ("http://cynic.me/");
			} catch {
				// There's no browser associated, so it pukes and dies. 
				// Don't really feel like fixing this, so, whatever. Tired... zzz...
			}
		}

		#region Is this a mac?
		// From:
		// http://stackoverflow.com/questions/10138040/how-to-detect-properly-windows-linux-mac-operating-systems
		// https://github.com/jpobst/Pinta/blob/master/Pinta.Core/Managers/SystemManager.cs
		//From Managed.Windows.Forms/XplatUI
		[DllImport ("libc")]
		static extern int uname (IntPtr buf);

		static bool IsRunningOnMac ()
		{
			IntPtr buffer = IntPtr.Zero;
			try {
				buffer = Marshal.AllocHGlobal (8192);
				if (uname (buffer) == 0) {
					string os = Marshal.PtrToStringAnsi (buffer);
					if (os == "Darwin")
						return true;
				}
			} 
			catch { } 
			finally {
				if (buffer != IntPtr.Zero)				
					Marshal.FreeHGlobal (buffer);
			}				
			return false;
		}
		#endregion


		void InitialDepositChanged (object sender, EventArgs e)
		{
			DoFractionalMath();
		}

		protected void OnDeleteEvent (object sender, DeleteEventArgs a)
		{
			Application.Quit ();
			a.RetVal = true;
		}


		/// <summary>
        /// This method does the math for the fractional reserve banking and updates the UI. It also calls the DoInterest() method.
        /// </summary>
        private void DoFractionalMath()
        {
            // This is the fraction of the money that must be held in reserve by the bank. 
            // It is also called the "reserve ratio", "reserve requirement", or "cash reserve ratio".
            // Its maximum value is 1, and its minimum value is "greater than zero", that is,
            // It can be as small as imaginable, but cannot be zero. 
            // i.e. anything divided by zero is undefined, or infinitely infinite, which is mathematically absurd. 
            // Note that some countries actually have the fractional reserve amount set to zero!
            // http://en.wikipedia.org/wiki/Reserve_requirement
            // Those countries include Australia, Canada, New Zealand, and Sweden. 
            decimal fractionalReserveFactor = (decimal)sbReserveRequirement.Value;
            // This is the number of times that the money goes through the cycle. 
            // The first time this happens is the initial deposit into the system. 
            // From the second time on, all adhere to the fractional reserve system. 
            Int32 iterations = Convert.ToInt32(sbIterations.Value);
            // We need to keep track of the last amount that was available to lend out. 
            // For the first iteration, this amount is the intitial deposit. 
            decimal lastAmountToLendAmount = (decimal)sbInitialDeposit.Value;
            
            // We're going to keep track of some running totals.
            // This is the total amount that the bank *can* lend out. 
            // It includes any fund that the bank *has* lent out.
            decimal runningSumOfCanLendOut = 0M;
            // This is the total amount of money of all customers that people believe that they have. 
            // All money in excess of the initial deposit is fictious. 
            decimal runningSumOfCustomerAccounts = 0M;
            // This is the total amount of money that the bank has kept in reserve. 
            // This can never go above the initial deposit. 
            decimal runningSumOfFractionallyReservedFunds = 0M;
            // This is the total amount of money that the bank has lent out. 
            // This amount minus the initial deposit is the amount of fictious money. 
            decimal runningSumOfHasLentOut = 0M;

            // This variable holds the HTML that we'll display. 
            StringBuilder sbHtmlTable = new StringBuilder();

            // We need to loop through the fractional reserve logic for each iteration. 
            // An iteration is basically whenever a person deposits money and another person borrows. 
            // See the graphic at http://cycnic.me for an easy way to understand the DoFractionalMath() method.
            for (int i = 1; i <= iterations; i++)
            {
                // Total up all the customers accounts first.
                runningSumOfCustomerAccounts += lastAmountToLendAmount;
                // Compute the amount of money that the bank must withhold (the deposit minus the fractional reserve)
                decimal tmpFractionalReserveWithheldInBank = lastAmountToLendAmount * fractionalReserveFactor;
                // Total up the amount of funds held in reserve
                runningSumOfFractionallyReservedFunds += tmpFractionalReserveWithheldInBank; //lastAmountToLendAmount * (1 - fractionalReserveFactor);
                // Total up the amount lent out to people
                runningSumOfCanLendOut += lastAmountToLendAmount - tmpFractionalReserveWithheldInBank;

				// Build the HTML
				sbHtmlTable.AppendLine( BuildHtmlRow ( i, 
		                            lastAmountToLendAmount, 
		                            tmpFractionalReserveWithheldInBank, 
		                            runningSumOfCanLendOut,
		                            runningSumOfHasLentOut,
		                            runningSumOfFractionallyReservedFunds,
		                            runningSumOfCustomerAccounts,
		                            sbInterestRate.Value,
				                    sbInterestPeriods.Value )); 

                // If we are at the last iteration, do not do this. 
                if (i < iterations)
                {
                    runningSumOfHasLentOut += lastAmountToLendAmount - tmpFractionalReserveWithheldInBank;
                }

                // This is the magic where fractional reserve banking invents money:
                //   tmpFractionalReserveWithheldInBank is the amount mandated that the bank must withhold in reserve.
                //   It is equal to the amount deposited times the "reserve requirement". 
                //   By removing the reserve requirement from the amount deposited, 
                //     we come up with the amount that is available to lend out. 
                lastAmountToLendAmount = lastAmountToLendAmount - tmpFractionalReserveWithheldInBank;
            }

            // These update the UI. 
            sbWhatCustomersThink.Value = (double)runningSumOfCustomerAccounts;
            sbBanksReserves.Value = (double)runningSumOfFractionallyReservedFunds;
            sbBankCanLoan.Value = (double)runningSumOfCanLendOut;
            sbBankHasLoanedOut.Value = (double)runningSumOfHasLentOut;
            //nudFakeTotal.Value = nudWhatTheBankCanLoanOut.Value + nudWhatTheBankHasInReserve.Value;
            sbBanksNextLoan.Value = (double)(runningSumOfCanLendOut - runningSumOfHasLentOut);
            // Once we've calculated everything for the fractional reserve banking, we can figure out what
            // possible interest could be, and so we run the DoInterest() method. 
			decimal interest = DoInterest((decimal)sbBankHasLoanedOut.Value, (decimal)sbInterestRate.Value, (int)sbInterestPeriods.Value);
			sbTotalInterestOnly.Value = (double)interest;
			sbTotalInterestAndPrincipal.Value = (double)((double)interest + sbBankHasLoanedOut.Value);
            
			Html = sbHtmlTable.ToString ();
        }

		private string Html = string.Empty;

		private string BuildHtmlRow (int     i, 
		                             decimal lastAmountToLendAmount, 
		                             decimal tmpFractionalReserveWithheldInBank, 
		                             decimal runningSumOfCanLendOut,
		                             decimal runningSumOfHasLentOut,
		                             decimal runningSumOfFractionallyReservedFunds,
		                             decimal runningSumOfCustomerAccounts,
		                             double  interestRate,
		                             double  interestPeriods)
		{

		    #region HTML
			// Do the HTML if required:
			StringBuilder sbHtmlTable = new StringBuilder();
			// This creates the rows for the table. 
			sbHtmlTable.AppendLine(
			    string.Format(
			    // This is a formatting string for our rows. It represents 1 row. The values below are substituted into it wherever you
			    // see {#}, where # is some number. 
			    "<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td><td>{8}</td></tr>", 
			    // the iteration #
			    i.ToString(),
			    // amount deposited into bank by customer
			    lastAmountToLendAmount.ToString("###,###,###,##0.00"),
			    // amount held in reserve
			    tmpFractionalReserveWithheldInBank.ToString("###,###,###,##0.00"),
			    // amount currently available to lend out
			    (lastAmountToLendAmount - tmpFractionalReserveWithheldInBank).ToString("###,###,###,##0.00"),
			    // amount that *can* be lent out
			    runningSumOfCanLendOut.ToString("###,###,###,##0.00"),
			    // amount that has been lent out
			    runningSumOfHasLentOut.ToString("###,###,###,##0.00"),
			    // the amount that the bank has available for withdrawl
			    runningSumOfFractionallyReservedFunds.ToString("###,###,###,##0.00"),
			    // the amount that customers believe that they have in the bank
			    runningSumOfCustomerAccounts.ToString("###,###,###,##0.00"),
			    // this is the interest that can never be repaid:
			    // F = P * (1 + r/n)^(n*t) - P
			    // -- This one here is for 5% for 1 year
			    //((Convert.ToDouble(runningSumOfHasLentOut) * Math.Pow(1f + 0.05f / 12f, 1 * 12)) - Convert.ToDouble(runningSumOfHasLentOut)).ToString("###,###,###,##0.00")
			    // This does the running sum of the amount that cannot be repaid. 
			    // It's put in a method to avoid having a big huge mess here that would only make things unreadable. 
			    DoInterest(runningSumOfHasLentOut, Convert.ToDecimal (interestRate), 
			           Convert.ToInt32(interestPeriods)).ToString("###,##0.00")
			     )
			  );
			
			#endregion
			return sbHtmlTable.ToString ();

		}


		protected void OnSbReserveRequirementValueChanged (object sender, EventArgs e)
		{
			DoFractionalMath();
		}		


		protected void OnSbIterationsValueChanged (object sender, EventArgs e)
		{
			DoFractionalMath();
		}

		protected void OnSbInterestPeriodsValueChanged (object sender, EventArgs e)
		{
			DoFractionalMath ();
		}		

		protected void OnSbInterestRateValueChanged (object sender, EventArgs e)
		{
			DoFractionalMath ();
		}		

		protected void OnCbxCompoundedChanged (object sender, EventArgs e)
		{
			DoFractionalMath ();
		}



		/// <summary>
        /// This method does the interest calculation for the running sum of unpayable interest for the HTML output.
        /// </summary>
        private decimal DoInterest (decimal principal, decimal rate, int time)
		{
			// The number of times to calculate the interest on.
			// Set it to montly by default.
			double times = 12;
			times = SetTimes (times);

            // final = principal * (1 + (interest rate / number of times compounded)) ^ (years compounded * number of times compounded)
            // F = P * (1 + r/n)^(n*t)
            // This is (interest rate / number of times compounded).
            double rn = Convert.ToDouble(rate) / times;
            // This is (years compounded * number of times compounded).
            double nt = Convert.ToDouble(time) * times;
            // This is the principal.
            double P = Convert.ToDouble(principal);
            // This is the final value.
            double F = P * Math.Pow(1 + rn, nt);

            // This is only the interest part, i.e. the final value - the principal.
            decimal interestOnly = Convert.ToDecimal(F) - principal;
            // This is the final value. 
            decimal interestAndPrincipal = Convert.ToDecimal(F);

            return interestOnly;
        }

		/// <summary>
		/// Sets the way that interest is compounded.
		/// </summary>
		/// <returns>
		/// The times.
		/// </returns>
		/// <param name='times'>
		/// Times. 
		/// </param>
		private double SetTimes(double times)
		{
			
			// Choose how to compound the interest.
            switch (cbxCompounded.Active) // http://stackoverflow.com/questions/7600933/how-to-get-selected-value-from-mono-gtk-combobox
            {
                case 0:
                    // Annually
                    times = 1;
                    break;
                case 1:
                    // Monthly
                    times = 12;
                    break;
                case 2:
                    // Daily
                    times = 365.25; // include leap years
                    break;
                case 3:
                    // hourly
                    times = 365.25 * 24; // hours in a year
                    break;
                case 4:
                    // By the minute
                    times = 365.25 * 24 * 60; // minutes in a year
                    break;
                case 5:
                    // By the second
                    times = 365.25 * 24 * 60 * 60; // seconds in a year
                    break;
                case 6:
                    // By ticks
                    times = 365.25 * 24 * 60 * 60 * TimeSpan.TicksPerSecond; // ticks in a year
                    break;
                default:
                    // Set the default to monthly:
                    times = 12;
                    break;
            }

			return times;
		}		


		string userAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) 
			+ System.IO.Path.DirectorySeparatorChar.ToString () // This makes the path cross platform.
			+ @"Frackin Reserve"
			+ System.IO.Path.DirectorySeparatorChar.ToString ();  

		protected void OnBtnDoHtmlClicked (object sender, EventArgs e)
		{

		#region Html stuff

            // Bad bad bad - this forces the interest update in the HTML
            // -- it's entirely because of bad programming to keep things together and make them more readable
            DoFractionalMath();

            // This assembles the HTML with the constants below, and the Html variable as it is updated in the DoFractionalMath() method above.
            // But first, sub in the interest for the header...
            string html1Sub = html1;
            string ip = sbInterestPeriods.Value.ToString("###,###");
            string ir = (sbInterestRate.Value * 100).ToString("##0.0");
            html1Sub = html1Sub.Replace("{0}", ip).Replace("{1}", ir); 
            // This assembles it. 
            string result = html1Sub + Html + html2;
            // Display the HTML if needed.

                // Ensure that the folder exists
                if (!Directory.Exists(userAppData))
                {
                    Directory.CreateDirectory(userAppData);
                }

                // If the file exists, delete it.
                if (File.Exists(userAppData + "frackin-reserve-html.html"))
                {
                    File.Delete(userAppData + "frackin-reserve-html.html");
                }
                // Write the HTML file.
                File.WriteAllText(userAppData + "frackin-reserve-html.html", result);
                // Open the HTML file in the default program. This should be an Internet browser. 
                try
                {
                    // This opens the HTML file.
                    System.Diagnostics.Process.Start(userAppData + "frackin-reserve-html.html");
                }
                catch
                {
					ThrowHtmlError(); // Cross platform version of code below.
					/*
	                // If there is a file association error, let the user choose what to do. 
	                DialogResult dr = MessageBox.Show("Your computer does not have the proper file associations to open an HTML file.\r\n\r\nYou can open it manually by double-clicking on the generated HTML file, or fix the file association problem yourself.\r\n\r\nDo you want to try to open it manually now?\r\n\r\n1) Click 'OK' to open an Windows Explorer window and open the file manually.\r\n\r\n2)Click 'Cancel' to close this error message and return to Frackin' Reserve. (You must then fix the file association problem manually to use this feature.)", "ERROR - No HTML File Association", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
	                // If they click OK...
	                if (dr == System.Windows.Forms.DialogResult.OK)
	                {
	                    try
	                    {
	                        // Open Windows Explorer so that they can see the file and open it themselves. 
	                        //System.Diagnostics.Process.Start("explorer.exe", userAppData);
	                    }
	                    catch
	                    {
	                    }
	                }
	                */
                }

		#endregion

		}

		private void ThrowHtmlError()
		{
			MessageDialog md = new MessageDialog(this, 
			                                     DialogFlags.DestroyWithParent,
			                                     MessageType.Error,
			                                     ButtonsType.YesNo,
			                                     "Error opening HTML table.");
			md.Title = "Open folder?";
			md.Text = "An error occurred. It looks like your computer cannot open HTML files automatically. \r\n\r\nDo you want to open the folder with the table so that you can open it manually?\r\n\r\n* File is located in:\r\n" + userAppData;
			int mdResult = md.Run ();
			if (mdResult == (int)ResponseType.Yes)
			{
				OpenFolder();
			}
			md.Destroy ();
		}

		private void OpenFolder ()
		{
			try {
				System.Diagnostics.Process.Start (userAppData);
			} catch {
			}
		}
		
        /// <summary>
        /// This section of icky HTML constants is simply so that the program can run as a stand-alone version.
        /// It would be better to stick it in a resource, but for the sake of non-programmers it is inline here
        /// as simple text so that they can more easily modify it. 
		/// (There does not seem to be any RESX support in GTK#.)
        /// </summary>
        #region Icky, messy HTML fragments as constant strings.
        const string html1 = @"<!DOCTYPE HTML PUBLIC '-//W3C//DTD HTML 4.01 Transitional//EN' 'http://www.w3.org/TR/html4/loose.dtd'>
<html>
 <head>
  <title> Frackin' Reserve - Fractional Reserve Banking Simulation </title>
  <meta name='Generator' content='Ryan Smyth - Frackin Reserve - Fractional Reserve Banking Simulation'>
  <meta name='Author' content='Ryan Smyth'>
  <meta name='Keywords' content='Fractional reserve banking is evil'>
  <meta name='Description' content='Fractional reserve banking is evil'>
<style type='text/css'>
table {
	border-width: 1px;
	border-spacing: 2px;
	border-style: solid;
	border-color: black;
	
	background-color: white;
	-webkit-border-radius: 25px;
	-moz-border-radius: 25px;
	border-radius: 25px;
}
th {
	border-width: 1px;
	padding: 3px;
	border-style: solid;
	border-color: black;
	background-color: white;
	/*-webkit-border-radius: 25px;
	-moz-border-radius: 25px;
	border-radius: 25px;*/
    background-color: #f0f0f0;

}
td {
	border-width: 1px;
	padding: 3px;
	border-style: solid;
	border-color: black;
	background-color: white;
	/*-webkit-border-radius: 25px;
	-moz-border-radius: 25px;
	border-radius: 25px;*/
    text-align: right;
}
table tr:last-child td:first-child {
-moz-border-radius-bottomleft:25px;
-webkit-border-bottom-left-radius:25px;
border-bottom-left-radius:25px}

table tr:last-child td:last-child {
-moz-border-radius-bottomright:25px;
-webkit-border-bottom-right-radius:25px;
border-bottom-right-radius:25px}

  </style>
 </head>
 <body>
<table cellspacing='0'>
<tr><th>Iteration #</th><th>Deposited by<br>Customer</th>
<th>Amount Held <br>in Reserve<br>from Deposit</th>
<th>Amount <br>Currrently <br>Available to <br>Lend Out<br>from Deposit</th>
<th>Total Amount that <br>&quot;Can&quot; be <br>Lent Out</th>
<th>Total Amount that <br>Has Been <br>Lent Out</th>
<th>Total Amount <br>Held in Reserve</th>
<th>Total Amount that <br>Customers Believe <br>They Have</th>
<th>Amount of <br>Interest for <br>{0} year(s) @ {1}%<br>on Loaned Money <br>CAN NEVER <br>BE REPAID! </th></tr>
";
        const string html2 = @"
<tr><td colspan='9' style='text-align: center;'> <b>Fractional Reserve Banking is EVIL.</b> </td></tr>
</table>
 </body>
</html>";
        #endregion


    }




}

