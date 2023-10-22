// SPDX-FileCopyrightText: Copyright (c) 2009-2023 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// Interface for a rules engine
    /// </summary>
    public interface ICDERulesEngine
    {
        /// <summary>
        /// Registers a new Rule
        /// </summary>
        /// <param name="pRule"></param>
        bool RegisterRule(TheThingRule pRule);

        /// <summary>
        /// Activates all rules
        /// </summary>
        bool ActivateRules();

        /// <summary>
        /// Gets the Base Thing of a Rule
        /// </summary>
        /// <returns></returns>
        TheThing GetBaseThing();
    }
}
