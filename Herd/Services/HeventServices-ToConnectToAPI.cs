using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

// for DocumentDB
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System.Configuration;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Security;
using System.Collections.ObjectModel;
using System.Data.Entity;
using Herd.Models;
using System.Net.Http;
using System.IdentityModel.Tokens;

namespace Herd.Services
{
    /* allows for signup for a new Herd user type */
    public class HerdDbaaaa<T> where T : class
    {
        private static string database;
        private static string Database
        {
            get
            {
                if (database == null)
                {
                    database = ConfigurationManager.AppSettings["HerdApi"];
                }
                return database;
            }
        }

        private static string heventsUri = "/api/Events";
        private static string hactivitiesUri = "/api/Activities";

        public enum docType { EVENT, ACTIVITY };

        private static string DatabaseUri(docType type)
        {
            string databaseUri = Database;

            // set the Uri for the data
            switch (type)
            {
                case docType.ACTIVITY:
                    databaseUri += hactivitiesUri;
                    break;

                default:
                    databaseUri += heventsUri;
                    break;
            }

            return databaseUri;
        }

        private static async Task<T> GetDocument(string id, docType type = docType.EVENT)
        {
            string databaseUri = DatabaseUri(type);

            using (var client = new HttpClient())
            {
                try {
                    HttpResponseMessage response = await client.PostAsJsonAsync<string>(databaseUri, id);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsAsync<T>();
                    }
                }
                catch (Exception)
                {
                    // TODO: log error
                }
                return null;
            }
        }

        public static async Task<T> CreateHtypeAsync(T item, docType type = docType.EVENT)
        {
            string databaseUri = DatabaseUri(type);

            using (var client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.PostAsJsonAsync<T>(databaseUri, item);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsAsync<T>();
                    }
                }
                catch (Exception)
                {
                    // TODO: log error
                }
                return null;
            }
        }

        //public static T CreateHactivity(T item)
        //{
        //    string databaseUri = DatabaseUri(docType.ACTIVITY);

        //    using (var client = new HttpClient())
        //    {
        //        try
        //        {
        //            HttpResponseMessage response = client.PostAsJsonAsync<T>(databaseUri, item).Result;
        //            if (response.IsSuccessStatusCode)
        //            {
        //                return response.Content.ReadAsAsync<T>().Result;
        //            }
        //        }
        //        catch (Exception)
        //        {
        //            // TODO: log error
        //        }
        //        return null;
        //    }
        //}

        public static async Task<T> UpdateHtypeAsync(string id, T item, docType type = docType.EVENT)
        {
            string databaseUri = DatabaseUri(type);

            using (var client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.PutAsJsonAsync<T>(databaseUri, item);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsAsync<T>();
                    }
                }
                catch (Exception)
                {
                    // TODO: log error
                }
                return null;
            }
        }

        //public static Task<T> UpdateHactivity(string id, T item)
        //{
        //    string databaseUri = DatabaseUri(docType.ACTIVITY);

        //    using (var client = new HttpClient())
        //    {
        //        try
        //        {
        //            HttpResponseMessage response = client.PutAsJsonAsync<T>(databaseUri, item).Result;
        //            if (response.IsSuccessStatusCode)
        //            {
        //                return response.Content.ReadAsAsync<T>();
        //            }
        //        }
        //        catch (Exception)
        //        {
        //            // TODO: log error
        //        }
        //        return null;
        //    }
        //}

        public static async Task<T> GetHtype(string id, docType type = docType.EVENT)
        {
            string databaseUri = DatabaseUri(type);

            using (var client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(databaseUri + "/" + id);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsAsync<T>();
                    }
                }
                catch (Exception)
                {
                    // TODO: log error
                }
                return null;
            }
        }

        //public static Task<T> GetHactivity(string id)
        //{
        //    string databaseUri = DatabaseUri(docType.ACTIVITY);

        //    using (var client = new HttpClient())
        //    {
        //        try
        //        {
        //            HttpResponseMessage response = client.GetAsync(databaseUri + "/" + id).Result;
        //            if (response.IsSuccessStatusCode)
        //            {
        //                return response.Content.ReadAsAsync<T>();
        //            }
        //        }
        //        catch (Exception)
        //        {
        //            // TODO: log error
        //        }
        //        return null;
        //    }
        //}

        public static async Task<IEnumerable<Hevent>> GetHEvents()
        {
            string databaseUri = DatabaseUri(docType.EVENT);

            using (var client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(databaseUri);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsAsync<IEnumerable<Hevent>>();
                    }
                }
                catch (Exception)
                {
                    // TODO: log error
                }
                return null;
            }
        }

        public static async Task<bool> DeleteHtypeAsync(string id, docType type = docType.EVENT)
        {
            string databaseUri = DatabaseUri(type);

            using (var client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.DeleteAsync(databaseUri + "/" + id);
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    // TODO: log error
                }
                return false;
            }
        }
    }

    // registration and login services
    public class LoginDb
    {
        private static string database;
        private static string Database
        {
            get
            {
                if (database == null)
                {
                    database = ConfigurationManager.AppSettings["HerdApi"] +
                        ConfigurationManager.AppSettings["HerdAuth"];
                }
                return database;
            }
        }      

        // Login
        public async Task<JwtSecurityToken> Login(Registration credentials)
        {
            using (var client = new HttpClient())
            {
                try {
                    HttpResponseMessage answer = await client.PostAsJsonAsync<Registration>(Database, credentials);
                    if (answer.IsSuccessStatusCode) {
                        return await answer.Content.ReadAsAsync<JwtSecurityToken>();
                    }
                }
                catch (Exception) {
                    // TODO: log error
                }
                return null;
            }
        }
    }
}