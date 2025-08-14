

using System.Collections.Generic;

namespace Shared
{
    public class BrowserProfile
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string IconPath { get; set; }
        public string ProfilePicturePath { get; set; }
    }

    public class Browser
    {
        public string Name { get; set; }
        public string ExecutablePath { get; set; }
        public string ProfileRootPath { get; set; }
        public List<BrowserProfile> Profiles { get; set; } = new List<BrowserProfile>();
        public string CommandLineArgumentFormat { get; set; }
    }

    public class AppSettings
    {
        public List<Browser> Browsers { get; set; } = new List<Browser>();
    }
}