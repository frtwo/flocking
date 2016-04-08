using Herd.Models;
using Herd.ViewModels;
using Herd.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;
using Microsoft.AspNet.Identity;
using System.Diagnostics;
using SendGrid;
using System.Configuration;

namespace Herd.Controllers
{
    [Authorize]
    public class EventController : Controller
    {
        private static DocumentRightsDbContext rightsContext;
        private static DocumentRightsDbContext RightsContext
        {
            get
            {
                if (rightsContext == null)
                {
                    rightsContext = new DocumentRightsDbContext();
                }
                return rightsContext;
            }
        }

        // GET: Hevents
        public ActionResult Index()
        {
            // get the user's available documents
            string username = User.Identity.Name;

            IEnumerable<DocumentRights> rightsQuery =
                from docRights in RightsContext.DocumentRights
                where docRights.UserName == username
                where docRights.DocumentType == DocumentRightsType.EVENT
                select docRights;

            List<Hevent> rights = new List<Hevent>();

            foreach (var item in rightsQuery)
            {
                rights.Add(HerdDb<Hevent>.GetHtype(t => t.Id == item.DocumentId));
            }

            return View(rights);
        }

        public ActionResult Details(string id)
        {
            // first check if the user is authorized to view this ID
            CheckIfAuthorized(id);

            // then get the Hevent
            var hevent = HerdDb<Hevent>.GetHtype(d => d.Id == id);

            // TODO: return some nice error explaining that there is no data?
            if (hevent == null) return HttpNotFound();

            var item = new HeventActivityViewModel()
            {
                HeventId = hevent.Id,
                Name = hevent.Name,
                Type = hevent.Type,
                Host = hevent.Host,
                Created = hevent.Created,
                Active = hevent.Active,
                Activities = new List<Hactivity>()
            };

            if (hevent.Activities != null)
            {
                // then get the Hactivities associated with the Hevent
                foreach (var activity in hevent.Activities)
                {
                    item.Activities.Add(HerdDb<Hactivity>.GetHtype(d => d.Id == activity));
                }
            }

            return View(item);
        }

