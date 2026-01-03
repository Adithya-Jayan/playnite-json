using Playnite.SDK;
using Playnite.SDK.Plugins;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using System.Windows;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace playnite_json
{
    [Guid("1B6AA61E-561C-47F4-9AED-15FFEB574CF4")]
    public class ExportGamesPlugin : GenericPlugin
    {
        private readonly IGameDatabase _gameDatabase;
        private static readonly ILogger Logger = LogManager.GetLogger();

        public ExportGamesPlugin(IPlayniteAPI api) : base(api)
        {
            _gameDatabase = api.Database;
        }

        public override Guid Id { get; } = new Guid("1B6AA61E-561C-47F4-9AED-15FFEB574CF4");

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                Description = "Export Library for Mobile (Zip)",
                MenuSection = "@Mobile Export",
                Action = (a) =>
                {
                    ExportLibrary();
                }
            };
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Optional: Auto-prompt on start can be annoying, moved to menu item.
        }

        private void ExportLibrary()
        {
            var result = PlayniteApi.Dialogs.ShowMessage(
                "This will export your library metadata and images to a Zip file for the mobile app.\nThis may take a while depending on your library size.",
                "Start Mobile Export?",
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes) return;

            PlayniteApi.Notifications.Add(new NotificationMessage(
                "mobile-export-start",
                "Mobile export started...",
                NotificationType.Info
            ));

            Task.Run(() =>
            {
                try
                {
                    PerformExport();
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        "mobile-export-success",
                        "Library exported successfully to 'MobileExport.zip'!",
                        NotificationType.Info
                    ));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Mobile export failed.");
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        "mobile-export-error",
                        $"Export failed: {ex.Message}",
                        NotificationType.Error
                    ));
                }
            });
        }

        private void PerformExport()
        {
            // 1. Setup Paths
            string exportRoot = Path.Combine(PlayniteApi.Paths.ApplicationPath, "MobileExportTemp");
            string imagesRoot = Path.Combine(exportRoot, "images");
            string jsonPath = Path.Combine(exportRoot, "library.json");
            string zipPath = Path.Combine(PlayniteApi.Paths.ApplicationPath, "MobileExport.zip");

            // Clean up previous runs
            if (Directory.Exists(exportRoot)) Directory.Delete(exportRoot, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);

            Directory.CreateDirectory(exportRoot);
            Directory.CreateDirectory(imagesRoot);

            var games = _gameDatabase.Games;
            var exportList = new List<GameInfo>();

            foreach (var game in games)
            {
                var info = new GameInfo
                {
                    Id = game.Id,
                    Name = game.Name,
                    Description = game.Description, // Contains HTML usually
                    ReleaseDate = game.ReleaseDate?.Date,
                    Playtime = (long)game.Playtime, // Seconds (Explicit Cast Fixed)
                    LastPlayed = game.LastActivity,
                    Added = game.Added,
                    CommunityScore = game.CommunityScore,
                    CriticScore = game.CriticScore,
                    UserScore = game.UserScore,
                    Hidden = game.Hidden,
                    Favorite = game.Favorite,
                    InstallDirectory = game.InstallDirectory,
                    IsInstalled = game.IsInstalled
                };

                // Map Linked Data (Ids -> Names)
                info.Platform = game.PlatformIds?.Select(id => _gameDatabase.Platforms.Get(id)?.Name).Where(n => n != null).ToList();
                info.Developers = game.DeveloperIds?.Select(id => _gameDatabase.Companies.Get(id)?.Name).Where(n => n != null).ToList();
                info.Publishers = game.PublisherIds?.Select(id => _gameDatabase.Companies.Get(id)?.Name).Where(n => n != null).ToList();
                info.Genres = game.GenreIds?.Select(id => _gameDatabase.Genres.Get(id)?.Name).Where(n => n != null).ToList();
                info.Tags = game.TagIds?.Select(id => _gameDatabase.Tags.Get(id)?.Name).Where(n => n != null).ToList();
                info.Features = game.FeatureIds?.Select(id => _gameDatabase.Features.Get(id)?.Name).Where(n => n != null).ToList();
                info.Series = game.SeriesIds?.Select(id => _gameDatabase.Series.Get(id)?.Name).Where(n => n != null).ToList();
                info.AgeRating = game.AgeRatingIds?.Select(id => _gameDatabase.AgeRatings.Get(id)?.Name).Where(n => n != null).ToList();
                
                if (game.SourceId != Guid.Empty)
                    info.Source = _gameDatabase.Sources.Get(game.SourceId)?.Name;
                
                if (game.CompletionStatusId != Guid.Empty)
                    info.CompletionStatus = _gameDatabase.CompletionStatuses.Get(game.CompletionStatusId)?.Name;

                // Handle Links
                if (game.Links != null)
                {
                    info.Links = game.Links.ToDictionary(l => l.Name, l => l.Url);
                }

                // Handle Images (Copy to export folder)
                // Using game.Id as subfolder to avoid collisions
                string gameImgPath = Path.Combine(imagesRoot, game.Id.ToString());
                bool hasImages = false;

                if (!string.IsNullOrEmpty(game.CoverImage))
                {
                    if (!hasImages) { Directory.CreateDirectory(gameImgPath); hasImages = true; }
                    string dest = Path.Combine(gameImgPath, "cover" + Path.GetExtension(game.CoverImage));
                    if (CopyDatabaseImage(game.CoverImage, dest))
                        info.CoverImage = $"images/{game.Id}/cover{Path.GetExtension(game.CoverImage)}";
                }

                if (!string.IsNullOrEmpty(game.BackgroundImage))
                {
                    if (!hasImages) { Directory.CreateDirectory(gameImgPath); hasImages = true; }
                    string dest = Path.Combine(gameImgPath, "background" + Path.GetExtension(game.BackgroundImage));
                    if (CopyDatabaseImage(game.BackgroundImage, dest))
                        info.BackgroundImage = $"images/{game.Id}/background{Path.GetExtension(game.BackgroundImage)}";
                }

                if (!string.IsNullOrEmpty(game.Icon))
                {
                    if (!hasImages) { Directory.CreateDirectory(gameImgPath); hasImages = true; }
                    string dest = Path.Combine(gameImgPath, "icon" + Path.GetExtension(game.Icon));
                    if (CopyDatabaseImage(game.Icon, dest))
                        info.Icon = $"images/{game.Id}/icon{Path.GetExtension(game.Icon)}";
                }

                exportList.Add(info);
            }

            // Write JSON
            string json = JsonConvert.SerializeObject(exportList, Formatting.Indented);
            File.WriteAllText(jsonPath, json);

            // Create Zip
            ZipFile.CreateFromDirectory(exportRoot, zipPath);

            // Cleanup Temp
            Directory.Delete(exportRoot, true);
        }

        private bool CopyDatabaseImage(string dbPath, string destPath)
        {
            try
            {
                // Playnite SDK helper to get full path
                // If direct GetFullFilePath is missing in this SDK version context, use manual path.
                // Assuming standard Playnite structure: ConfigurationPath/library/files/dbPath
                // dbPath from API is usually like "guid/image.jpg"
                
                string fullSourcePath;
                
                // Try simple path combination first
                // Warning: PlayniteApi.Paths.ConfigurationPath might need to be verified.
                string basePath = Path.Combine(PlayniteApi.Paths.ConfigurationPath, "library", "files");
                fullSourcePath = Path.Combine(basePath, dbPath);

                if (File.Exists(fullSourcePath))
                {
                    File.Copy(fullSourcePath, destPath, true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to copy image: {dbPath}");
            }
            return false;
        }

        private class GameInfo
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public long Playtime { get; set; }
            public DateTime? LastPlayed { get; set; }
            public DateTime? Added { get; set; }
            public DateTime? ReleaseDate { get; set; }
            public bool Hidden { get; set; }
            public bool Favorite { get; set; }
            public bool IsInstalled { get; set; }
            public string InstallDirectory { get; set; }

            public string Source { get; set; }
            public string CompletionStatus { get; set; }
            
            public List<string> Platform { get; set; }
            public List<string> Developers { get; set; }
            public List<string> Publishers { get; set; }
            public List<string> Genres { get; set; }
            public List<string> Tags { get; set; }
            public List<string> Features { get; set; }
            public List<string> Series { get; set; }
            public List<string> AgeRating { get; set; }

            public Dictionary<string, string> Links { get; set; }

            public string CoverImage { get; set; }
            public string BackgroundImage { get; set; }
            public string Icon { get; set; }

            public int? CommunityScore { get; set; }
            public int? CriticScore { get; set; }
            public int? UserScore { get; set; }
        }
    }
}