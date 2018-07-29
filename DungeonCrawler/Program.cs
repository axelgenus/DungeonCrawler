using DungeonCrawler.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.Threading.Tasks;

namespace DungeonCrawler
{
    class Program
    {
        private static ChannelFactory<DungeonPbEM.DungeonXMLSoapChannel> channelFactory;

        static void Main(string[] args)
        {
            try
            {
                MainAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"An error has occurred: {exception.Message}.");

                #if DEBUG
                Console.WriteLine(exception.StackTrace);

                if (exception.InnerException != null)
                {
                    Console.WriteLine($"Inner exception\n{exception.InnerException}");
                }
                #endif
            }

            Console.WriteLine("Done! Press any key to exit.");
            Console.ReadKey();
        }

        private static async Task MainAsync(string[] args)
        {
            Console.WriteLine("Welcome in DungeonCrawler!");
            Console.WriteLine();

            channelFactory = new ChannelFactory<DungeonPbEM.DungeonXMLSoapChannel>("*");

            User user = await Login();
            if (user == null) return;

            IList<Campaign> campaigns = await GetCampaigns(user);
            if (campaigns == null) return;

            Campaign campaign = null;
            do
            {
                Console.Write("Select a campaign to backup: ");

                if (UInt32.TryParse(Console.ReadLine(), out uint campaignID))
                {
                    campaign = campaigns.SingleOrDefault(c => c.ID == campaignID);
                }
            }
            while (campaign == null);

            IList<Character> characters = await GetCharacters(user, campaign);
            if (characters == null) return;

            var backupPath = PrepareBackup(campaign);
            if (string.IsNullOrEmpty(backupPath)) return;

            await BackupCharacters(backupPath, user, campaign, characters);
            await BackupPosts(backupPath, user, campaign);
        }

        private static async Task<User> Login()
        {
            Console.Write("Please enter your username: ");
            var username = Console.ReadLine();

            Console.Write("Please enter your password: ");
            var password = Console.ReadLine();

            User user;

            using (DungeonPbEM.DungeonXMLSoapChannel service = channelFactory.CreateChannel())
            {
                var body = new DungeonPbEM.LoginRequestBody(username, password);
                var response = await service.LoginAsync(new DungeonPbEM.LoginRequest(body));

                user = User.FromResponse(response);
            }

            Console.WriteLine($"Logged in as '{user.DisplayName}'");

            return user;
        }

        private static async Task<IList<Campaign>> GetCampaigns(User user)
        {
            Console.Write("Getting user's campaigns... ");

            IList<Campaign> campaigns;

            using (DungeonPbEM.DungeonXMLSoapChannel service = channelFactory.CreateChannel())
            {
                var body = new DungeonPbEM.GetCampaignListRequestBody(user.ID);
                var response = await service.GetCampaignListAsync(new DungeonPbEM.GetCampaignListRequest(body));

                campaigns = Campaign.FromResponse(response);
            }

            Console.WriteLine("done!");

            // Only active campaigns where the user is the dungeon master can be backed up
            campaigns = campaigns.Where(c => c.IsActive && c.IsDungeonMaster).ToList();

            if (campaigns.Any())
            {
                foreach (Campaign campaign in campaigns)
                {
                    Console.WriteLine($"[{campaign.ID}] {campaign.Name}");
                }
            }
            else
            {
                campaigns = null;

                Console.WriteLine("No campaigns are available for backing up.");
            }

            return campaigns;
        }

        private static async Task<IList<Character>> GetCharacters(User user, Campaign campaign)
        {
            Console.Write("Getting campaign's characters... ");

            IList<Character> characters;

            using (DungeonPbEM.DungeonXMLSoapChannel service = channelFactory.CreateChannel())
            {
                var body = new DungeonPbEM.GetCampaignPCListRequestBody(user.ID, campaign.ID);
                var response = await service.GetCampaignPCListAsync(new DungeonPbEM.GetCampaignPCListRequest(body));

                characters = Character.FromResponse(response);
            }

            Console.WriteLine("done!");

            return characters;
        }

        private static string PrepareBackup(Campaign campaign)
        {
            string backupPath = Path.Combine("Backups", campaign.Name);

            if (Directory.Exists(backupPath))
            {
                int answer;
                do
                {
                    Console.Write($"A backup for '{campaign.Name}' already exists. Do you want to overwrite it? [y/n]: ");
                    answer = Console.Read();
                }
                while (answer != 'y' && answer != 'n');

                if (answer == 'n') return null;

                Directory.Delete(backupPath, true);
            }

            Directory.CreateDirectory(backupPath);

            return backupPath;
        }

        private static async Task BackupCharacters(string backupPath, User user, Campaign campaign, IList<Character> characters)
        {
            Console.WriteLine($"Backing up characters:");

            var charactersPath = Path.Combine(backupPath, "Characters");
            Directory.CreateDirectory(charactersPath);

            using (WebClient webClient = new WebClient())
            using (DungeonPbEM.DungeonXMLSoapChannel service = channelFactory.CreateChannel())
            {
                foreach (Character character in characters)
                {
                    Console.WriteLine($" * {character.Name}");

                    var body = new DungeonPbEM.GetCharacterSheetRequestBody(user.ID, campaign.ID, 0, character.ID);
                    var response = await service.GetCharacterSheetAsync(new DungeonPbEM.GetCharacterSheetRequest(body));

                    var sheetPath = Path.Combine(charactersPath, $"{character.Name}.xml");
                    File.WriteAllText(sheetPath, response.Body.GetCharacterSheetResult.OuterXml);

                    var avatarUri = new Uri($"http://www.dungeonpbem.net/Image/Character/{character.FileName}");
                    var avatarPath = Path.Combine(charactersPath, character.FileName);
                    await webClient.DownloadFileTaskAsync(avatarUri, avatarPath);
                }
            }
        }

        private static async Task BackupPosts(string backupPath, User user, Campaign campaign)
        {
            Console.WriteLine($"Backing up posts:");

            var postsPath = Path.Combine(backupPath, "Posts");
            Directory.CreateDirectory(postsPath);

            using (DungeonPbEM.DungeonXMLSoapChannel service = channelFactory.CreateChannel())
            {
                const byte quantity = 25;
                const ushort summaryLength = 100;
                ushort page = 1;

                bool done = false;

                do
                {
                    Console.Write($" * Page {page}");

                    IList<Post> posts;
                    {
                        var body = new DungeonPbEM.GetPostListRequestBody(user.ID, campaign.ID, 0, page, quantity, summaryLength);
                        var response = await service.GetPostListAsync(new DungeonPbEM.GetPostListRequest(body));

                        posts = Post.FromResponse(response);

                        done = response.Body.GetPostListResult.ChildNodes.Count < quantity;
                    }

                    foreach (var post in posts)
                    {
                        Console.Write(".");

                        var body = new DungeonPbEM.GetPostSingleRequestBody(user.ID, campaign.ID, 0, post.ID);
                        var response = await service.GetPostSingleAsync(new DungeonPbEM.GetPostSingleRequest(body));

                        var postPath = Path.Combine(postsPath, $"{post.ID}.xml");
                        File.WriteAllText(postPath, response.Body.GetPostSingleResult.InnerXml);
                        File.SetCreationTime(postPath, post.CreatedAt);
                        File.SetLastWriteTime(postPath, post.UpdatedAt);
                    }

                    Console.WriteLine();

                    page++;
                }
                while (!done);
            }
        }
    }
}
