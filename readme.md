# BÁO CÁO ĐỒ ÁN LẬP TRÌNH MẠNG

## ĐỀ TÀI: XÂY DỰNG ỨNG DỤNG MẠNG XÃ HỘI CHIA SẺ ẢNH (LOCKET CLONE)

---

## MỤC LỤC

1. Tổng quan đề tài
2. Công nghệ sử dụng
3. Kiến trúc hệ thống
4. Danh sách chức năng chi tiết
5. Giải pháp kỹ thuật & xử lý vấn đề
6. Kết luận

---

## I. TỔNG QUAN ĐỀ TÀI

Đồ án tập trung xây dựng một ứng dụng mạng xã hội mô phỏng theo **Locket Widget**, cho phép người dùng chia sẻ nhanh các khoảnh khắc hằng ngày dưới dạng ảnh với nhóm bạn bè thân thiết.

Hệ thống được thiết kế theo mô hình **Client – Server**, hoạt động trong mạng LAN, hỗ trợ các chức năng **Real-time** như:

- Cập nhật bảng tin
- Nhắn tin
- Thông báo
- Tương tác

---

## II. CÔNG NGHỆ SỬ DỤNG

### 1. Ngôn ngữ & Nền tảng

- **Ngôn ngữ:** C#
- **Framework:** .NET 6.0

### 2. Client

- **Giao diện:** Windows Forms (WinForms)
- **Chức năng:** Hiển thị Feed, Chat, Kết bạn, Upload ảnh

### 3. Server

- **ASP.NET Core**
- **SignalR Hub:** Xử lý các chức năng realtime
- **REST API:** Upload ảnh qua HTTP

### 4. Giao thức truyền tải

- **SignalR (WebSocket):** Chat, Like, Feed, Notification
- **HTTP (REST):** Upload file ảnh

### 5. Lưu trữ dữ liệu

- **In-Memory List (RAM)** tại Server
- Có thể mở rộng sang **SQL Server** trong tương lai

---

## III. KIẾN TRÚC HỆ THỐNG

Hệ thống gồm 2 thành phần chính, hoạt động trong mạng LAN:

### 1. Server – LocketServer

- Đóng vai trò xử lý toàn bộ logic nghiệp vụ
- Quản lý kết nối Client thông qua **LocketHub (SignalR)**
- Lưu trữ ảnh trong thư mục `wwwroot/uploads`
- Cung cấp API `/upload` cho Client

### 2. Client – LocketClient

- Ứng dụng Desktop WinForms
- Kết nối Server thông qua IP mạng LAN
- Giao tiếp dữ liệu bằng **SignalR HubConnection**
- Upload ảnh qua HTTP API

---

## IV. DANH SÁCH CHỨC NĂNG CHI TIẾT

### 1. Hệ thống Tài khoản (Authentication)

- **Đăng ký (Register):**  
  Người dùng tạo tài khoản bằng SĐT, mật khẩu và họ tên. Server kiểm tra trùng SĐT.
- **Đăng nhập (Login):**  
  Xác thực người dùng và lưu phiên làm việc tại Client (`CurrentUser`).

---

### 2. Hệ thống Kết bạn (Connection – 2 bước)

#### Gửi lời mời

- Người dùng A nhập SĐT của B
- Server kiểm tra tồn tại
- Gửi tín hiệu `ReceiveFriendRequest` realtime tới B
- A được thêm vào danh sách chờ của B

#### Chấp nhận lời mời

- B nhận thông báo và danh sách lời mời
- Khi bấm **Đồng ý**, Server:
  - Thêm A và B vào danh sách bạn bè của nhau
  - Feed của hai bên tự động cập nhật

---

### 3. Bảng tin & Tương tác (Feed & Interaction)

- **Đăng bài (Post Story):**
  - Chọn ảnh từ Camera hoặc máy
  - Upload ảnh qua HTTP API
  - Server trả về link ảnh IP LAN
  - Broadcast bài viết tới bạn bè

- **Xem bảng tin:**  
  Hiển thị ảnh của bản thân và bạn bè, sắp xếp theo thời gian mới nhất.

- **Thả tim (Like):**  
  Cập nhật số lượng Like realtime trên tất cả Client.

- **Xóa bài viết:**  
  Chỉ chủ bài viết được phép xóa. Khi xóa, bài viết biến mất realtime.

---

### 4. Hệ thống Nhắn tin (Real-time Chat)

- Chat riêng 1–1 giữa hai người bạn
- Reply trực tiếp từ bài viết
- Tải lịch sử chat khi chọn bạn
- Hiển thị thông báo Popup khi có tin nhắn mới

---

## V. GIẢI PHÁP KỸ THUẬT & XỬ LÝ VẤN ĐỀ

### 1. Kết nối nhiều máy trong LAN

**Vấn đề:** Client không thể kết nối `localhost` khi chạy trên máy khác.

**Giải pháp:**

- Client đọc IP Server từ file `server_ip.txt`
- Server lắng nghe trên `0.0.0.0:5000`

---

### 2. Lỗi hiển thị ảnh trong LAN (Black Image)

**Vấn đề:** Link ảnh dùng `localhost` → máy khác không tải được.

**Giải pháp:**

- Server tự động lấy IP LAN bằng hàm `GetLocalIpAddress()`
- Trả về link ảnh dạng: http://192.168.1.xxx:5000/uploads/tenanh.png

---

### 3. Tối ưu Realtime & Bảo mật

- Dùng `Clients.Group(phoneNumber)` thay vì `Clients.All`
- Đảm bảo cập nhật UI WinForms an toàn luồng bằng `Invoke()`

---

## VI. KẾT LUẬN

Đồ án đã xây dựng thành công một ứng dụng mạng xã hội chia sẻ ảnh hoạt động ổn định trong mạng LAN, đáp ứng tốt các yêu cầu:

- Real-time
- Tốc độ phản hồi nhanh
- Kiến trúc rõ ràng, dễ mở rộng

Hệ thống có thể phát triển thêm các chức năng như lưu trữ CSDL, thông báo đẩy, và triển khai trên Internet.

---
