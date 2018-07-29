using System;

namespace DungeonCrawler.Models
{
    public class User
    {
        public uint ID { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }

        public static User FromResponse(DungeonPbEM.LoginResponse response)
        {
            return new User
            {
                ID = UInt32.Parse(response.Body.LoginResult.GetAttribute("userID")),
                DisplayName = response.Body.LoginResult.GetAttribute("nameDisplay"),
                Email = response.Body.LoginResult.GetAttribute("email")
            };
        }
    }
}
