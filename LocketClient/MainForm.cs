using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows.Forms;
using Microsoft.AspNetCore.SignalR.Client;
using Shared;

namespace LocketClient
{
    public class MainForm : Form
    {
        private TabControl tabs;

        // Feed Components
        private FlowLayoutPanel feedPanel;

        // Chat Components (Giao diện Messenger)
        private ListBox listFriends; // Danh sách bạn 
        private FlowLayoutPanel pnlRequests;
        private Panel chatAreaPanel; // Khu vực chat 
        private FlowLayoutPanel messageHistoryPanel; // Nơi hiện tin nhắn
        private TextBox txtChatInput;
        private Label lblChatHeader; // Tên người đang chat cùng

        // Data
        private string currentChatPartnerPhone = null; // Đang chat với ai?
        private Dictionary<string, string> friendNames = new Dictionary<string, string>(); // Cache tên bạn bè (SĐT -> Tên)

        // Camera Components
        private PictureBox picPreview;
        private string tempImagePath = "";

        public MainForm()
        {
            this.Text = $"Locket - {LoginForm.CurrentUser.FullName}";
            this.Size = new Size(550, 800);
            this.StartPosition = FormStartPosition.CenterScreen;


            tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                SizeMode = TabSizeMode.Fixed, // kích thước cố định
                ItemSize = new Size((this.ClientSize.Width / 3) - 2, 40)
            };
            // ---------------------------------------

            // Tab 1: Camera
            TabPage tabCamera = new TabPage("Camera");
            tabCamera.BackColor = Color.FromArgb(24, 24, 24);
            SetupCameraTab(tabCamera);

            // Tab 2: Feed
            TabPage tabFeed = new TabPage("Feed");
            tabFeed.BackColor = Color.FromArgb(24, 24, 24);


            feedPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(24, 24, 24),
                Padding = new Padding(35, 10, 0, 0)
            };
            tabFeed.Controls.Add(feedPanel);

            // Tab 3: Messenger
            TabPage tabChat = new TabPage("Messenger");
            tabChat.BackColor = Color.FromArgb(24, 24, 24);
            SetupMessengerTab(tabChat);

            tabs.TabPages.Add(tabCamera);
            tabs.TabPages.Add(tabFeed);
            tabs.TabPages.Add(tabChat);
            this.Controls.Add(tabs);

            // Sự kiện khi thay đổi kích thước cửa sổ thì Tab cũng tự giãn theo
            this.Resize += (s, e) =>
            {
                if (tabs.TabCount > 0)
                    tabs.ItemSize = new Size((this.ClientSize.Width / tabs.TabCount) - 2, 40);
            };

            try { UIStyle.ApplyDarkMode(this); } catch { }

