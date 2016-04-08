using System;
using System.Collections.Generic;
using System.Linq;

// for DocumentDB
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System.Configuration;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Security;
using System.Data.Entity;
using SendGrid;

namespace Herd.Services
{
    /* allows for signup for a new Herd user type */
    public sealed class HerdDb<T> where T : class
    {
        //Expose the "database" value from configuration as a property for internal use
        private static string databaseId;
        private static string DatabaseId
        {
            get
            {
                if (string.IsNullOrEmpty(databaseId))
                {
                    databaseId = ConfigurationManager.AppSettings["DatabaseId"];
                }

                return databaseId;
            }
        }

        //Use the Database if it exists
        private static Microsoft.Azure.Documents.Database ReadDatabase()
        {
            var db = Client.CreateDatabaseQuery()
                            .Where(d => d.Id == DatabaseId)
                            .AsEnumerable()
                            .FirstOrDefault();

            return db;
        }

        //Use the ReadOrCreateDatabase function to get a reference to the database.
        private static Microsoft.Azure.Documents.Database database;
        private static Microsoft.Azure.Documents.Database Database
        {
            get
            {
                if (database == null)
                {
                    database = ReadDatabase();
                }

                return database;
            }
        }

        //Use the DocumentCollection if it exists
        private static DocumentCollection ReadCollection(string databaseLink)
        {
            var col = Client.CreateDocumentCollectionQuery(databaseLink)
                              .Where(c => c.Id == CollectionId)
                              .AsEnumerable()
                              .FirstOrDefault();

            return col;
        }

        //Use the ReadOrCreateCollection function to get a reference to the collection.
        private static DocumentCollection collection;
        private static DocumentCollection Collection
        {
            get
            {
                if (collection == null)
                {
                    collection = ReadCollection(Database.SelfLink);
                }

                return collection;
            }
        }

        //Expose the "collection" value from configuration as a property for internal use
        private static string collectionId;
        private static string CollectionId
        {
            get
            {
                if (string.IsNullOrEmpty(collectionId))
                {
                    collectionId = ConfigurationManager.AppSettings["CollectionId"];
                }

                return collectionId;
            }
        }

        //This property establishes a new connection to DocumentDB the first time it is used, 
        //and then reuses this instance for the duration of the application avoiding the
        //overhead of instantiating a new instance of DocumentClient with each request
        private static DocumentClient client;
        private static DocumentClient Client
        {
            get
            {
                if (client == null)
                {
                    string endpoint = ConfigurationManager.AppSettings["EndpointUrl"];

                    // from http://crosbymichael.com/securestring-how-to.html
                    // encrypts authorization key in memory
                    SecureString authorizationKey = new SecureString();
                    foreach (char c in ConfigurationManager.AppSettings["AuthorizationKey"])
                    {
                        authorizationKey.AppendChar(c);
                    }
                    authorizationKey.MakeReadOnly();

                    client = new DocumentClient(new Uri(endpoint), authorizationKey);
                }

                return client;
            }
        }

        /* ------------------------------------*/
        /* -------- Public Members ------------*/
        /* ------------------------------------*/

        // CREATE
        public static async Task<Document> CreateHtypeAsync(T item)
        {
            return await Client.CreateDocumentAsync(Collection.SelfLink, item);
        }

        // QUERY
        public static T GetHtype(Expression<Func<T, bool>> predicate)
        {
            return Client.CreateDocumentQuery<T>(Collection.SelfLink)
                        .Where(predicate)
                        .AsEnumerable()
                        .FirstOrDefault();
        }

        // READ
        public static async Task<Document> ReadHtype(string documentId)
        {
            Uri documentUri = UriFactory.CreateDocumentUri(database.Id, collection.Id, documentId);
            return await Client.ReadDocumentAsync(documentUri);            
        }

        // READ MULTIPLE
        public static IEnumerable<T> GetHtypes(Expression<Func<T, bool>> predicate)
        {
            return Client.CreateDocumentQuery<T>(Collection.DocumentsLink)
                .Where(predicate)
                .AsEnumerable();
        }

        // UPDATE -- // TODO: Remove the string id since it is too little info
        public static async Task<Document> UpdateHtypeAsync(string id, T item)
        {
            // The response from DocumentDB will tell you whether a Create or a Replace was done.
            // If a Create happened, the HTTP response will be StatusCode 201. If a Replace occurred the StatusCode will be a 200.
            // return await Client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), item);
            // TODO: log
            Uri documentUri = UriFactory.CreateDocumentUri(database.Id, collection.Id, id);
            return await Client.ReplaceDocumentAsync(documentUri, item);
        }

        // DELETE
        public static async Task<Document> DeleteHtypeAsync(string id)
        {
            // TODO: log
            Uri documentUri = UriFactory.CreateDocumentUri(database.Id, collection.Id, id);
            return await Client.DeleteDocumentAsync(documentUri);

        }

        // User utilities
        // CREATE User
        public static async Task<User> CreateDbUser(string username)
        {
            try
            {
                var userDefinition = new User { Id = username };
                var result = await Client.CreateUserAsync(Database.SelfLink, userDefinition);
                return result.Resource;
            }
            // username exists?
            catch (DocumentClientException error)
            {
                if ((int)error.StatusCode == 409)
                {
                    // TODO: log error 409
                    return (new User { Id = username });
                }
                // TODO: log error
                return null;
            }
            // not sure what the error is, so log it.
            catch (Exception)
            {
                // TODO: log error
                return null;
            }
        }

        // READ User
        public static async Task<User> GetDbUser(string username)
        {
            try
            {
                var userLink = UriFactory.CreateUserUri(Database.Id, username);
                var result = await Client.ReadUserAsync(userLink);
                return (result.Resource);
            }
            catch (Exception)
            {
                // TODO: log error
                return null;
            }
        }

        // sets the permission level
        public enum AllowType { READ, OWN }

        // CREATE Permission
        public static async Task<Permission> CreatePermission(string username, string docId,
            AllowType perm = AllowType.READ)
        {
            try
            {
                // get the user
                User user = await GetDbUser(username);

                // set the permission level
                PermissionMode permissionType;
                switch (perm)
                {
                    case AllowType.OWN:
                        permissionType = PermissionMode.All;
                        break;
                    default:
                        permissionType = PermissionMode.Read;
                        break;
                }

                // set the permission
                Permission p = new Permission
                {
                    Id = Guid.NewGuid().ToString("N"),
                    PermissionMode = permissionType,
                    ResourceLink = docId
                };

                // finally, create the permission.
                return (Permission)await Client.CreatePermissionAsync(user.SelfLink, p);
            }
            catch (Exception)
            {
                // TODO: log error
                return null;
            }
        }

        // GET User Permissions
        public static async Task<List<Permission>> GetDbUserPermissions(string username)
        {
            try
            {
                Microsoft.Azure.Documents.User user = await GetDbUser(username);
                FeedResponse<Permission> permissions = await Client.ReadPermissionFeedAsync(user.SelfLink);
                List<Permission> listOfPermissions = new List<Permission>();
                foreach (var permission in permissions)
                {
                    listOfPermissions.Add(permission);
                }

                return listOfPermissions;
            }
            catch (Exception)
            {
                // TODO: log error
                return null;
            }
        }

        // DELETE User


        // DELETE User's Permissions
    } // end HerdDb

}