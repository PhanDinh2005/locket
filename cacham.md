# 📌 Danh sách Function & Chức năng

Tài liệu mô tả các hàm chính trong hệ thống, bao gồm vị trí file, tên hàm và chức năng tương ứng.

---

## 🔹 LocketHub.cs (SignalR Hub)

| Function Name         | Chức năng                                                    |
| --------------------- | ------------------------------------------------------------ |
| `Register`            | Đăng ký tài khoản – lưu user mới vào danh sách               |
| `Login`               | Đăng nhập – kiểm tra SĐT và mật khẩu                         |
| `GetUserName`         | Lấy tên hiển thị của người dùng dựa trên SĐT                 |
| `SendFriendRequest`   | Gửi lời mời kết bạn                                          |
| `AcceptFriendRequest` | Chấp nhận lời mời – chuyển từ danh sách chờ sang bạn bè      |
| `GetFriendRequests`   | Lấy danh sách lời mời kết bạn đang chờ                       |
| `UploadPost`          | Đăng bài viết mới – gửi cho bản thân và bạn bè               |
| `GetPosts`            | Lấy bảng tin (Feed) – lọc bài của mình và bạn bè             |
| `DeletePost`          | Xóa bài viết – kiểm tra chính chủ, xóa và thông báo realtime |
| `ToggleLike`          | Thả tim / bỏ tim – cập nhật số like realtime                 |
| `SendPrivateMessage`  | Gửi tin nhắn riêng (chat 1-1)                                |
| `GetPrivateMessages`  | Lấy lịch sử tin nhắn giữa hai người                          |

---

## 🔹 Program.cs

| Function / API           | Chức năng                                                   |
| ------------------------ | ----------------------------------------------------------- |
| `GetLocalIpAddress`      | Lấy IP mạng LAN – dùng tạo link ảnh xem được trên nhiều máy |
| `app.MapPost("/upload")` | API upload ảnh – nhận file từ client và lưu vào ổ cứng      |

---

## 📌 Ghi chú

- Hệ thống sử dụng **SignalR** để xử lý realtime (chat, like, feed).
- Dữ liệu hiện tại lưu **in-memory (List)**, phục vụ mục đích học tập & demo.
- Client: **WinForms (.NET)**
- Server: **ASP.NET Core + SignalR**

---

## 🔹 LoginForm.cs (Client – WinForms)

| Function Name     | Chức năng                                                |
| ----------------- | -------------------------------------------------------- |
| `GetServerIp`     | Đọc file cấu hình – lấy IP Server từ `server_ip.txt`     |
| `ConnectToServer` | Kết nối tới Server bằng SignalR                          |
| `Login`           | Xử lý nút **Đăng nhập** – gửi SĐT và mật khẩu lên Server |
| `Register`        | Xử lý nút **Đăng ký** – gửi thông tin đăng ký lên Server |

---

## 🔹 MainForm.cs (Client – WinForms)

### 🔸 Khởi tạo & Kết nối

| Function Name           | Chức năng                                                                      |
| ----------------------- | ------------------------------------------------------------------------------ |
| `GetServerIp`           | Đọc file cấu hình – lấy IP Server để dùng cho upload ảnh                       |
| `LoadInitialData`       | Tải dữ liệu ban đầu (gọi 3 chức năng: Feed, Danh sách bạn bè, Lời mời kết bạn) |
| `RegisterSignalREvents` | Đăng ký lắng nghe các sự kiện realtime từ Server (SignalR)                     |

---

### 🔸 Kết bạn & Nhắn tin

| Function Name        | Chức năng                                                  |
| -------------------- | ---------------------------------------------------------- |
| `SetupMessengerTab`  | Vẽ giao diện Chat (chia cột trái/phải, thêm khung lời mời) |
| `BtnAddFriend_Click` | Nút **Thêm bạn** – nhập SĐT và gửi lời mời kết bạn         |
| `AddRequestToUI`     | Vẽ một lời mời kết bạn (hiển thị tên + nút **Đồng ý**)     |
| `UpdateFriendListUI` | Cập nhật danh sách bạn bè (vẽ lại cột bên trái)            |

---

### 🔸 Bảng tin (Feed)

| Function Name    | Chức năng                                                        |
| ---------------- | ---------------------------------------------------------------- |
| `AddPostToFeed`  | Vẽ một bài viết (ảnh, tên người đăng, nút xóa, nút tim, ô reply) |
| `UpdateLikeUI`   | Cập nhật trạng thái Like (đổi màu tim và số lượng realtime)      |
| `UploadFile`     | Upload ảnh – gửi file lên API `/upload` của Server               |
| `SetupCameraTab` | Cấu hình tab Camera (chọn ảnh, nhập caption, đăng bài)           |

---

### 🔸 Chat 1–1

| Function Name                      | Chức năng                                             |
| ---------------------------------- | ----------------------------------------------------- |
| `ListFriends_SelectedIndexChanged` | Chọn bạn để chat – hiển thị khung chat và tải lịch sử |
| `BtnSendChat_Click`                | Nút **Gửi** – gửi tin nhắn tới Server                 |
| `ProcessIncomingMessage`           | Xử lý tin nhắn đến – vẽ bong bóng chat trái/phải      |
| `ShowInAppNotification`            | Hiển thị thông báo trong app khi có tin nhắn mới      |

---