            RegisterSignalREvents();
            LoadInitialData();
        }

        private async void LoadInitialData()
        {
            // 1. Lấy bài đăng 
            await LoginForm.Connection.InvokeAsync("GetPosts", LoginForm.CurrentUser.PhoneNumber);

            // 2. Tải danh sách bạn bè
            if (LoginForm.CurrentUser.Friends != null)
            {
                UpdateFriendListUI(LoginForm.CurrentUser.Friends);
            }

            // 3. Tải danh sách lời mời kết bạn đang chờ 
            await LoginForm.Connection.InvokeAsync("GetFriendRequests", LoginForm.CurrentUser.PhoneNumber);
        }

        private void RegisterSignalREvents()
        {
            // 1. NHẬN BÀI ĐĂNG MỚI
            LoginForm.Connection.On<Post>("ReceivePost", (post) =>
            {
                this.Invoke((MethodInvoker)(() =>
                {
                    AddPostToFeed(post);
                }));
            });

            // 2. NHẬN DANH SÁCH BẠN BÈ 
            LoginForm.Connection.On<List<string>>("UpdateFriendList", (friends) =>
            {
                this.Invoke((MethodInvoker)(async () =>
                {
                    // Cập nhật danh sách bên trái
                    UpdateFriendListUI(friends);

                    // Gọi Server lấy Feed mới 
                    await LoginForm.Connection.InvokeAsync("GetPosts", LoginForm.CurrentUser.PhoneNumber);

                }));
            });

            // 3. NHẬN DỮ LIỆU LỊCH SỬ FEED 
            LoginForm.Connection.On<List<Post>>("LoadHistoryPosts", (posts) =>
            {
                this.Invoke((MethodInvoker)(() =>
                {

                    feedPanel.Controls.Clear();

                    foreach (var p in posts)
                    {
                        AddPostToFeed(p);
                    }
                }));
            });

            // 4. NHẬN UPDATE LIKE 
            LoginForm.Connection.On<Guid, int, List<string>>("UpdateLike", (id, count, likedBy) =>
            {
                this.Invoke((MethodInvoker)(() => UpdateLikeUI(id, count, likedBy)));
            });

            // 5. NHẬN TIN NHẮN 
            LoginForm.Connection.On<Shared.Message>("ReceiveMessage", (msg) =>
            {
                this.Invoke((MethodInvoker)(() =>
                {
                    // Luôn xử lý chat vào khung
                    ProcessIncomingMessage(msg);

                    if (msg.FromUser != LoginForm.CurrentUser.PhoneNumber)
                    {

                        if (tabs.SelectedTab.Text != "Messenger" || currentChatPartnerPhone != msg.FromUser)
                        {
                            ShowInAppNotification(msg);
                        }
                    }
                }));
            });

            // 6. NHẬN LỆNH XÓA BÀI 
            LoginForm.Connection.On<Guid>("PostDeleted", (deletedId) =>
            {
                this.Invoke((MethodInvoker)(() =>
                {
                    Control[] found = feedPanel.Controls.Find(deletedId.ToString(), false);
                    if (found.Length > 0)
                    {
                        feedPanel.Controls.Remove(found[0]);
                        found[0].Dispose();
                    }
                }));
            });


            // 7. Nhận lời mời Real-time
            LoginForm.Connection.On<string, string>("ReceiveFriendRequest", (phone, name) =>
            {
                this.Invoke((MethodInvoker)(() =>
                {
                    MessageBox.Show($"Bạn nhận được lời mời kết bạn từ {name}!");
                    AddRequestToUI(phone, name);
                }));
            });

            // 8.Load danh sách lời mời 
            LoginForm.Connection.On<List<string>>("LoadFriendRequests", (listRequests) =>
            {
                this.Invoke((MethodInvoker)(async () =>
                {
                    pnlRequests.Controls.Clear();
                    foreach (var phone in listRequests)
                    {
                        // Lấy tên người gửi
                        string name = await LoginForm.Connection.InvokeAsync<string>("GetUserName", phone);
                        AddRequestToUI(phone, name);
                    }
                }));
            });
        }

        // --- GIAO DIỆN MESSENGER  ---
        private void SetupMessengerTab(TabPage tab)
        {
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 220, // nút Đồng ý
                BackColor = Color.FromArgb(24, 24, 24)
            };

            // --- CỘT TRÁI ---
            Panel leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5), BackColor = Color.FromArgb(30, 30, 30) };

            // 1. Nút Thêm Bạn 
            RoundedButton btnAddFriend = new RoundedButton
            {
                Text = "+ Thêm Bạn Mới",
                Height = 40,
                Dock = DockStyle.Top,
                BackColor = Color.SeaGreen,
                ForeColor = Color.White
            };
            btnAddFriend.Click += BtnAddFriend_Click;

            // 2. Panel chứa Lời mời kết bạn 
            pnlRequests = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 0),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(45, 45, 45)
            };

            // 3. Danh sách bạn bè 
            listFriends = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None
            };
            listFriends.SelectedIndexChanged += ListFriends_SelectedIndexChanged;

            // Thêm theo thứ tự ngược lại của Dock 
            leftPanel.Controls.Add(listFriends);
            leftPanel.Controls.Add(pnlRequests);
            leftPanel.Controls.Add(btnAddFriend);

            // --- CỘT PHẢI ---
            chatAreaPanel = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.Black };
            lblChatHeader = new Label
            {
                Text = "...",
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.Gold
            };

            Panel inputArea = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(5), BackColor = Color.FromArgb(30, 30, 30) };
            RoundedButton btnSend = new RoundedButton { Text = "Gửi", Width = 80, Dock = DockStyle.Right, BackColor = Color.Gold };
            txtChatInput = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 12), Multiline = true };
            btnSend.Click += BtnSendChat_Click;
            inputArea.Controls.Add(txtChatInput);
            inputArea.Controls.Add(btnSend);

            messageHistoryPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(10),
                BackColor = Color.Black
            };

            chatAreaPanel.Controls.Add(messageHistoryPanel);
            chatAreaPanel.Controls.Add(inputArea);
            chatAreaPanel.Controls.Add(lblChatHeader);

            split.Panel1.Controls.Add(leftPanel);
            split.Panel2.Controls.Add(chatAreaPanel);
            split.Panel2.Controls.Add(new Label { Text = "👈 Chọn bạn để chat", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Gray, BackColor = Color.Black });

            tab.Controls.Add(split);
        }
        // Hàm vẽ 1 dòng lời mời kết bạn
        private void AddRequestToUI(string phone, string name)
        {
            Panel pnlItem = new Panel
            {
                Width = 200,
                Height = 60,
                BackColor = Color.FromArgb(60, 60, 60),
                Margin = new Padding(5)
            };

            Label lblInfo = new Label
            {
                Text = $"{name}\n({phone})",
                ForeColor = Color.Gold,
                AutoSize = true,
                Location = new Point(5, 5),
                Font = new Font("Segoe UI", 9)
            };

            Button btnAccept = new Button
            {
                Text = "Đồng ý",
                BackColor = Color.Green,
                ForeColor = Color.White,
                Location = new Point(5, 30),
                Size = new Size(190, 25),
                FlatStyle = FlatStyle.Flat
            };
            btnAccept.FlatAppearance.BorderSize = 0;

            btnAccept.Click += async (s, e) =>
            {

                await LoginForm.Connection.InvokeAsync("AcceptFriendRequest", LoginForm.CurrentUser.PhoneNumber, phone);

                pnlRequests.Controls.Remove(pnlItem);
            };

            pnlItem.Controls.Add(lblInfo);
            pnlItem.Controls.Add(btnAccept);
            pnlRequests.Controls.Add(pnlItem);
        }
        // --- LOGIC KẾT BẠN & CHỌN BẠN ---
        private async void BtnAddFriend_Click(object sender, EventArgs e)
        {
            // 1. Hiện hộp thoại nhập SĐT
            string phone = Microsoft.VisualBasic.Interaction.InputBox("Nhập số điện thoại người muốn kết bạn:", "Thêm bạn", "");

            if (!string.IsNullOrEmpty(phone))
            {
                // 2. Kiểm tra không được tự kết bạn với mình
                if (phone == LoginForm.CurrentUser.PhoneNumber)
                {
                    MessageBox.Show("Không thể kết bạn với chính mình!");
                    return;
                }

                // 3. GỌI SERVER: Gửi lời mời
                // Lưu ý: Kiểu trả về bây giờ là <string> chứ không phải <bool>
                string result = await LoginForm.Connection.InvokeAsync<string>("SendFriendRequest", LoginForm.CurrentUser.PhoneNumber, phone);

                // 4. Hiện thông báo trả về từ Server (VD: "Đã gửi lời mời", "Người này không tồn tại"...)
                MessageBox.Show(result);
            }
        }

        private async void UpdateFriendListUI(List<string> friends)
        {
            listFriends.Items.Clear();
            friendNames.Clear();

            foreach (var phone in friends)
            {
                // Gọi Server lấy tên thật của bạn bè để hiển thị
                string name = await LoginForm.Connection.InvokeAsync<string>("GetUserName", phone);
                friendNames[phone] = name;
                listFriends.Items.Add($"{name} ({phone})"); // Hiển thị "Tên (SĐT)"
            }
        }

        private async void ListFriends_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listFriends.SelectedIndex == -1) return;

            // Lấy SĐT từ text hiển thị
            string selectedText = listFriends.SelectedItem.ToString();
            string phone = selectedText.Substring(selectedText.LastIndexOf('(') + 1).Trim(')');

            currentChatPartnerPhone = phone;
            string name = friendNames.ContainsKey(phone) ? friendNames[phone] : phone;

            // Hiển thị khung chat
            chatAreaPanel.Visible = true;
            chatAreaPanel.BringToFront();
            lblChatHeader.Text = $"💬 Đang chat với: {name}";

            // --- CẬP NHẬT QUAN TRỌNG: TẢI LỊCH SỬ TIN NHẮN ---

            // 1. Xóa sạch khung chat cũ
            messageHistoryPanel.Controls.Clear();

            try
            {
                // 2. Gọi Server lấy tin nhắn cũ giữa Mình và Người bạn đó
                var historyMessages = await LoginForm.Connection.InvokeAsync<List<Shared.Message>>(
                    "GetPrivateMessages",
                    LoginForm.CurrentUser.PhoneNumber,
                    currentChatPartnerPhone
                );

                // 3. Vẽ lại từng tin nhắn lên màn hình
                foreach (var msg in historyMessages)
                {
                    ProcessIncomingMessage(msg);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải lịch sử chat: " + ex.Message);
            }
        }

        // --- LOGIC GỬI & NHẬN TIN NHẮN ---
        private async void BtnSendChat_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtChatInput.Text) || currentChatPartnerPhone == null) return;

            var msg = new Shared.Message
            {
                FromUser = LoginForm.CurrentUser.PhoneNumber,
                SenderName = LoginForm.CurrentUser.FullName,
                ToUser = currentChatPartnerPhone,
                Content = txtChatInput.Text
            };

            await LoginForm.Connection.InvokeAsync("SendPrivateMessage", msg);
            txtChatInput.Clear();
        }

        private void ProcessIncomingMessage(Shared.Message msg)
        {
            // Xác định xem đây là tin nhắn của mình hay của bạn
            bool isMyMsg = msg.FromUser == LoginForm.CurrentUser.PhoneNumber;
            bool isPartnerMsg = msg.FromUser == currentChatPartnerPhone;

            // Chỉ hiện nếu là tin nhắn của 2 người đang chat
            if (isMyMsg || isPartnerMsg)
            {
                // 1. Tạo Bong bóng chat (Label)
                Label bubble = new Label
                {
                    Text = isMyMsg ? msg.Content : $"{msg.SenderName}:\n{msg.Content}",
                    AutoSize = true,

                    MaximumSize = new Size(messageHistoryPanel.Width - 100, 0),
                    Padding = new Padding(10),
                    Font = new Font("Segoe UI", 11),
                    ForeColor = isMyMsg ? Color.Black : Color.White,
                    BackColor = isMyMsg ? Color.Gold : Color.FromArgb(60, 60, 60)
                };

                // 2. TẠO HÀNG CHỨA (ROW) - 
                FlowLayoutPanel row = new FlowLayoutPanel();
                row.Width = messageHistoryPanel.ClientSize.Width - 25;
                row.Height = bubble.GetPreferredSize(new Size(bubble.MaximumSize.Width, 0)).Height + 20;
                row.Padding = new Padding(0, 5, 0, 5); // Cách trên dưới chút cho thoáng
                row.FlowDirection = isMyMsg ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

                row.Controls.Add(bubble);
                messageHistoryPanel.Controls.Add(row);
                messageHistoryPanel.ScrollControlIntoView(row);
            }
        }

        // --- FEED & LIKE ---
        private void AddPostToFeed(Post post)
        {
            if (feedPanel.InvokeRequired)
            {
                feedPanel.Invoke(new Action(() => AddPostToFeed(post)));
                return;
            }

            Panel card = new Panel
            {
                Name = post.Id.ToString(),
                Width = 440,
                Height = 580,
                BackColor = Color.FromArgb(35, 35, 35),
                Margin = new Padding(0, 0, 0, 20)
            };

            // 1. HEADER
            Label lblHeader = new Label
            {
                Text = post.AuthorName,
                AutoSize = true,
                Top = 10,
                Left = 10,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.Gold
            };

            // 2. NÚT XÓA
            if (post.AuthorPhone == LoginForm.CurrentUser.PhoneNumber)
            {
                Label btnDelete = new Label
                {
                    Text = "🗑",
                    Font = new Font("Segoe UI", 12),
                    ForeColor = Color.Red,
                    Top = 10,
                    Left = 400,
                    Cursor = Cursors.Hand,
                    AutoSize = true
                };
                btnDelete.Click += async (s, e) =>
                {
                    if (MessageBox.Show("Xóa bài này?", "Xác nhận", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        await LoginForm.Connection.InvokeAsync("DeletePost", post.Id, LoginForm.CurrentUser.PhoneNumber);
                };
                card.Controls.Add(btnDelete);
            }

            // 3. THỜI GIAN
            Label lblTime = new Label
            {
                Text = post.CreatedAt.ToString("HH:mm dd/MM"),
                AutoSize = true,
                Top = 35,
                Left = 10,
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Gray
            };

            // 4. ẢNH
            PictureBox pb = new PictureBox
            {
                Top = 60,
                Left = 10,
                Width = 420,
                Height = 320,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };
            try { if (!string.IsNullOrEmpty(post.ImageUrl)) pb.Load(post.ImageUrl); } catch { }

            // 5. CAPTION
            Label lblCap = new Label { Text = post.Caption, Top = 390, Left = 10, Width = 420, Height = 25, Font = new Font("Segoe UI", 10, FontStyle.Italic), ForeColor = Color.WhiteSmoke };

            // 6. LIKE
            bool isLiked = post.LikedBy != null && post.LikedBy.Contains(LoginForm.CurrentUser.PhoneNumber);
            RoundedButton btnLike = new RoundedButton
            {
                Name = "btnLike",
                Text = $"❤️ {post.LikeCount}",
                Top = 420,
                Left = 10,
                Width = 80,
                Height = 35,
                BackColor = isLiked ? Color.Crimson : Color.Gray,
                ForeColor = Color.White
            };
            btnLike.Click += async (s, e) => await LoginForm.Connection.InvokeAsync("ToggleLike", post.Id, LoginForm.CurrentUser.PhoneNumber);

            // 7. REPLY UI
            TextBox txtReply = new TextBox
            {
                Top = 470,
                Left = 10,
                Width = 320,
                PlaceholderText = $"Nhắn cho {post.AuthorName}...",
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            RoundedButton btnSendReply = new RoundedButton
            {
                Text = "➤",
                Top = 467,
                Left = 340,
                Width = 80,
                Height = 30,
                BackColor = Color.Gold,
                ForeColor = Color.Black,
                BorderRadius = 15
            };

            btnSendReply.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtReply.Text)) return;
                if (post.AuthorPhone == LoginForm.CurrentUser.PhoneNumber) { MessageBox.Show("Không thể tự nhắn!"); return; }

                var msg = new Shared.Message { FromUser = LoginForm.CurrentUser.PhoneNumber, SenderName = LoginForm.CurrentUser.FullName, ToUser = post.AuthorPhone, Content = $"[Replying Story]: {txtReply.Text}" };
                await LoginForm.Connection.InvokeAsync("SendPrivateMessage", msg);
                MessageBox.Show("Đã gửi tin nhắn!");
                txtReply.Clear();
            };

            card.Controls.AddRange(new Control[] { lblHeader, lblTime, pb, lblCap, btnLike, txtReply, btnSendReply });
            feedPanel.Controls.Add(card);
            feedPanel.Controls.SetChildIndex(card, 0);
            feedPanel.Invalidate();
            feedPanel.Update();
        }
        private void UpdateLikeUI(Guid postId, int newCount, List<string> likedBy)
        {
            Control[] found = feedPanel.Controls.Find(postId.ToString(), false);
            if (found.Length > 0)
            {
                Panel card = (Panel)found[0];
                Control[] btns = card.Controls.Find("btnLike", false);
                if (btns.Length > 0)
                {
                    RoundedButton btn = (RoundedButton)btns[0];
                    btn.Text = $"❤️ {newCount}";
                    bool amILiking = likedBy.Contains(LoginForm.CurrentUser.PhoneNumber);
                    btn.BackColor = amILiking ? Color.Crimson : Color.Gray;
                }
            }
        }
        // --- CAMERA ---
        private void SetupCameraTab(TabPage tab)
        {
            RoundedButton btnCapture = new RoundedButton { Text = "📸 Chụp Ảnh", Top = 30, Left = 100, Width = 280, BackColor = Color.White, ForeColor = Color.Black };
            picPreview = new PictureBox { Top = 80, Left = 40, Width = 400, Height = 400, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.Black };
            TextBox txtCaption = new TextBox { Top = 500, Left = 40, Width = 400, PlaceholderText = "Thêm chú thích...", Font = new Font("Segoe UI", 12) };
            RoundedButton btnPost = new RoundedButton { Text = "Gửi Locket 🚀", Top = 550, Left = 100, Width = 280, Height = 50, BackColor = Color.Gold, ForeColor = Color.Black };

            btnCapture.Click += (s, e) =>
            {
                OpenFileDialog ofd = new OpenFileDialog();
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    tempImagePath = ofd.FileName;
                    picPreview.Image = Image.FromFile(tempImagePath);
                }
            };
            btnPost.Click += async (s, e) =>
            {
                if (string.IsNullOrEmpty(tempImagePath)) return;
                string imageUrl = await UploadFile(tempImagePath);
                if (string.IsNullOrEmpty(imageUrl)) return;

                var post = new Post
                {
                    AuthorPhone = LoginForm.CurrentUser.PhoneNumber,
                    AuthorName = LoginForm.CurrentUser.FullName,
                    ImageUrl = imageUrl,
                    Caption = txtCaption.Text
                };
                await LoginForm.Connection.InvokeAsync("UploadPost", post);
                MessageBox.Show("Đã đăng bài thành công!");
                tabs.SelectedIndex = 1;
                txtCaption.Clear();
                picPreview.Image = null;
            };

            tab.Controls.AddRange(new Control[] { btnCapture, picPreview, txtCaption, btnPost });
        }
        // 1. Hàm đọc IP từ file cấu hình
        private string GetServerIp()
        {
            try
            {
                // Tìm file server_ip.txt cùng thư mục với file .exe
                string path = Path.Combine(Application.StartupPath, "server_ip.txt");
                if (File.Exists(path))
                {
                    string ip = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(ip)) return ip;
                }
            }
            catch { }
            return "localhost";
        }

        // 2. Hàm UploadFile đã sửa (Dùng IP động)
        private async Task<string> UploadFile(string filePath)
        {
            using (var client = new HttpClient())
            {
                using (var content = new MultipartFormDataContent())
                {
                    try
                    {
                        var fileStream = File.OpenRead(filePath);
                        content.Add(new StreamContent(fileStream), "file", Path.GetFileName(filePath));

                        // --- [SỬA ĐOẠN NÀY] ---
                        string ip = GetServerIp(); // Lấy IP từ file text
                        string uploadUrl = $"http://{ip}:5000/upload";

                        var response = await client.PostAsync(uploadUrl, content);

                        if (!response.IsSuccessStatusCode) return null;

                        var result = await response.Content.ReadFromJsonAsync<UploadResult>();
                        return result.Url;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi upload ảnh: " + ex.Message);
                        return null;
                    }
                }
            }
        }
        class UploadResult { public string Url { get; set; } }
        // Hàm tạo thông báo trôi nổi góc phải màn hình
        private void ShowInAppNotification(Shared.Message msg)
        {
            // 1. Tạo Panel chứa thông báo
            Panel pnlNotify = new Panel
            {
                Size = new Size(320, 70),
                BackColor = Color.FromArgb(40, 40, 40),
                Location = new Point(this.ClientSize.Width - 330, 10),
                BorderStyle = BorderStyle.FixedSingle
            };

            // 2. Tạo Label tên người gửi
            Label lblName = new Label
            {
                Text = $"📩 Tin nhắn từ {msg.SenderName}",
                ForeColor = Color.Gold,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(10, 5),
                AutoSize = true
            };

            // 3. Tạo Label nội dung tin nhắn 
            string shortContent = msg.Content.Length > 35 ? msg.Content.Substring(0, 35) + "..." : msg.Content;
            Label lblContent = new Label
            {
                Text = shortContent,
                ForeColor = Color.White,
                Location = new Point(10, 30),
                AutoSize = true
            };

            // 4. Thêm Label vào Panel
            pnlNotify.Controls.Add(lblName);
            pnlNotify.Controls.Add(lblContent);

            // 5. Thêm Panel vào Form chính
            this.Controls.Add(pnlNotify);
            pnlNotify.BringToFront();

            // 6. Tự động tắt sau 4 giây
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 4000;
            timer.Tick += (s, e) =>
            {
                this.Controls.Remove(pnlNotify);
                pnlNotify.Dispose();
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }

    }
}