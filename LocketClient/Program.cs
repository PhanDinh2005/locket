using System;
using System.Windows.Forms;

namespace LocketClient
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Chạy Form đăng nhập trước
            Application.Run(new LoginForm());
        }
    }
}