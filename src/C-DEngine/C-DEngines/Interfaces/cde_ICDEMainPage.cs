// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;

namespace nsCDEngine.Engines.NMIService
{
    /// <summary>
    /// Interface for a hosting viewers main page
    /// </summary>
    public interface ICDEMainPage
    {
        /// <summary>
        /// Show an information Toast on the screen
        /// </summary>
        /// <param name="pMsg"></param>
        /// <param name="pTargetScreen"></param>
        void ShowMessageToast(TSM pMsg, string pTargetScreen);

        /// <summary>
        /// Implement a Screen Transition on Clients Example:
        ///
        ///           if (TheCDEngines.MyNMIService != null)
        ///            {
        ///                if (pTargetScreen == TheCDEngines.MyNMIService.MyNMIModel.MyCurrentScreen || string.IsNullOrEmpty(pTargetScreen)) return;
        ///                if (pTargetScreen.Equals("BACK"))
        ///                    ((Windows.UI.Xaml.Controls.Frame)Window.Current.Content).GoBack();
        ///                else
        ///                    ((Windows.UI.Xaml.Controls.Frame)Window.Current.Content).Navigate(typeof(nsCDEngine.Engines.NMIService.ICDEMainPage), pTargetScreen);
        ///                if (TheNMIScreen.TransitToScreen(pTargetScreen))
        ///                {
        ///                    TheCDEngines.MyNMIService.MyNMIModel.MyLastScreen = TheCDEngines.MyNMIService.MyNMIModel.MyCurrentScreen;
        ///                    TheCDEngines.MyNMIService.MyNMIModel.MyCurrentScreen = pTargetScreen;
        ///                }
        ///            }
        /// </summary>
        /// <param name="pTargetScreen"></param>
        bool TransitToScreen(string pTargetScreen);

        /// <summary>
        /// Implements a transition to the Home Screen of the Station or User. If no user is logged on it should go to the designated Station Home Page
        /// Example:
        ///        if (MyUserManager.LoggedOnUser == null)
        ///        {
        ///            string tScreen = "";
        ///            if (TheBaseAssets.MyServiceHostInfo.StartupEngines.Count > 0)
        ///            {
        ///                IBaseEngine tBase = TheThingRegistry.GetBaseEngine(TheBaseAssets.MyServiceHostInfo.StartupEngines[0]);
        ///                if (tBase != null)
        ///                    tScreen = tBase.GetDashboard();
        ///            }
        ///            if (!string.IsNullOrEmpty(tScreen))
        ///                return TransitToScreen(tScreen.ToString());
        ///            else
        ///                return TransitToScreen(TheCDEngines.MyNMIService.MyNMIModel.MainDashboardScreen.ToString());
        ///        }
        ///        else
        ///        {
        ///            string iTargetHomeScreen = TheBaseAssets.MyApplication.MyUserManager.GetUsersHomeScreen();
        ///            if (string.IsNullOrEmpty(iTargetHomeScreen))
        ///                iTargetHomeScreen = TheCDEngines.MyNMIService.MyNMIModel.MainDashboardScreen.ToString();
        ///            return TransitToScreen(iTargetHomeScreen);
        ///        }
        /// </summary>
        /// <returns>True if transition was successful</returns>
        bool GotoStationHome();

    }
}
