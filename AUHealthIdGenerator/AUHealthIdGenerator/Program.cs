using System;
using System.Windows.Forms;

namespace AUHealthIdGenerator
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
        }
    }
}
