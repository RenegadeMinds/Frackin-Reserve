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
// It's all in here. And working.
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



	}
}

