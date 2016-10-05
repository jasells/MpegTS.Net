using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpegTS
{
    public static class AsyncErrorHandler
    {
        public static void HandleException(Exception exception)
        {
            System.Diagnostics.Debug.WriteLine("=== BEGIN EXCEPTION ===");
            PrintException(exception);
            System.Diagnostics.Debug.WriteLine("=== END EXCEPTION ===");
        }

        private static void PrintException(Exception exception)
        {
            if (exception != null)
            {
                System.Diagnostics.Debug.WriteLine("------");
                System.Diagnostics.Debug.WriteLine(exception.Message);
                System.Diagnostics.Debug.WriteLine(exception.StackTrace);
                System.Diagnostics.Debug.WriteLine("------");

                PrintException(exception.InnerException);
            }
        }
    }
}
