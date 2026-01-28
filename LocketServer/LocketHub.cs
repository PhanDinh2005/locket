using Microsoft.AspNetCore.SignalR;
using Shared;

namespace LocketServer
{
    public class LocketHub : Hub
    {
        public static List<User> Users = new List<User>();
        public static List<Post> Posts = new List<Post>();
        public static List<Message> GlobalMessages = new List<Message>();

        // --- USER ---
        public async Task<bool> Register(string phone, string password, string name)
        {
            if (Users.Any(u => u.PhoneNumber == phone)) return false;
            Users.Add(new User { PhoneNumber = phone, Password = password, FullName = name });
            ServerLogger.LogInfo($"New User: {name}");
            return true;
        }

        public async Task<User> Login(string phone, string password)
        {
            var user = Users.FirstOrDefault(u => u.PhoneNumber == phone && u.Password == password);
            if (user != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, phone);
                ServerLogger.LogInfo($"Login: {phone}");
            }
            return user;
        }

        public async Task<string> GetUserName(string phone)
        {
            var u = Users.FirstOrDefault(x => x.PhoneNumber == phone);
            return u != null ? u.FullName : phone;
        }

        // --- BƯỚC 1: Xóa hàm AddFriend cũ đi ---
        // --- BƯỚC 2: Dán 2 hàm này vào thay thế ---

        // Hàm 1: Gửi lời mời kết bạn (Thay cho nút Add Friend cũ)
        public async Task<string> SendFriendRequest(string myPhone, string targetPhone)
        {
            var me = Users.FirstOrDefault(u => u.PhoneNumber == myPhone);
            var target = Users.FirstOrDefault(u => u.PhoneNumber == targetPhone);

            // Kiểm tra lỗi cơ bản
            if (me == null || target == null) return "Không tìm thấy người dùng.";
            if (myPhone == targetPhone) return "Không thể kết bạn với chính mình.";
            if (me.Friends.Contains(targetPhone)) return "Hai người đã là bạn bè rồi.";

            // Kiểm tra xem đã gửi lời mời trước đó chưa
            // LƯU Ý: Bạn cần thêm List<string> FriendRequests vào class User nhé (xem Bước 3 bên dưới)
            if (target.FriendRequests.Contains(myPhone)) return "Đã gửi lời mời rồi, hãy chờ họ đồng ý.";

            // Thêm vào danh sách chờ của người kia
            target.FriendRequests.Add(myPhone);

            // Báo ngay cho người kia biết (Real-time) để hiện thông báo
            await Clients.Group(targetPhone).SendAsync("ReceiveFriendRequest", myPhone, me.FullName);

            ServerLogger.LogInfo($"Request: {myPhone} -> {targetPhone}");
            return "Đã gửi lời mời kết bạn thành công!";
        }

        // Hàm 2: Chấp nhận lời mời (Dùng cho nút "Đồng ý" bên Client)
        public async Task AcceptFriendRequest(string myPhone, string requesterPhone)
        {
            var me = Users.FirstOrDefault(u => u.PhoneNumber == myPhone); // "me" là người ĐƯỢC mời (đang bấm đồng ý)
            var requester = Users.FirstOrDefault(u => u.PhoneNumber == requesterPhone); // "requester" là người GỬI lời mời

            if (me != null && requester != null)
            {
                // 1. Xóa khỏi danh sách chờ
                me.FriendRequests.Remove(requesterPhone);

                // 2. Thêm vào danh sách bạn bè chính thức (2 chiều)
                if (!me.Friends.Contains(requesterPhone)) me.Friends.Add(requesterPhone);
                if (!requester.Friends.Contains(myPhone)) requester.Friends.Add(myPhone);

                // 3. Cập nhật danh sách bạn bè cho cả 2 ngay lập tức
                await Clients.Group(myPhone).SendAsync("UpdateFriendList", me.Friends);
                await Clients.Group(requesterPhone).SendAsync("UpdateFriendList", requester.Friends);

                // 4. Tự động làm mới Feed cho cả 2 (để thấy ảnh của nhau ngay)
                await GetPosts(myPhone); // Gửi feed mới cho mình
                await Clients.Group(requesterPhone).SendAsync("RefreshFeed"); // Báo người kia load lại feed

                // 5. Gửi thông báo cho người xin kết bạn biết
                await Clients.Group(requesterPhone).SendAsync("ReceiveMessage", new Message
                {
                    FromUser = "SYSTEM",
                    SenderName = "Hệ thống",
                    ToUser = requesterPhone,
                    Content = $"{me.FullName} đã chấp nhận lời mời kết bạn!"
                });

                ServerLogger.LogInfo($"Accepted: {myPhone} <-> {requesterPhone}");
            }
        }

