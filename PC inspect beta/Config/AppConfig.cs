#nullable disable

namespace PC_inspect_beta.Config
{
    /// <summary>
    /// Centralised application configuration.
    /// Swap MongoDB cluster / database name here without touching any other file.
    /// </summary>
    public static class AppConfig
    {
        /// <summary>MongoDB Atlas connection string.</summary>
        public const string MongoConnectionString =
            "mongodb+srv://mbilalnasir:ePnjLUxwJJNo13ZN@inspectifycluster.n3r5mt6.mongodb.net/?appName=InspectifyCluster";

        /// <summary>Database name inside the cluster.</summary>
        public const string DatabaseName = "inspectify";

        /// <summary>Collection for user accounts.</summary>
        public const string UsersCollection = "users";

        /// <summary>Collection for marketplace listings.</summary>
        public const string AdsCollection = "ads";

        /// <summary>Application display name shown in window titles.</summary>
        public const string AppName = "Inspectify LapStore";

        /// <summary>Short tagline shown beneath the app name in the main header.</summary>
        public const string Tagline = "Diagnose · Verify · Sell with Confidence";

        /// <summary>Application version string.</summary>
        public const string Version = "2.0.0";

        // ── SMTP (same Gmail App Password as the web project) ──
        public const string SmtpHost = "smtp.gmail.com";
        public const int SmtpPort = 587;
        public const string SmtpUsername = "inspectifylapstore@gmail.com";
        public const string SmtpPassword = "cvmo rozv fftj pelz";
        public const string SmtpFromName = "Inspectify LapStore";
    }
}
