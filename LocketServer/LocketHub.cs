using Microsoft.AspNetCore.SignalR;
using Shared;

namespace LocketServer
{
    public class LocketHub : Hub
    {
        // Database giả lập
        public static List<User> Users = new List<User>();
        public static List<Post> Posts = new List<Post>();

        // 1. THÊM KHO LƯU TRỮ TIN NHẮN
        public static List<Message> GlobalMessages = new List<Message>();

        // --- User & Friend ---
        public async Task<bool> Register(string phone, string password, string name)
        {
            if (Users.Any(u => u.PhoneNumber == phone)) return false;
            Users.Add(new User { PhoneNumber = phone, Password = password, FullName = name });
            Console.WriteLine($"[REGISTER] {name}");
            return true;
        }

        public async Task<User> Login(string phone, string password)
        {
            var user = Users.FirstOrDefault(u => u.PhoneNumber == phone && u.Password == password);
            if (user != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, phone);
                Console.WriteLine($"[LOGIN] {phone}");
            }
            return user;
        }

        public async Task<bool> AddFriend(string myPhone, string friendPhone)
        {
            var me = Users.FirstOrDefault(u => u.PhoneNumber == myPhone);
            var friend = Users.FirstOrDefault(u => u.PhoneNumber == friendPhone);
            if (me == null || friend == null) return false;
            if (me.Friends.Contains(friendPhone)) return false;

            me.Friends.Add(friendPhone);
            friend.Friends.Add(myPhone);

            await Clients.Group(myPhone).SendAsync("UpdateFriendList", me.Friends);
            await Clients.Group(friendPhone).SendAsync("UpdateFriendList", friend.Friends);
            return true;
        }

        public async Task<string> GetUserName(string phone)
        {
            var u = Users.FirstOrDefault(x => x.PhoneNumber == phone);
            return u != null ? u.FullName : phone;
        }

        // --- Post & Like ---
        public async Task UploadPost(Post post)
        {
            Posts.Add(post);
            await Clients.All.SendAsync("ReceivePost", post);
        }

        public async Task GetPosts()
        {
            var sortedPosts = Posts.OrderByDescending(p => p.CreatedAt).ToList();
            await Clients.Caller.SendAsync("LoadHistoryPosts", sortedPosts);
        }

        public async Task ToggleLike(Guid postId, string userPhone)
        {
            var post = Posts.FirstOrDefault(p => p.Id == postId);
            if (post != null)
            {
                if (post.LikedBy.Contains(userPhone)) post.LikedBy.Remove(userPhone);
                else post.LikedBy.Add(userPhone);

                await Clients.All.SendAsync("UpdateLike", postId, post.LikedBy.Count, post.LikedBy);
            }
        }

        // --- CHAT (QUAN TRỌNG: CẬP NHẬT ĐỂ LƯU TRỮ) ---
        public async Task SendPrivateMessage(Message msg)
        {
            // 2. LƯU TIN NHẮN VÀO LIST TRƯỚC KHI GỬI
            GlobalMessages.Add(msg);
            Console.WriteLine($"[MSG] {msg.FromUser} -> {msg.ToUser}: {msg.Content}");

            // Gửi cho người nhận
            await Clients.Group(msg.ToUser).SendAsync("ReceiveMessage", msg);
            // Gửi lại cho người gửi (để hiển thị)
            await Clients.Group(msg.FromUser).SendAsync("ReceiveMessage", msg);
        }

        // 3. HÀM MỚI: LẤY LỊCH SỬ TIN NHẮN GIỮA 2 NGƯỜI
        public async Task<List<Message>> GetPrivateMessages(string user1, string user2)
        {
            // Lọc ra các tin nhắn mà (Người gửi là A và Nhận là B) HOẶC (Người gửi là B và Nhận là A)
            return GlobalMessages
                .Where(m => (m.FromUser == user1 && m.ToUser == user2) ||
                            (m.FromUser == user2 && m.ToUser == user1))
                .OrderBy(m => m.Timestamp) // Sắp xếp theo thời gian
                .ToList();
        }
    }
}