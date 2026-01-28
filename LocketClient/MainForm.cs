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
        private ListBox listFriends; // Danh sách bạn bên trái
        private Panel chatAreaPanel; // Khu vực chat bên phải
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
            this.Size = new Size(550, 800); // Chỉnh lại size cho vừa vặn điện thoại hơn
            this.StartPosition = FormStartPosition.CenterScreen;

            // --- SỬA ĐOẠN NÀY ĐỂ 3 TAB ĐỀU NHAU ---
            tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                SizeMode = TabSizeMode.Fixed, // Chế độ kích thước cố định
                ItemSize = new Size((this.ClientSize.Width / 3) - 2, 40) // Chia 3 chiều rộng màn hình
            };
            // ---------------------------------------

            // Tab 1: Camera
            TabPage tabCamera = new TabPage("Camera");
            tabCamera.BackColor = Color.FromArgb(24, 24, 24); // Nền tối
            SetupCameraTab(tabCamera);

            // Tab 2: Feed
            TabPage tabFeed = new TabPage("Feed");
            tabFeed.BackColor = Color.FromArgb(24, 24, 24); // Nền tối (XÓA VÙNG TRẮNG)

            // Sửa FlowLayoutPanel để căn giữa
            feedPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(24, 24, 24), // Nền tối
                Padding = new Padding(35, 10, 0, 0) // Căn lề trái 35px để đẩy Feed ra giữa
            };
            tabFeed.Controls.Add(feedPanel);

            // Tab 3: Messenger
            TabPage tabChat = new TabPage("Messenger");
            tabChat.BackColor = Color.FromArgb(24, 24, 24); // Nền tối
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
            // 1. Tải bài đăng cũ
            await LoginForm.Connection.InvokeAsync("GetPosts");

            // 2. Tải danh sách bạn bè (Nếu User đăng nhập đã có bạn)
            if (LoginForm.CurrentUser.Friends != null)
            {
                UpdateFriendListUI(LoginForm.CurrentUser.Friends);
            }
        }

        private void RegisterSignalREvents()
        {
            // Nhận bài đăng mới
            LoginForm.Connection.On<Post>("ReceivePost", (post) => this.Invoke((MethodInvoker)(() => AddPostToFeed(post))));

            // Nhận danh sách bài cũ
            LoginForm.Connection.On<List<Post>>("LoadHistoryPosts", (posts) =>
            {
                this.Invoke((MethodInvoker)(() =>
                {
                    feedPanel.Controls.Clear();
                    foreach (var p in posts) AddPostToFeed(p);
                }));
            });

            // Nhận Like update (kèm danh sách người like để tô màu nút)
            LoginForm.Connection.On<Guid, int, List<string>>("UpdateLike", (id, count, likedBy) =>
            {
                this.Invoke((MethodInvoker)(() => UpdateLikeUI(id, count, likedBy)));
            });

            // Nhận tin nhắn
            LoginForm.Connection.On<Shared.Message>("ReceiveMessage", (msg) =>
            {
                this.Invoke((MethodInvoker)(() => ProcessIncomingMessage(msg)));
            });

            // Cập nhật danh sách bạn bè khi có bạn mới
            LoginForm.Connection.On<List<string>>("UpdateFriendList", (friends) =>
            {
                this.Invoke((MethodInvoker)(() => UpdateFriendListUI(friends)));
            });
        }

        // --- GIAO DIỆN MESSENGER (KIỂU TÁCH BẠN BÈ) ---
        private void SetupMessengerTab(TabPage tab)
        {
            // Chia màn hình làm 2
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 200, // Cột danh sách bạn bè nhỏ lại chút
                BackColor = Color.FromArgb(24, 24, 24) // Màu nền của thanh chia cắt
            };

            // --- CỘT TRÁI: DANH SÁCH BẠN ---
            Panel leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5), BackColor = Color.FromArgb(30, 30, 30) };

            RoundedButton btnAddFriend = new RoundedButton
            {
                Text = "+ Thêm Bạn",
                Height = 40,
                Dock = DockStyle.Top,
                BackColor = Color.SeaGreen,
                ForeColor = Color.White
            };
            btnAddFriend.Click += BtnAddFriend_Click;

            listFriends = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11),
                BackColor = Color.FromArgb(30, 30, 30), // Nền tối
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None
            };
            listFriends.SelectedIndexChanged += ListFriends_SelectedIndexChanged;

            leftPanel.Controls.Add(listFriends);
            leftPanel.Controls.Add(btnAddFriend);

            // --- CỘT PHẢI: KHUNG CHAT ---
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
                BackColor = Color.Black // Nền vùng chat màu đen
            };

            chatAreaPanel.Controls.Add(messageHistoryPanel);
            chatAreaPanel.Controls.Add(inputArea);
            chatAreaPanel.Controls.Add(lblChatHeader);

            split.Panel1.Controls.Add(leftPanel);
            split.Panel2.Controls.Add(chatAreaPanel);

            // Label hướng dẫn khi chưa chọn bạn
            Label lblGuide = new Label
            {
                Text = "👈 Chọn bạn để chat",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray,
                BackColor = Color.Black
            };
            split.Panel2.Controls.Add(lblGuide);

            tab.Controls.Add(split);
        }

        // --- LOGIC KẾT BẠN & CHỌN BẠN ---
        private async void BtnAddFriend_Click(object sender, EventArgs e)
        {
            string phone = Microsoft.VisualBasic.Interaction.InputBox("Nhập số điện thoại người muốn kết bạn:", "Thêm bạn", "");
            if (!string.IsNullOrEmpty(phone))
            {
                if (phone == LoginForm.CurrentUser.PhoneNumber) { MessageBox.Show("Không thể kết bạn với chính mình!"); return; }

                bool success = await LoginForm.Connection.InvokeAsync<bool>("AddFriend", LoginForm.CurrentUser.PhoneNumber, phone);
                if (success) MessageBox.Show("Đã kết bạn thành công!");
                else MessageBox.Show("Người này không tồn tại hoặc đã là bạn bè.");
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

        // Thay thế hàm ListFriends_SelectedIndexChanged cũ bằng hàm này
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
                    // Giới hạn chiều rộng tin nhắn (để nó tự xuống dòng nếu quá dài)
                    MaximumSize = new Size(messageHistoryPanel.Width - 100, 0),
                    Padding = new Padding(10),
                    Font = new Font("Segoe UI", 11),
                    ForeColor = isMyMsg ? Color.Black : Color.White,
                    BackColor = isMyMsg ? Color.Gold : Color.FromArgb(60, 60, 60)
                };

                // 2. TẠO HÀNG CHỨA (ROW) - DÙNG FLOWLAYOUTPANEL ĐỂ CĂN LỀ TỰ ĐỘNG
                FlowLayoutPanel row = new FlowLayoutPanel();
                row.Width = messageHistoryPanel.ClientSize.Width - 25; // Trừ hao thanh cuộn
                                                                       // Tự động tính chiều cao hàng dựa trên chiều cao tin nhắn
                row.Height = bubble.GetPreferredSize(new Size(bubble.MaximumSize.Width, 0)).Height + 20;
                row.Padding = new Padding(0, 5, 0, 5); // Cách trên dưới chút cho thoáng

                // --- KHẮC PHỤC LỖI THẲNG HÀNG TẠI ĐÂY ---
                // Nếu là mình: Xếp từ Phải sang Trái. Nếu là bạn: Trái sang Phải
                row.FlowDirection = isMyMsg ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

                row.Controls.Add(bubble);
                messageHistoryPanel.Controls.Add(row);

                // Tự động cuộn xuống tin nhắn mới nhất
                messageHistoryPanel.ScrollControlIntoView(row);
            }
        }

        // --- FEED & LIKE 1 LẦN ---
        private void AddPostToFeed(Post post)
        {
            // 1. Tăng chiều cao Card lên để chứa đủ ảnh + nút like + ô chat (500 -> 560)
            Panel card = new Panel
            {
                Name = post.Id.ToString(), // Để tìm kiếm khi update like
                Width = 440,
                Height = 560,
                BackColor = Color.FromArgb(35, 35, 35),
                Margin = new Padding(0, 0, 0, 20)
            };

            // Header: Tên + Thời gian
            Label lblHeader = new Label { Text = post.AuthorName, AutoSize = true, Top = 10, Left = 10, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.Gold };
            Label lblTime = new Label { Text = post.CreatedAt.ToString("HH:mm"), AutoSize = true, Top = 12, Left = 380, Font = new Font("Segoe UI", 9), ForeColor = Color.Gray };

            // Ảnh
            PictureBox pb = new PictureBox { Top = 40, Left = 10, Width = 420, Height = 320, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
            try { pb.Load(post.ImageUrl); } catch { }

            // Caption
            Label lblCap = new Label { Text = post.Caption, Top = 370, Left = 10, Width = 420, Height = 25, Font = new Font("Segoe UI", 10, FontStyle.Italic), ForeColor = Color.WhiteSmoke };

            // --- PHẦN LIKE (TOGGLE) ---
            bool isLiked = post.LikedBy.Contains(LoginForm.CurrentUser.PhoneNumber);
            RoundedButton btnLike = new RoundedButton
            {
                Name = "btnLike",
                Text = $"❤️ {post.LikeCount}",
                Top = 400,
                Left = 10,
                Width = 80,
                Height = 35,
                BackColor = isLiked ? Color.Crimson : Color.Gray, // Đỏ nếu đã like, Xám nếu chưa
                ForeColor = Color.White
            };

            btnLike.Click += async (s, e) =>
            {
                // Gọi hàm ToggleLike (Like/Unlike)
                await LoginForm.Connection.InvokeAsync("ToggleLike", post.Id, LoginForm.CurrentUser.PhoneNumber);
            };

            // --- PHẦN NHẮN TIN TRẢ LỜI (REPLY STORY) --- MỚI THÊM LẠI

            // Ô nhập tin nhắn
            TextBox txtReply = new TextBox
            {
                Top = 450,
                Left = 10,
                Width = 320,
                PlaceholderText = $"Nhắn cho {post.AuthorName}...",
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Nút Gửi (Mũi tên)
            RoundedButton btnSendReply = new RoundedButton
            {
                Text = "➤",
                Top = 447,
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

                if (post.AuthorPhone == LoginForm.CurrentUser.PhoneNumber)
                {
                    MessageBox.Show("Không thể tự nhắn tin cho chính mình!");
                    return;
                }

                // Tạo tin nhắn
                var msg = new Shared.Message
                {
                    FromUser = LoginForm.CurrentUser.PhoneNumber,
                    SenderName = LoginForm.CurrentUser.FullName,
                    ToUser = post.AuthorPhone, // Gửi thẳng cho chủ bài viết
                    Content = $"[Replying Story]: {txtReply.Text}" // Đánh dấu là reply story
                };

                // Gửi lên Server
                await LoginForm.Connection.InvokeAsync("SendPrivateMessage", msg);

                MessageBox.Show("Đã gửi tin nhắn!");
                txtReply.Clear();
            };

            // Thêm tất cả vào Card
            card.Controls.AddRange(new Control[] { lblHeader, lblTime, pb, lblCap, btnLike, txtReply, btnSendReply });

            feedPanel.Controls.Add(card);
            feedPanel.Controls.SetChildIndex(card, 0); // Đẩy bài mới lên đầu
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

                    // Kiểm tra xem mình còn trong danh sách like không để đổi màu nút
                    bool amILiking = likedBy.Contains(LoginForm.CurrentUser.PhoneNumber);
                    btn.BackColor = amILiking ? Color.Crimson : Color.Gray;
                }
            }
        }

        // --- CAMERA (Giữ nguyên) ---
        private void SetupCameraTab(TabPage tab)
        {
            // (Copy lại code camera cũ của bạn vào đây, không thay đổi logic)
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
                        var response = await client.PostAsync("http://localhost:5000/upload", content);
                        if (!response.IsSuccessStatusCode) return null;
                        var result = await response.Content.ReadFromJsonAsync<UploadResult>();
                        return result.Url;
                    }
                    catch { return null; }
                }
            }
        }
        class UploadResult { public string Url { get; set; } }
    }
}