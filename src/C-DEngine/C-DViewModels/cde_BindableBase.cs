// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using System;
using System.ComponentModel;


namespace nsCDEngine.ViewModels
{
    /// <summary>
    /// Implementation of <see cref="INotifyPropertyChanged"/> to simplify models.
    /// </summary>
    public abstract class TheBindableBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Multicast event for property change notifications.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Checks if a property already matches a desired value.  Sets the property and
        /// notifies listeners only when necessary.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="storage">Reference to a property with both getter and setter.</param>
        /// <param name="value">Desired value for the property.</param>
        /// <param name="cdeT">Type of this property - important for comparison with old value</param>
        /// <param name="cdeE">Extended Flags of the value</param>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers that
        /// support CallerMemberName.</param>
        /// <returns>True if the value was changed, false if the existing value matched the
        /// desired value.</returns>
        protected bool SetProperty<T>(ref T storage, T value, int cdeT,int cdeE, string propertyName = null)
        {
            if ((cdeE & 8) == 0)
            {
                if ((cdeE & 1) != 0)    //Required in case the property is encrypted.
                {
                    if (Equals(storage, value)) return false;
                }
                else
                {
                    switch (cdeT)
                    {
                        case 1:
                            if (Math.Abs(TheCommonUtils.CDbl(storage) - TheCommonUtils.CDbl(value)) < Double.Epsilon
                                && (storage == null && value == null || storage != null && value != null)) // 3.217: treat 0 and null as different
                            {
                                return false;
                            }
                            break;
                        case 2:
                            if (TheCommonUtils.CBool(storage) == TheCommonUtils.CBool(value)
                                && (storage == null && value == null || storage != null && value != null)) // 3.217: treat 0 and null as different
                            {
                                return false;
                            }
                            value = (T)((object)TheCommonUtils.CBool(value));
                            break;
                        case 3:
                            if (storage != null && value != null && TheCommonUtils.CDate(storage) == TheCommonUtils.CDate(value)) return false;
                            break;
                        case 4: //Binary Comparison - Could be very expensive to do Byte[] comparison
                            if ((storage == null && value == null)) return false;
                            break;
                        case 5: //Function - Never Set it!
                            return false;
                        case 6:
                            if (TheCommonUtils.CGuid(storage) == TheCommonUtils.CGuid(value)) return false;
                            break;
                        default:
                            if (Equals(storage, value)) return false;
                            break;
                    }
                }
            }
            storage = value; // CORE REVIEW Markus: SHould we clone the value here to prevent future modification?
            // CODE REVIEW: There is a race condition between setting mHasChanged and delivering the change notification. Should we raise the notification outside or is this mostly obsolete anyway (XAML only)?
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Notifies listeners that a property value has changed.
        /// </summary>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers
        /// that support Databinding</param>
        protected void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
