using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using Herd.Models;
using Newtonsoft.Json;

namespace Herd.ViewModels
{
    public class HeventActivityViewModel
    {
        // id of the Hevent
        public string HeventId { get; set; }

        public string Name { get; set; }

        public string Type { get; set; }

        public Hadmin Host { get; set; }

        public DateTime Created { get; set; }

        public string Active { get; set; }

        public List<Hactivity> Activities { get; set; }
    }
}