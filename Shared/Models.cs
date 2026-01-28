using System;
using System.Collections.Generic;

namespace Shared
{
    public class User
    {
        public string PhoneNumber { get; set; } // ID
        public string Password { get; set; }
        public string FullName { get; set; }
        public string AvatarUrl { get; set; }
        // Danh sách SĐT bạn bè
        public List<string> Friends { get; set; } = new List<string>();
        // Danh sách lưu SĐT những người gửi lời mời kết bạn
        public List<string> FriendRequests { get; set; } = new List<string>();
    }

    public class Post
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string AuthorPhone { get; set; }
        public string AuthorName { get; set; }
        public string ImageUrl { get; set; }
        public string Caption { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Thay đổi quan trọng: Lưu danh sách người like thay vì chỉ số lượng
        public List<string> LikedBy { get; set; } = new List<string>();

        // Helper để lấy số lượng
        public int LikeCount => LikedBy.Count;
    }

    public class Message
    {
        public string FromUser { get; set; } // SĐT người gửi
        public string SenderName { get; set; } // Tên người gửi
        public string ToUser { get; set; } // SĐT người nhận
        public string Content { get; set; }
        public string FileUrl { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}