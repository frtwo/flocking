using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Herd.ViewModels
{
    public class HinviteViewModel
    {
        // created by admin
        public string ActivityId { get; set; }
        public string ResponseId { get; set; }

        public string ActivityTitle { get; set; } // activity title

        // created by admin
        [Required]
        public string Email { get; set; }
    }
}
