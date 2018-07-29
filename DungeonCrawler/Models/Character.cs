using System;
using System.Collections.Generic;
using System.Xml;

namespace DungeonCrawler.Models
{
    public class Character
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string FileName { get; set; }

        public static IList<Character> FromResponse(DungeonPbEM.GetCampaignPCListResponse response)
        {
            var characters = new List<Character>();

            foreach (XmlElement element in response.Body.GetCampaignPCListResult.ChildNodes)
            {
                characters.Add(new Character
                {
                    ID = Int32.Parse(element.GetAttribute("characterID")),
                    Name = element.GetAttribute("name"),
                    FileName = element.GetAttribute("fileName")
                });
            }

            return characters;
        }
    }
}