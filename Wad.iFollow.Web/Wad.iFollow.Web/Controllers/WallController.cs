using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Wad.iFollow.Web.Models;
using Newtonsoft.Json;

namespace Wad.iFollow.Web.Controllers
{
    public class WallController : BaseController
    {
        //
        // GET: /Main/

        public ActionResult MainPage()
        {
            user currentUser = Session["user"] as user;
            WallPostsModel wpm = new WallPostsModel();

            //If there is no user, we are redirected to Login page
            try
            {

                currentUser = entities.users.First(u => u.id == currentUser.id);
            }
            catch
            {
                return RedirectToAction("Index", "Home");
            }

            ICollection<post> posts = currentUser.posts;
            ICollection<image> images = currentUser.images;
            DbSet<follower> followers = entities.followers;
 
            foreach(follower f in followers)
            {
                if (f.followerId == currentUser.id)
                {
                    using (var conn = new ifollowdatabaseEntities4())
                    {
                        user ff = conn.users.First(u => u.id == f.followedId);
                        foreach (image i in ff.images)
                        {
                            images.Add(i);
                        }

                        foreach (post p in ff.posts)
                        {
                            posts.Add(p);
                        }
                    }
                }
            }

            wpm.BuildFromImagesAndPosts(currentUser.posts, currentUser.images, currentUser.id);

            return View(wpm);          
        }

        public ActionResult WallPost()
        {
            return PartialView("_WallPosts");
        }