        // POST: Hevent
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "Id,Name,Type,Created,Active")] Hevent item)
        {
            if (ModelState.IsValid)
            {
                var hevent = await HerdDb<Hevent>.CreateHtypeAsync(item);
                if (hevent != null)
                {
                    bool saved = await AuthorizeDocument(hevent.Id, DocumentRightsType.EVENT);
                    if (saved) return RedirectToAction("Index");
                }
            }
            return View(item);
        }

        [HttpGet]
        public ActionResult AddActivity(string HeventId)
        {
            CheckIfAuthorized(HeventId);

            //instantiate the product repository
            var viewModel = new HactivityViewModel();
            viewModel.HeventId = HeventId;

            return PartialView(viewModel);            
        }

        [HttpPost] // this action takes the viewModel from the modal
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddActivity(HactivityViewModel viewModel)
        {
            CheckIfAuthorized(viewModel.HeventId);

            // post new Hactivity to the Hevent
            if (ModelState.IsValid)
            {
                try
                {
                    var toUpdate = HerdDb<Hevent>.GetHtype(d => d.Id == viewModel.HeventId);
                    if (toUpdate.Activities == null) toUpdate.Activities = new List<string>();
                    List<string> splitChoices = viewModel.Choices.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

                    // create the activity
                    var activityTemp = new Hactivity() {
                        Description = viewModel.Description,
                        Location = viewModel.Location,
                        Starting = viewModel.Starting,
                        Ending = viewModel.Ending,
                        Title = viewModel.Title
                    };

                    // TODO: change later to include multiple questions
                    activityTemp.Choices = new List<Hactivity.Options>();
                    activityTemp.Choices.Add(new Hactivity.Options() {
                        Question = viewModel.Question,
                        Option = splitChoices
                    });
                                        
                    var activity = await HerdDb<Hactivity>.CreateHtypeAsync(activityTemp);
                    // then update the Event with the Activity.Id
                    if (activity != null)
                    {
                        toUpdate.Activities.Add(activity.Id);
                        await HerdDb<Hevent>.UpdateHtypeAsync(viewModel.HeventId, toUpdate);

                        // after the update is done, go back to the Hevent
                        bool saved = await AuthorizeDocument(activity.Id, DocumentRightsType.ACTIVITY);
                        if (saved)
                        {
                            try
                            {
                                // create a new response
                                var tempResponse = await HerdDb<Hresponse>.CreateHtypeAsync(
                                    new Hresponse() { ActivityId = activity.Id });
                                await AuthorizeDocument(tempResponse.Id, DocumentRightsType.RESPONSE);

                                // then plug it into the existing activity
                                var tempActivity = HerdDb<Hactivity>.GetHtype(t => t.Id == activity.Id);
                                tempActivity.ResponseId = tempResponse.Id;
                                await HerdDb<Hactivity>.UpdateHtypeAsync(activity.Id, tempActivity);

                                // on success, go to the details of the event.
                                return RedirectToAction("Details", new { id = viewModel.HeventId });
                            }
                            catch (Exception)
                            {
                                // TODO: log error
                            }                            
                        }
                    }
                    else
                    {
                        // TODO: show an error that the activity was not added.
                    }
                }
                catch (Exception e)
                {
                    // TODO: log and show an error that something went wrong.
                }
            }

            // else show the original Hevent, unchanged.
            return View(HerdDb<Hevent>.GetHtype(d => d.Id == viewModel.HeventId));
        }

        [HttpGet]
        public ActionResult EditActivity(string id)
        {
            // check to see if the user can edit this
            CheckIfAuthorized(id);

            // get the activity's detail
            var hactivity = HerdDb<Hactivity>.GetHtype(d => d.Id == id);

            // populate the view model
            var viewModel = new HactivityViewModel()
            {
                Id = hactivity.Id,
                Description = hactivity.Description,
                Location = hactivity.Location,
                Starting = hactivity.Starting,
                Ending = hactivity.Ending
            };

            return View(viewModel);
        }

        // TODO: implement EditActivity(string id)
        [HttpPost] // this action takes the viewModel from the modal
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditActivity(HactivityViewModel viewModel)
        {
            CheckIfAuthorized(viewModel.HeventId);

            // post new Hactivity to the Hevent
            if (ModelState.IsValid)
            {
                var toUpdate = HerdDb<Hactivity>.GetHtype(d => d.Id == viewModel.HeventId);

                // TODO: show error to user when toUpdate is null. 

                toUpdate.Description = viewModel.Description;
                toUpdate.Location = viewModel.Location;
                toUpdate.Starting = viewModel.Starting;
                toUpdate.Ending = viewModel.Ending;
                toUpdate.Title = viewModel.Title;

                toUpdate.Choices = new List<Hactivity.Options>();

                List<string> splitChoices = viewModel.Choices.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                toUpdate.Choices.Add(new Hactivity.Options()
                {
                    Question = viewModel.Question,
                    Option = splitChoices
                });


                // TODO: show user that change did not commit if this errors out.
                await HerdDb<Hactivity>.UpdateHtypeAsync(viewModel.Id, toUpdate);
            }

            // after the update is done, go back to the Hevent to show the updated activity.
            return RedirectToAction("Details", new { id = viewModel.Id });
        }

        /* -- Response -- */

        [HttpGet]
        public ActionResult ActivityDetails(string ActivityId)
        {
            CheckIfAuthorized(ActivityId);
            HresponseViewModel viewModel;

            try
            {
                var activity = HerdDb<Hactivity>.GetHtype(t => t.Id == ActivityId);
                var response = HerdDb<Hresponse>.GetHtype(t => t.Id == activity.ResponseId);

                // instantiate the response model
                viewModel = new HresponseViewModel()
                {
                    Activity = activity,
                    Id = ActivityId
                };

                if (response.Alerts != null)
                {
                    viewModel.Alerts = new List<HresponseViewModel.Alert>();
                    foreach (var item in response.Alerts)
                    {
                        var alert = HerdDb<Halert>.GetHtype(t => t.Id == item);
                        viewModel.Alerts.Add(new HresponseViewModel.Alert()
                        {
                            Id = alert.Id,
                            Message = alert.Message,
                            Sent = alert.Sent.ToShortDateString() + " " + alert.Sent.ToShortTimeString()
                        });
                    }
                }

                if (response.Rsvps != null)
                {
                    viewModel.Rsvps = new List<HresponseViewModel.Rsvp>();
                    foreach (var item in response.Rsvps)
                    {
                        var rsvp = HerdDb<Rsvp>.GetHtype(t => t.Id == item);
                        viewModel.Rsvps.Add(new HresponseViewModel.Rsvp()
                        {
                            Answer = string.IsNullOrEmpty(rsvp.Answer) ? "No response yet." : rsvp.Answer,
                            Id = rsvp.Id,
                            Invitee = rsvp.Email
                        });
                    }
                }
            }
            catch (Exception e)
            {
                // TODO: log error
                // TODO: report that item was unable to be saved to the user
                return RedirectToAction("Index");
            }

            return View(viewModel);
        }

        public ActionResult Edit(string id = null)
        {
            // TODO: fix this from API to MVC
            if (string.IsNullOrEmpty(id))
            {
                // TODO: log error and present generic error to the user.
                return View("Index");
            }

            try
            {
                CheckIfAuthorized(id);
                Hevent item = HerdDb<Hevent>.GetHtype(d => d.Id == id);
                return View(item);
            }
            catch (Exception)
            {
                // TODO: log error and present generic error to the user.
                return View("Index");
            }            
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(Hevent item)
        {
            CheckIfAuthorized(item.Id);

            if (ModelState.IsValid)
            {
                await HerdDb<Hevent>.UpdateHtypeAsync(item.Id, item);
                return RedirectToAction("Index");
            }

            return View(item);
        }

        private Hevent GetHevent(string id = null)
        {
            if (id != null)
            {
                CheckIfAuthorized(id);

                try
                {
                    var toGet = HerdDb<Hevent>.GetHtype(d => d.Id == id);
                    return toGet;
                }
                catch (Microsoft.Azure.Documents.DocumentClientException)
                {
                    TempData["Herror"] = "Event is not found.";
                }
            }
            return null;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(string HeventId)
        {
            if (HeventId != null)
            {
                CheckIfAuthorized(HeventId);

                var toDelete = GetHevent(HeventId);

                // first get all activities within this Hevent
                if (toDelete != null)
                {  
                    // then delete them
                    foreach (var activity in toDelete.Activities)
                    {
                        try
                        {
                            await HerdDb<Hactivity>.DeleteHtypeAsync(activity);
                        }
                        catch (Microsoft.Azure.Documents.DocumentClientException)
                        {
                            // do we need to alert the user of previously deleted activities?
                            TempData["Hwarning"] = "No activities available to delete.";
                        }
                    }
                }

                // delete the parent Hevent
                try
                {
                    var docId = toDelete.Id;
                    await HerdDb<Hevent>.DeleteHtypeAsync(docId);
                    await UnAuthorizeDocument(docId);
                }
                catch (Microsoft.Azure.Documents.DocumentClientException)
                {
                    // TODO: return something else to notify of error in deletion
                    TempData["Herror"] = "Unable to delete the Event.";
                    return RedirectToAction("Index");
                }
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteActivity(HactivityViewModel viewModel)
        {
            if (viewModel.Id != null && viewModel.HeventId != null)
            {
                CheckIfAuthorized(viewModel.Id);

                try
                {
                    var docId = viewModel.Id;
                    await HerdDb<Hactivity>.DeleteHtypeAsync(docId);
                    await UnAuthorizeDocument(docId);
                }
                catch (Microsoft.Azure.Documents.DocumentClientException)
                {
                    // TODO: return something else to notify of error in deletion
                    TempData["Herror"] = "Unable to delete the Activity.";
                    return RedirectToAction("Details", 
                        new Herd.ViewModels.HeventActivityViewModel() { HeventId = viewModel.HeventId});
                }
            }

            return RedirectToAction("Index");
        }

        /* -- RSVP -- */

        [HttpGet]
        public ActionResult Invite(string ActivityId)
        {
            try
            {
                // get the activity
                var activity = HerdDb<Hactivity>.GetHtype(d => d.Id == ActivityId);

                CheckIfAuthorized(activity.ResponseId); // HresponseId

                //instantiate the repository
                var viewModel = new HinviteViewModel();
                viewModel.ActivityId = activity.Id;
                viewModel.ActivityTitle = activity.Title;
                viewModel.ResponseId = activity.ResponseId;

                return PartialView(viewModel);
            }
            catch (Exception)
            {
                // TODO: log unauthorized access error
                return RedirectToAction("Index");
            }
        }

        [HttpPost] // this action takes the viewModel from the modal
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Invite(HinviteViewModel viewModel)
        {
            Hactivity activity = null;
            try
            {
                activity = HerdDb<Hactivity>.GetHtype(d => d.Id == viewModel.ActivityId);
                CheckIfAuthorized(activity.ResponseId);
                viewModel.ResponseId = activity.ResponseId;
            }
            catch (Exception)
            {
                // else show the original Invite, unchanged.
                // TODO: log error
                return PartialView(viewModel);
            }

            var checkEmail = new RegexUtilities();
            // post new RSVP to the Hresponse
            if (ModelState.IsValid && checkEmail.IsValidEmail(viewModel.Email))
            {
                var toUpdate = HerdDb<Hresponse>.GetHtype(d => d.Id == viewModel.ResponseId);
                if (toUpdate.Rsvps == null) toUpdate.Rsvps = new List<string>();

                // create the RSVP
                try
                {
                    // create the RSVP
                    var result = await HerdDb<Rsvp>.CreateHtypeAsync(
                    new Rsvp()
                    {
                        ActivityId = toUpdate.ActivityId,
                        Email = viewModel.Email,
                    });

                    // add the RSVP to the invite list in the Hresponse
                    if (toUpdate.Invitees == null) toUpdate.Invitees = new List<string>();
                    // ... unless it is already previously added
                    if (!toUpdate.Invitees.Contains(viewModel.Email))
                    {
                        toUpdate.Invitees.Add(viewModel.Email);
                    }

                    // add the RSVP to the Hresponse
                    toUpdate.Rsvps.Add(result.Id);
                    await HerdDb<Hresponse>.UpdateHtypeAsync(viewModel.ResponseId, toUpdate);

                    bool saved = await AuthorizeDocument(result.Id, DocumentRightsType.RSVP);
                    // if not saved, log error
                    if (!saved)
                    {
                        return PartialView(viewModel);
                    }

                    // compose the callback link
                    string callbackUrl = Url.Action("Rsvp", "Invite",
                        new { RsvpId = result.Id }, protocol: Request.Url.Scheme);

                    // compose the message
                    AlertMessage message = new AlertMessage()
                    {
                        Subject = "[flocking] Invitation to Event: " + activity.Title,
                        Message = "Hello, You have been invited to an event using Flocking! " +
                            "Please respond to the invite by clicking <a href=\"" + callbackUrl + "\">here</a>"
                    };

                    // send message
                    string mailSent = await MailAsync(message);

                    // grant authorization to the invitee
                    await AuthorizeDocument(result.Id, DocumentRightsType.RSVP, viewModel.Email);

                    // TODO: implement function/class to track email statuses
                }
                catch (NullReferenceException)
                {
                    // TODO: log the error
                    return PartialView(viewModel);
                }
                catch (ArgumentNullException)
                {
                    // TODO: log the error
                    return PartialView(viewModel);
                }
                catch (Exception)
                {
                    // TODO: log the error
                    return PartialView(viewModel);
                }

                // after the update is done, go back to the Hactivity
                return RedirectToAction("ActivityDetails", new { ActivityId = viewModel.ActivityId });
            }

            // else show the original Invite, unchanged.
            return PartialView(viewModel);
        }

        private bool IsRegistered(string email)
        {
            // Access the User names from the context
            using (var context = new UserDbContext())
            {
                // Populate your users and store it in the ViewBag (or other storage)
                var username = context.Users.Where(t => t.UserName == email);
                if (username.FirstOrDefault() == null) return true;
            }

            return false;
        }

        /* -- Alerts -- */

        [HttpGet]
        public ActionResult AddAlert(string ResponseId)
        {
            CheckIfAuthorized(ResponseId); // HresponseId

            //instantiate the repository
            var viewModel = new HalertViewModel();
            viewModel.ResponseId = ResponseId;

            return PartialView("Alert", viewModel);
        }

        [HttpPost] // this action takes the viewModel from the modal
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddAlert(HalertViewModel viewModel)
        {
            CheckIfAuthorized(viewModel.ResponseId);
            
            if (ModelState.IsValid)
            {                                
                try
                {
                    // post new Alert to the Hresponse
                    var response = HerdDb<Hresponse>.GetHtype(d => d.Id == viewModel.ResponseId);
                    if (response.Alerts == null) response.Alerts = new List<string>();

                    // create the Alert
                    var mailMessage = new AlertMessage() {
                        Message = viewModel.Message,
                        Subject = "flocking Alert: " + viewModel.Subject,
                    };                    
                    var alertDoc = await HerdDb<Halert>.CreateHtypeAsync(new Halert()
                    {
                        Message = viewModel.Message,
                        HresponseId = response.Id
                    });

                    // now load the newly created Halert
                    var alert = HerdDb<Halert>.GetHtype(t => t.Id == alertDoc.Id);

                    // notify people
                    if (response.Invitees == null)
                    {
                        // TODO: send message to user that there are no people invited to alert.
                        return RedirectToAction("ActivityDetails", new { id = viewModel.ResponseId });
                    }
                    foreach (var contact in response.Invitees)
                    {
                        // create the notification
                        var notification = new Hnotification();
                        mailMessage.Recipient = contact;

                        // send the message
                        string strDate = await MailAsync(mailMessage);
                        DateTime timestamp = DateTime.Parse(strDate);
                        notification.Sent = timestamp;
                        notification.Message = viewModel.Message;
                        notification.AlertId = alertDoc.Id;
                        notification.SentTo = contact;

                        // send the notification to the user
                        var newNotification = await HerdDb<Hnotification>.CreateHtypeAsync(notification);
                        // allow the user to see evidence of the notification online
                        await AuthorizeDocument(newNotification.Id, DocumentRightsType.NOTIFICATION,
                            contact);

                        // put the notification in the Response for the admin to see, too
                        if (response.Notifications == null) response.Notifications = new List<string>();
                        response.Notifications.Add(newNotification.Id);

                        // save the notification IDs in the Halert
                        if (alert.Notified == null) alert.Notified = new List<string>();
                        alert.Notified.Add(newNotification.Id);
                        alert.Sent = timestamp;
                    }

                    // update the alert with all the notification IDs collected
                    await HerdDb<Halert>.UpdateHtypeAsync(alert.Id, alert);

                    // update the response with the Halert ID
                    response.Alerts.Add(alert.Id);
                    await HerdDb<Hresponse>.UpdateHtypeAsync(viewModel.ResponseId, response);

                    // now authorize the admin to see this alert
                    bool saved = await AuthorizeDocument(alert.Id, DocumentRightsType.ALERT);
                    // if not saved, log error
                    if (!saved)
                    {
                        // TODO: log the error
                    }
                    // after the update is done, go back to the Hactivity
                    return RedirectToAction("ActivityDetails", new { ActivityId = response.ActivityId });
                }
                catch (NullReferenceException)
                {
                    // TODO: log the error
                }
                catch (ArgumentNullException)
                {
                    // TODO: log the error
                }
                catch (Exception)
                {
                    // TODO: log the error
                }
            }

            // a failure occurred somewhere.
            return RedirectToAction("Index");
        }
        
        [HttpGet]
        public ActionResult AlertDetails(string id)
        {
            // first check if the user is authorized to view this ID
            CheckIfAuthorized(id); // HalertId
            HalertDetailViewModel viewModel = new HalertDetailViewModel();

            try
            {
                // then get the Halert
                var halert = HerdDb<Halert>.GetHtype(d => d.Id == id);
                // we need the Hresponse for the people to send the alert to
                var hresponse = HerdDb<Hresponse>.GetHtype(d => d.Id == halert.HresponseId);

                // TODO: return some nice error explaining that there is no data?
                if (halert == null) return HttpNotFound();

                var alertDetail = new HalertDetailViewModel()
                {
                    Id = halert.Id,
                    Message = halert.Message,
                };

                // get the notifications in the Halert and display the statuses
                foreach (var notifiedId in halert.Notified)
                {
                    var notification = HerdDb<Hnotification>.GetHtype(d => d.Id == notifiedId);
                    viewModel.Alertees.Add(new HalertDetailViewModel.Notified()
                    {
                        Email = notification.SentTo,
                        Received = (notification.Received == null) ? "Not yet." : notification.Received.ToString()
                    });
                }                
            }
            catch (Exception e)
            {
                // TODO: log error
                return RedirectToAction("Index");
            }

            return PartialView(viewModel);
        }

        /* -- private functions -- */

        // checks to see if the user has access to use the document (docId)
        private void CheckIfAuthorized(string docId)
        {
            string username = User.Identity.Name;

            IEnumerable<DocumentRights> rightsQuery =
                from docRights in RightsContext.DocumentRights
                where docRights.UserName == username
                where docRights.DocumentId == docId
                select docRights;

            if (rightsQuery.FirstOrDefault() == null)
            {
                // TODO: log unauthorized access error
                RedirectToAction("Index");
            }

            // user access checks out.
            return;
        }

        // authorize the user for the documentId given (and type to describe)
        private async Task<bool> AuthorizeDocument(string docId, DocumentRightsType docType,
            string username = null)
        {
            try
            {
                RightsContext.DocumentRights.Add(new DocumentRights()
                {
                    UserName = (username == null) ? User.Identity.Name : username,
                    DocumentId = docId,
                    DocumentType = docType
                });
                await RightsContext.SaveChangesAsync();
            }
            catch (Exception)
            {
                // TODO: log error
                // TODO: report that item was unable to be saved to the user
                return false;
            }

            return true;
        }

        // unauthorize the user for the documentId given (and type to describe)
        private async Task<bool> UnAuthorizeDocument(string docId)
        {
            try
            {
                IEnumerable<DocumentRights> query = RightsContext.DocumentRights
                    .Where(t => t.DocumentId == docId
                    && t.UserName == User.Identity.Name);

                if (query.FirstOrDefault() != null)
                {
                    RightsContext.DocumentRights.Remove(query.FirstOrDefault());
                    await RightsContext.SaveChangesAsync();
                }
            }
            catch (Exception)
            {
                // TODO: log error
                // TODO: report that item was unable to be saved to the user
                return false;
            }

            return true;
        }

        // Use NuGet to install SendGrid (Basic C# client lib) 
        private async Task<string> MailAsync(AlertMessage message)
        {
            var myMessage = new SendGridMessage();
            // uncomment to send emails to the user's actual email/SMS
            // myMessage.AddTo(message.Destination);
            myMessage.AddTo("frichson@outlook.com");
            myMessage.From = new System.Net.Mail.MailAddress(
                                "info@davidcovers.com", "DavidCovers");
            myMessage.Subject = message.Subject;
            myMessage.Text = message.Message;
            myMessage.Html = message.Message;

            var apiKey = ConfigurationManager.AppSettings["mailKey"];
            // create a Web transport, using API Key
            var transportWeb = new Web(apiKey);

            // Send the email.
            if (transportWeb != null)
            {
                await transportWeb.DeliverAsync(myMessage);
                return DateTime.Now.ToString();
            }
            else
            {
                Trace.TraceError("Failed to create Web transport.");
                await Task.FromResult(0);
                return null;
            }
        }

        private class AlertMessage
        {
            public string Message { get; set; }
            public string Recipient { get; set; }
            public string Subject { get; set; }
        }
    }
}