        // --- POST & FEED (QUAN TRỌNG) ---

        // 1. Đăng bài: Chỉ gửi cho bạn bè
        // Trong LocketHub.cs
        public async Task UploadPost(Post post)
        {
            Posts.Add(post);
            ServerLogger.LogInfo($"New Post: {post.Caption}");

            // Tìm thông tin người đăng
            var author = Users.FirstOrDefault(u => u.PhoneNumber == post.AuthorPhone);

            if (author != null)
            {
                // QUAN TRỌNG: Gửi cho CHÍNH MÌNH (để máy mình hiện ảnh vừa đăng)
                await Clients.Group(author.PhoneNumber).SendAsync("ReceivePost", post);

                // Sau đó mới gửi cho bạn bè
                foreach (var friendPhone in author.Friends)
                {
                    await Clients.Group(friendPhone).SendAsync("ReceivePost", post);
                }
            }
        }

        // 2. Lấy bài cũ (Bao gồm cả bài quá khứ của bạn bè)
        public async Task GetPosts(string myPhone)
        {
            var me = Users.FirstOrDefault(u => u.PhoneNumber == myPhone);
            if (me != null)
            {
                // Lấy bài của MÌNH hoặc của BẠN BÈ
                var myFeed = Posts
                    .Where(p => p.AuthorPhone == myPhone || me.Friends.Contains(p.AuthorPhone))
                    .OrderByDescending(p => p.CreatedAt) // Mới nhất lên đầu
                    .ToList();

                await Clients.Caller.SendAsync("LoadHistoryPosts", myFeed);
            }
        }

        // 3. Like (Giữ nguyên gửi All để tránh lỗi UI không đồng bộ, vì Like metadata không quá nhạy cảm)
        public async Task ToggleLike(Guid postId, string userPhone)
        {
            var post = Posts.FirstOrDefault(p => p.Id == postId);
            if (post != null)
            {
                if (post.LikedBy.Contains(userPhone)) post.LikedBy.Remove(userPhone);
                else post.LikedBy.Add(userPhone);

                // Gửi cho TẤT CẢ để đảm bảo ai đang xem bài đó cũng thấy tim nhảy
                await Clients.All.SendAsync("UpdateLike", postId, post.LikedBy.Count, post.LikedBy);
                ServerLogger.LogInfo($"Like update: {postId}");
            }
        }

        // 4. Xóa bài
        public async Task DeletePost(Guid postId, string requestUserPhone)
        {
            // 1. Tìm bài viết
            var post = Posts.FirstOrDefault(p => p.Id == postId);

            // 2. Kiểm tra bảo mật: Bài phải tồn tại VÀ Người yêu cầu phải là Tác giả
            if (post != null && post.AuthorPhone == requestUserPhone)
            {
                // 3. Xóa khỏi List trong RAM
                Posts.Remove(post);
                ServerLogger.LogWarning($"User {requestUserPhone} deleted post {postId}");

                // 4. Báo cho TẤT CẢ Client biết để gỡ bài đó xuống ngay lập tức
                await Clients.All.SendAsync("PostDeleted", postId);
            }
        }

        // --- CHAT ---
        public async Task SendPrivateMessage(Message msg)
        {
            GlobalMessages.Add(msg);
            ServerLogger.LogChat(msg.FromUser, msg.ToUser, msg.Content);
            await Clients.Group(msg.ToUser).SendAsync("ReceiveMessage", msg);
            await Clients.Group(msg.FromUser).SendAsync("ReceiveMessage", msg);
        }

        public async Task<List<Message>> GetPrivateMessages(string user1, string user2)
        {
            return GlobalMessages
                .Where(m => (m.FromUser == user1 && m.ToUser == user2) ||
                            (m.FromUser == user2 && m.ToUser == user1))
                .OrderBy(m => m.Timestamp)
                .ToList();
        }
        // Thêm vào trong class LocketHub
        // Thêm hàm này vào LocketHub.cs để Client lấy danh sách lời mời
        public async Task GetFriendRequests(string myPhone)
        {
            var me = Users.FirstOrDefault(u => u.PhoneNumber == myPhone);

            // Nếu tìm thấy user và danh sách lời mời không null
            if (me != null && me.FriendRequests != null)
            {
                // Trả về danh sách SĐT đang chờ cho Client
                await Clients.Caller.SendAsync("LoadFriendRequests", me.FriendRequests);
            }
            else
            {
                // Trả về danh sách rỗng nếu chưa có gì
                await Clients.Caller.SendAsync("LoadFriendRequests", new List<string>());
            }
        }

    }
}