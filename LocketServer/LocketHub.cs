using Microsoft.AspNetCore.SignalR;
using Shared;

namespace LocketServer
{
    public class LocketHub : Hub
    {
        public static List<User> Users = new List<User>();
        public static List<Post> Posts = new List<Post>();
        public static List<Message> GlobalMessages = new List<Message>();

        public async Task<bool> Register(string phone, string password, string name)
        {
            if (Users.Any(u => u.PhoneNumber == phone))
            {
                ServerLogger.LogWarning($"Register failed: {phone} already exists");
                return false;
            }
            Users.Add(new User { PhoneNumber = phone, Password = password, FullName = name });
            ServerLogger.LogInfo($"New User Registered: {name} ({phone})");
            return true;
        }

        public async Task<User> Login(string phone, string password)
        {
            var user = Users.FirstOrDefault(u => u.PhoneNumber == phone && u.Password == password);
            if (user != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, phone);
                ServerLogger.LogInfo($"User Logged In: {phone}");
            }
            else
            {
                ServerLogger.LogWarning($"Login failed: {phone}");
            }
            return user;
        }

        // ... (Các hàm AddFriend, GetUserName giữ nguyên, không cần log chi tiết) ...
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

            ServerLogger.LogInfo($"Friendship created: {myPhone} <-> {friendPhone}");
            return true;
        }

        public async Task<string> GetUserName(string phone)
        {
            var u = Users.FirstOrDefault(x => x.PhoneNumber == phone);
            return u != null ? u.FullName : phone;
        }

        public async Task UploadPost(Post post)
        {
            Posts.Add(post);
            ServerLogger.LogInfo($"New Post from {post.AuthorName}: {post.Caption}");
            await Clients.All.SendAsync("ReceivePost", post);
        }

        public async Task GetPosts()
        {
            // Không cần log cái này vì nó gọi nhiều, sẽ làm rối màn hình
            var sortedPosts = Posts.OrderByDescending(p => p.CreatedAt).ToList();
            await Clients.Caller.SendAsync("LoadHistoryPosts", sortedPosts);
        }

        public async Task ToggleLike(Guid postId, string userPhone)
        {
            var post = Posts.FirstOrDefault(p => p.Id == postId);
            if (post != null)
            {
                string action = "Liked";
                if (post.LikedBy.Contains(userPhone))
                {
                    post.LikedBy.Remove(userPhone);
                    action = "Unliked";
                }
                else post.LikedBy.Add(userPhone);

                ServerLogger.LogInfo($"{userPhone} {action} post {postId}");
                await Clients.All.SendAsync("UpdateLike", postId, post.LikedBy.Count, post.LikedBy);
            }
        }

        public async Task SendPrivateMessage(Message msg)
        {
            GlobalMessages.Add(msg);

            // LOG TIN NHẮN MÀU XANH CYAN
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
        
    }
}