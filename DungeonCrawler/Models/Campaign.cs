using System;
using System.Collections.Generic;
using System.Xml;

namespace DungeonCrawler.Models
{
    public class Campaign
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public bool IsDungeonMaster { get; set; }
        public bool IsActive { get; set; }

        public static IList<Campaign> FromResponse(DungeonPbEM.GetCampaignListResponse response)
        {
            var campaigns = new List<Campaign>();

            foreach (XmlElement element in response.Body.GetCampaignListResult)
            {
                campaigns.Add(new Campaign
                {
                    ID = Int32.Parse(element.GetAttribute("campaignID")),
                    Name = element.GetAttribute("name"),
                    IsDungeonMaster = element.GetAttribute("isDM").Equals("1"),
                    IsActive = element.GetAttribute("status").Equals("1")
                });
            }

            return campaigns;
        }
    }
}