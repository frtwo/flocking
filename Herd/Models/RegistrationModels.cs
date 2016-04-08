using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Configuration;
using System.Data.Entity;

namespace Herd.Models
{
    public class Registration
    {
        public string username { get; set; }
        public string password { get; set; }
    }

    public class DocumentRightsDbContext : DbContext
    {
        public DbSet<DocumentRights> DocumentRights { get; set; }

        public DocumentRightsDbContext() : base("name=DatabaseConnection")
        {
        }  
    }

    public enum DocumentRightsType
    {
        EVENT,
        ACTIVITY,
        RSVP,
        NOTIFICATION,
        RESPONSE,
        ALERT
            // Add new types below.
    }

    public class DocumentRights
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public string UserName { get; set; }

        public string DocumentId { get; set; }

        public DocumentRightsType DocumentType { get; set; }
    }
}