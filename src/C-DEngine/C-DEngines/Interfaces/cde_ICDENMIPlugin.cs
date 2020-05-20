// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;

namespace nsCDEngine.Engines
{
    /// <summary>
    /// Required for NMI Runtime Plugin
    /// </summary>
    public interface ICDENMIPlugin
    {
        /// <summary>
        /// Registers an NMI control with the NM Model
        /// </summary>
        void RegisterNMIControls();

        /// <summary>
        /// Called by the NMI Model to render a specific Framework Template
        /// </summary>
        /// <param name="TemplateID"></param>
        void RenderMainFrameTemplate(Guid TemplateID);
    }
}
