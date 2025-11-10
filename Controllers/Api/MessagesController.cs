using Online_chat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Data.Entity;
using System.Web.Mvc;

namespace Online_chat.Controllers.Api
{
    public class MessagesController : ApiController
    {
        private ApplicationDbContext _context = new ApplicationDbContext();

        // GET /api/messages
        public IHttpActionResult GetMessages()
        {
            var messages = _context.PrivateMessages
                .Include(m => m.Sender)
                .OrderByDescending(m => m.Timestamp);
            return Ok(messages);
        }
    }
}