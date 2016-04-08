using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Herd.ViewModels
{
    public class HalertDetailViewModel
    {
        public string Id { get; set; } // HalertId
        public string Title { get; set; }
        public string Message { get; set; }

        public List<Notified> Alertees { get; set; }

        public class Notified
        {
            public string Email { get; set; }
            public string Received { get; set; }
        }
    }
}