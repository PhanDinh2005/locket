using System;
using System.Drawing; // Cần thiết cho màu sắc
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.SignalR.Client;
using Shared;

namespace LocketClient
{
    public class LoginForm : Form
    {
        private TextBox txtPhone, txtPass, txtName;
        // Dùng RoundedButton thay vì Button thường
        private RoundedButton btnLogin, btnRegister;

        // HubConnection để kết nối Server
        public static HubConnection Connection;
        public static User CurrentUser;

        public LoginForm()
        {
            this.Text = "Locket - Login";
            this.Size = new System.Drawing.Size(320, 400); 
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle; // Cố định khung 
            this.MaximizeBox = false;

            // UI Elements
            Label lblPhone = new Label { Text = "Số Điện Thoại:", Top = 30, Left = 30, AutoSize = true };
            txtPhone = new TextBox { Top = 55, Left = 30, Width = 240, Font = new Font("Segoe UI", 10) };

            Label lblPass = new Label { Text = "Mật Khẩu:", Top = 95, Left = 30, AutoSize = true };
            txtPass = new TextBox { Top = 120, Left = 30, Width = 240, PasswordChar = '●', Font = new Font("Segoe UI", 10) };

            Label lblName = new Label { Text = "Họ Tên (Nếu Đăng ký):", Top = 160, Left = 30, AutoSize = true };
            txtName = new TextBox { Top = 185, Left = 30, Width = 240, Font = new Font("Segoe UI", 10) };

            // Nút Đăng Nhập
            btnLogin = new RoundedButton
            {
                Text = "Đăng Nhập",
                Top = 240,
                Left = 30,
                Width = 110,
                Height = 40,
                BackColor = Color.Gold,
                ForeColor = Color.Black
            };

            // Nút Đăng Ký 
            btnRegister = new RoundedButton
            {
                Text = "Đăng Ký",
                Top = 240,
                Left = 160,
                Width = 110,
                Height = 40,
                BackColor = Color.LightGray,
                ForeColor = Color.Black
            };

            this.Controls.AddRange(new Control[] { lblPhone, txtPhone, lblPass, txtPass, lblName, txtName, btnLogin, btnRegister });

            // --- KÍCH HOẠT DARK MODE ---
            // (Yêu cầu phải có file UIHelper.cs mình đã đưa trước đó)
            try { UIStyle.ApplyDarkMode(this); } catch { /* Bỏ qua nếu chưa có UIHelper */ }

            // Logic Kết Nối
            Connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/lockethub")
                .WithAutomaticReconnect()
                .Build();

            btnLogin.Click += async (s, e) => await Login();
            btnRegister.Click += async (s, e) => await Register();

            // Kết nối socket ngay khi mở app
            ConnectToServer();
        }

        private async void ConnectToServer()
        {
            try
            {
                await Connection.StartAsync();
                // Tắt thông báo popup đỡ phiền, chỉ in ra console debug
                System.Diagnostics.Debug.WriteLine("Đã kết nối Server!");
            }
            catch
            {
                MessageBox.Show("Không thể kết nối Server! Hãy kiểm tra lại backend.");
            }
        }

        private async Task Login()
        {
            if (string.IsNullOrEmpty(txtPhone.Text) || string.IsNullOrEmpty(txtPass.Text))
            {
                MessageBox.Show("Vui lòng nhập SĐT và Mật khẩu!");
                return;
            }

            try
            {
                var user = await Connection.InvokeAsync<User>("Login", txtPhone.Text, txtPass.Text);
                if (user != null)
                {
                    CurrentUser = user;
                    this.Hide();
                    new MainForm().ShowDialog(); // Mở màn hình chính
                    this.Close();
                }
                else MessageBox.Show("Sai tài khoản hoặc mật khẩu!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi đăng nhập: " + ex.Message);
            }
        }

        private async Task Register()
        {
            if (string.IsNullOrEmpty(txtPhone.Text) || string.IsNullOrEmpty(txtPass.Text) || string.IsNullOrEmpty(txtName.Text))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ thông tin để đăng ký!");
                return;
            }

            try
            {
                bool success = await Connection.InvokeAsync<bool>("Register", txtPhone.Text, txtPass.Text, txtName.Text);
                MessageBox.Show(success ? "Đăng ký thành công! Hãy đăng nhập ngay." : "SĐT đã tồn tại!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi đăng ký: " + ex.Message);
            }
        }
    }
}