// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;

namespace cdePackager
{


    public class Program
    {
        class ConsoleLogger : ILogger
        {
            public void WriteLine(string text)
            {
                Console.WriteLine(text);
            }
        }
        static int Main(string[] args)
        {
            if (args.Length < 2 || args.Length > 5)
            {
                Console.WriteLine("Usage: <plug-in dll or CDES> <bindirectory> [ <storedirectory> [ <cdePlatform> [ -diag ]]]");
                Console.WriteLine($"{args.Length} Arguments: ");
                foreach(var arg in args)
                {
                    Console.WriteLine(arg);
                }
                return 1;
            }

            bool diag = (args.Length > 4 && args[4].ToLowerInvariant() == "-diag") || Environment.GetEnvironmentVariable("cdePackagerDiag") != null;
            bool bCheckForFileOverwrite = diag || Environment.GetEnvironmentVariable("cdePackagerCheckOverwrite") != null;
            string error;
            return ThePackager.PackagePlugIn(args[0], args[1], args.Length > 2 ? args[2] : null, args.Length > 3 ? args[3] : null, new ConsoleLogger(), diag, bCheckForFileOverwrite, out error);
        }

    }

}