        public JsonResult AutoCompleteSearch(string term)
        {
                //var user = Session["user"] as User
            var result = (from r in entities.users
                            where r.firstName.ToLower().Contains(term.ToLower())
                            select new { r.firstName }).Distinct();
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public void Rate(string id, string value)
        {
            user currentUser = Session["user"] as user;
            rating newRating = new rating();
            newRating.userId = currentUser.id;
            newRating.postId = long.Parse(id);
            newRating.value = int.Parse(value);
            entities.ratings.Add(newRating);
            try
            {
                entities.SaveChanges();
            }
            catch (DbEntityValidationException dbEx)
            {
            }
        }

        public ActionResult SetComment(string currentComment,string postId)
        {
            user currentUser = Session["user"] as user;
            string username = currentUser.firstName + " " + currentUser.lastName;

            using (var entities = new ifollowdatabaseEntities4())
            {
                comment c = new comment();
                c.message = currentComment;
                c.postId = Convert.ToInt64(postId);
                c.userId = currentUser.id;
                c.dateCreated = DateTime.UtcNow;
                c.id = entities.comments.Count() + 1;
                entities.comments.Add(c);

                try
                {
                    entities.SaveChanges();
                }
                catch (DbEntityValidationException dbEx)
                {
                    foreach (var validationErrors in dbEx.EntityValidationErrors)
                    {
                        foreach (var validationError in validationErrors.ValidationErrors)
                        {
                            Trace.TraceInformation("Property: {0} Error: {1}", validationError.PropertyName, validationError.ErrorMessage);
                        }
                    }
                }
            }

            JsonResult json = Json(new { message = currentComment, id = postId, username = username });
            return json;
        }

        public ActionResult GetCommentsForPost(string postId)
        {
            List<WallComment> deliever = new List<WallComment>();

            long pId = Convert.ToInt64(postId);
            using (var entities = new ifollowdatabaseEntities4())
            {
                if (entities.comments.Any(c => c.postId == pId))
                {
                    List<comment> comms = entities.comments.ToList<comment>();
                    
                    foreach(var c in comms)
                    {
                        if (c.postId == pId)
                        {
                            WallComment cc = new WallComment();
                            cc.message = c.message;
                            cc.username = c.user.firstName + " " + c.user.lastName;
                            cc.dateCreated = (DateTime)c.dateCreated;
                            deliever.Add(cc);
                        }
                    }
                }
            }

            var jsonSerializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            JsonResult json = Json(new { list =  jsonSerializer.Serialize(deliever)}, JsonRequestBehavior.AllowGet);
            return json;            
        }

        public ActionResult Settings()
        {
            ViewBag.Message = "Your contact page.";

            SettingsModel sm = new SettingsModel();
            user currentUser = Session["user"] as user;

            try
            {
                currentUser = entities.users.First(u => u.id == currentUser.id);
            }
            catch
            {
                Url.Action("LogOff", "Account");
                //return RedirectToAction("Index", "Home");
            }

            if (currentUser != null)
            {
                sm.firstName = currentUser.firstName;
                sm.lastName = currentUser.lastName;
                sm.country = currentUser.country;
                sm.city = currentUser.city;
                //sm.birthDate = (System.DateTime)currentUser.birthdate;
            }

            return PartialView("_SettingsModal");
        }

        public ActionResult Post()
        {
            ViewBag.Message = "Your posting page.";
            return PartialView("_PostSettings");
        }

        public ActionResult Profile(string user)
        {
            long currentUserId;
            ProfileModel pm = new ProfileModel();
            pm.avatarPath = "";
            if (user == "current")
            {
                currentUserId = (Session["user"] as user).id;
                pm.isCurrentUser = true;
            }
            else
            {
                currentUserId = (long)Convert.ToDouble(user);
                pm.isCurrentUser = ((Session["user"] as user).id == currentUserId);
            }
                        
            user currentUser = entities.users.First(u => u.id == currentUserId);

            if (entities.images.Any(i => i.ownerId == currentUserId && i.isAvatar == true))
            {
                image avatar = entities.images.First(i => i.ownerId == currentUserId && i.isAvatar == true);
                pm.avatarPath = "~/Images/UserPhotos/" + avatar.url;
            }
            
            pm.userName = currentUser.firstName;
            pm.postsCount = currentUser.posts.Count();
            pm.followersCount = currentUser.followers.Count();
            pm.followedCount = currentUser.followers1.Count();
            pm.elements.BuildFromImagesAndPosts(currentUser.posts, currentUser.images, currentUserId);
            pm.userId = currentUser.id;

            ViewBag.Message = "Your profile page.";
            return PartialView("_ProfilePage", pm);
        }

        public ActionResult ViewPosts(long user)
        {
            ProfileModel pm = new ProfileModel();
            user thisUser = (Session["user"] as user);
            user currentUser = entities.users.First(u => u.id == user);
            pm.userName = currentUser.firstName;
            pm.postsCount = currentUser.posts.Count();
            pm.followersCount = currentUser.followers.Count();
            pm.followedCount = currentUser.followers1.Count();
            pm.elements.BuildFromImagesAndPosts(currentUser.posts, currentUser.images, thisUser.id);

            if (pm.elements.wallElements.Count() == 0)
            {
                return PartialView("_NoPosts");
            }

            return PartialView("_ProfilePagePosts", pm);
        }

        public ActionResult ViewFollowers(long user)
        {
            user currentUser = Session["user"] as user;

            FollowersModel fm = new FollowersModel();
            try
            {
                fm.BuildFollowersForUser(user);
            }
            catch
            {
                return PartialView("_NoFollowers");
            }

            return PartialView("_Followers", fm);
        }

        public ActionResult ViewFollowed(long user)
        {
            user currentUser = Session["user"] as user;

            FollowersModel fm = new FollowersModel();
            try
            {
                fm.BuildFollowedForUser(user);
            }
            catch
            {
                return PartialView("_NoUsersFollowed");
            }

            return PartialView("_Followers", fm);
        }

        public ActionResult SaveAvatar(ProfileModel fileModel)
        {
            JsonResult json = null;
            if (ModelState.IsValid)
            {
                image newImage = null;
                user currentUser = Session["user"] as user;

                if (fileModel != null && fileModel.File != null)
                {
                    string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff") + ".png";
                    var path = Path.Combine(Server.MapPath("~/Images/UserPhotos"), timestamp);
                    fileModel.File.SaveAs(path);
                    fileModel.avatarPath = path;

                    string oldFile = "";

                    if (entities.images.Any(i => i.ownerId == currentUser.id && i.isAvatar == true))
                    {
                        image avatar = entities.images.First(i => i.ownerId == currentUser.id && i.isAvatar == true);
                        oldFile = Path.Combine(Server.MapPath("~/Images/UserPhotos"), avatar.url);
                        avatar.url = timestamp;
                    }
                    else
                    {
                        newImage = new image();
                        newImage.isAvatar = true;
                        newImage.isDeleted = false;
                        newImage.url = timestamp;

                        int count = entities.images.Count();
                        newImage.id = count + 1;
                        newImage.ownerId = currentUser.id;

                        entities.images.Add(newImage);
                    }                    
                    
                    try
                    {
                        entities.SaveChanges();
                        json = Json(new { path = timestamp});
                        if (oldFile != "")
                        {
                            System.IO.File.Delete(oldFile);
                        }
                    }
                    catch (DbEntityValidationException dbEx)
                    {
                        foreach (var validationErrors in dbEx.EntityValidationErrors)
                        {
                            foreach (var validationError in validationErrors.ValidationErrors)
                            {
                                Trace.TraceInformation("Property: {0} Error: {1}", validationError.PropertyName, validationError.ErrorMessage);
                            }
                        }
                    }
                }
            }
            
            return json;
        }

        public ActionResult Followers()
        {
            ViewBag.Message = "Your followers page.";
            user currentUser = Session["user"] as user;
            
            FollowersModel fm = new FollowersModel();
            try
            {
                fm.BuildRecommendationsForUser(currentUser.id);
            }
            catch
            {
                Url.Action("LogOff", "Account");
                return RedirectToAction("Index", "Home");
            }           

            return PartialView("_Followers", fm);
        }
    }
}
