using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Herd.ViewModels
{
    public class HactivityViewModel
    {
        // id of the Hevent
        public string Id { get; set; }

        public string HeventId { get; set; }

        [Required(ErrorMessage = "required")]
        public string Title { get; set; }

        [Required(ErrorMessage = "required")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Enter a place name and/or address")]
        public string Location { get; set; }

        [Required(ErrorMessage = "Date and time required")]
        public DateTime Starting { get; set; }

        [Required(ErrorMessage = "Date and time required")]
        public DateTime Ending { get; set; }

        public string Question { get; set; }
        public string Choices { get; set; }
    }
}