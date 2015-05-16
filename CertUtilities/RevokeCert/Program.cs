﻿using System;
using System.Runtime.InteropServices;
using CERTADMINLib;
using CERTCLIENTLib;

namespace RevokeCert
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: RevokeCert.exe [CAConfig] [Reason] [SerialNumber]");
                return 2;
            }

            string caConfig = args[0];
            int reason = int.Parse(args[1]);
            string serial = args[2];

            CCertAdmin admin = null;
            try
            {
                admin = new CCertAdmin();
                admin.RevokeCertificate(caConfig, serial, reason, DateTime.Now);

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            finally
            {
                if (admin != null)
                    Marshal.FinalReleaseComObject(admin);
            }
        }
    }
}