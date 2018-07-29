using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace DungeonCrawler.Models
{
    public class Post
    {
        private const string dateTimeFormat = "yyyyMMddHHmmss";

        public uint ID { get; set; }
        public int SenderID { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public static IList<Post> FromResponse(DungeonPbEM.GetPostListResponse response)
        {
            var posts = new List<Post>();

            foreach (XmlElement element in response.Body.GetPostListResult.ChildNodes)
            {
                if (element.HasAttribute("delete")) continue;

                posts.Add(new Post
                {
                    ID = UInt32.Parse(element.GetAttribute("postID")),
                    SenderID = Int32.Parse(element.GetAttribute("fromCharacterID")),
                    CreatedAt = DateTime.ParseExact(element.GetAttribute("insertDate"), dateTimeFormat, CultureInfo.InvariantCulture),
                    UpdatedAt = DateTime.ParseExact(element.GetAttribute("updateDate"), dateTimeFormat, CultureInfo.InvariantCulture)
                });
            }

            return posts;
        }
    }
}
