#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using Newtonsoft.Json.Linq;

namespace PC_inspect_beta.Core
{
    /// <summary>
    /// All MongoDB Atlas interactions — users, marketplace ads, and image storage.
    /// Images are persisted via GridFS; the returned "URL" is the GridFS file id string,
    /// which keeps the call-site signature identical to the previous Firebase implementation.
    /// </summary>
    public static class DbHelper
    {
        // ── Singletons ────────────────────────────────────────────────────────
        private static readonly IMongoClient _client =
            new MongoClient(Config.AppConfig.MongoConnectionString);

        private static readonly IMongoDatabase _db =
            _client.GetDatabase(Config.AppConfig.DatabaseName);

        private static readonly IMongoCollection<BsonDocument> _users =
            _db.GetCollection<BsonDocument>(Config.AppConfig.UsersCollection);

        private static readonly IMongoCollection<BsonDocument> _ads =
            _db.GetCollection<BsonDocument>(Config.AppConfig.AdsCollection);

        private static readonly GridFSBucket _gridFs = new GridFSBucket(_db);

        // ── Users ─────────────────────────────────────────────────────────────

        /// <summary>Returns the user object from MongoDB, or null if not found.</summary>
        public static async Task<JObject> GetUser(string username)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", username);
            var doc = await _users.Find(filter).FirstOrDefaultAsync();
            return doc == null ? null : BsonToJObject(doc);
        }

        /// <summary>Finds a user by email address, or null if not found.</summary>
        public static async Task<JObject> GetUserByEmail(string email)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("email", email);
            var doc = await _users.Find(filter).FirstOrDefaultAsync();
            return doc == null ? null : BsonToJObject(doc);
        }

        /// <summary>Creates or overwrites a user record in MongoDB (keyed by username).</summary>
        public static async Task SaveUser(string username, object data)
        {
            var doc = ToBsonDocument(data);
            doc["_id"] = username;

            var filter = Builders<BsonDocument>.Filter.Eq("_id", username);
            await _users.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
        }

        // ── Ad Posts ──────────────────────────────────────────────────────────

        /// <summary>Saves a marketplace listing to MongoDB.</summary>
        public static async Task SaveAdPost(string username, object data)
        {
            var doc = ToBsonDocument(data);
            doc["_id"] = $"{username}_{DateTime.UtcNow.Ticks}";
            await _ads.InsertOneAsync(doc);
        }

        // ── Storage (GridFS) ──────────────────────────────────────────────────

        /// <summary>
        /// Uploads a stream to GridFS under the given key and returns the file id as string.
        /// </summary>
        public static async Task<string> UploadStream(string user, Stream stream, string storageKey)
        {
            if (stream.CanSeek) stream.Position = 0;

            var options = new GridFSUploadOptions
            {
                Metadata = new BsonDocument
                {
                    { "user", user ?? string.Empty },
                    { "uploadedAt", DateTime.UtcNow }
                }
            };

            ObjectId id = await _gridFs.UploadFromStreamAsync(storageKey, stream, options);
            return id.ToString();
        }

        /// <summary>
        /// Uploads local image files to GridFS and returns their file-id strings.
        /// <paramref name="progress"/> is reported as 0–100.
        /// </summary>
        public static async Task<List<string>> UploadImages(string username,
            List<string> localPaths, IProgress<int> progress = null)
        {
            var ids = new List<string>();
            int done = 0;

            foreach (string path in localPaths)
            {
                string fileName = $"ads/{username}_{DateTime.UtcNow.Ticks}_{Path.GetFileName(path)}";
                using var stream = File.OpenRead(path);
                string id = await UploadStream(username, stream, fileName);
                ids.Add(id);
                progress?.Report((++done * 100) / localPaths.Count);
            }
            return ids;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static BsonDocument ToBsonDocument(object data)
        {
            if (data is BsonDocument bd) return bd;

            // Use Newtonsoft to serialize (handles anonymous types, Dictionary<string, object>,
            // and lists of primitives cleanly) then parse into BsonDocument.
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
            return BsonDocument.Parse(json);
        }

        private static JObject BsonToJObject(BsonDocument doc)
        {
            string json = doc.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });
            return JObject.Parse(json);
        }
    }
}
